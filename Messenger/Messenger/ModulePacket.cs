using Messenger.Foundation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.Windows;

namespace Messenger
{
    /// <summary>
    /// 管理用户聊天记录
    /// </summary>
    class ModulePacket
    {
        /// <summary>
        /// 默认数据库路径
        /// </summary>
        private const string DefaultPath = nameof(Messenger) + ".db";

        /// <summary>
        /// 数据库实例 (为 null 说明出错, 此时相当于 "阅后即焚")
        /// </summary>
        private SQLiteConnection _connection = null;
        private ConcurrentDictionary<int, BindingList<ItemPacket>> _messages = new ConcurrentDictionary<int, BindingList<ItemPacket>>();
        private event EventHandler<GenericEventArgs<ItemPacket>> _Receiving = null;
        private event EventHandler<GenericEventArgs<ItemPacket>> _OnHandled = null;

        private static ModulePacket _instance = null;

        /// <summary>
        /// 消息接收事件
        /// </summary>
        public static event EventHandler<GenericEventArgs<ItemPacket>> Receiving { add => _instance._Receiving += value; remove => _instance._Receiving -= value; }
        /// <summary>
        /// 消息接收事件处理后
        /// </summary>
        public static event EventHandler<GenericEventArgs<ItemPacket>> OnHandled { add => _instance._OnHandled += value; remove => _instance._OnHandled -= value; }

        /// <summary>
        /// 补全消息记录 (自动判断消息类型)
        /// </summary>
        private static ItemPacket SetPacket(ItemPacket pkt, PacketGenre genre, object value)
        {
            if (value is string str && genre == PacketGenre.MessageText)
                pkt.Value = str;
            else if (value is byte[] byt && genre == PacketGenre.MessageImage)
                pkt.Value = Cache.SetBuffer(byt, false);
            else
                throw new ApplicationException();
            pkt.Genre = genre;
            return pkt;
        }

        /// <summary>
        /// 插入一条消息记录
        /// </summary>
        public static ItemPacket Insert(int gid, PacketGenre type, object obj)
        {
            var rcd = new ItemPacket() { Source = Interact.ID, Target = gid, Groups = gid };
            SetPacket(rcd, type, obj);
            Insert(rcd);
            return rcd;
        }

        /// <summary>
        /// 插入一条消息记录
        /// </summary>
        public static ItemPacket Insert(IPacketHeader header, object value)
        {
            int gid = (header.Target == Interact.ID) ? header.Source : header.Target;
            var pkt = new ItemPacket() { Source = header.Source, Target = header.Target, Groups = gid };
            SetPacket(pkt, header.Genre, value);
            Insert(pkt);
            OnReceived(pkt);
            return pkt;
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        private static void OnReceived(ItemPacket rcd)
        {
            var arg = new GenericEventArgs<ItemPacket>() { Value = rcd };
            _instance._Receiving?.Invoke(_instance, arg);
            _instance._OnHandled?.Invoke(_instance, arg);
        }

        /// <summary>
        /// 向数据库写入消息记录
        /// </summary>
        private static void Insert(ItemPacket pkt)
        {
            if (_instance == null)
                return;
            if (_instance._messages.TryGetValue(pkt.Groups, out var lst))
                Application.Current.Dispatcher.Invoke(() => lst.Add(pkt));
            if (_instance._connection == null)
                return;
            var str = pkt.Value as string;
            if (str == null)
                return;

            Task.Run(() =>
                {
                    var cmd = default(SQLiteCommand);
                    try
                    {
                        cmd = new SQLiteCommand(_instance._connection);
                        cmd.CommandText = "insert into Messages values(@sid, @tid, @gid, @tim, @typ, @msg)";
                        cmd.Parameters.Add(new SQLiteParameter("@sid", pkt.Source));
                        cmd.Parameters.Add(new SQLiteParameter("@tid", pkt.Target));
                        cmd.Parameters.Add(new SQLiteParameter("@gid", pkt.Groups));
                        cmd.Parameters.Add(new SQLiteParameter("@tim", pkt.Timestamp.ToBinary()));
                        cmd.Parameters.Add(new SQLiteParameter("@typ", (long)pkt.Genre));
                        cmd.Parameters.Add(new SQLiteParameter("@msg", str));
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Log.E(nameof(ModulePacket), ex, "记录消息出错");
                    }
                    finally
                    {
                        cmd?.Dispose();
                    }
                });
        }

        /// <summary>
        /// 依据编号查询 并返回最近的 N 条消息记录 (返回值不会为 null)
        /// </summary>
        public static BindingList<ItemPacket> Query(int gid, int max = 32)
        {
            var lst = _instance._messages.GetOrAdd(gid, _ => new BindingList<ItemPacket>());
            if (lst.Count > 0)
                return lst;
            if (_instance?._connection == null)
                return lst;

            var cmd = default(SQLiteCommand);
            var rdr = default(SQLiteDataReader);
            var lis = new List<ItemPacket>();
            try
            {
                cmd = new SQLiteCommand(_instance._connection);
                cmd.CommandText = "select * from Messages where GroupsID = @gid order by MessageTime desc limit 0,@max";
                cmd.Parameters.Add(new SQLiteParameter("@gid", gid));
                cmd.Parameters.Add(new SQLiteParameter("@max", max));
                rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var rcd = new ItemPacket();
                    rcd.Source = rdr.GetInt32(0);
                    rcd.Target = rdr.GetInt32(1);
                    rcd.Groups = rdr.GetInt32(2);
                    rcd.Timestamp = DateTime.FromBinary(rdr.GetInt64(3));
                    rcd.Genre = (PacketGenre)rdr.GetInt64(4);
                    rcd.Value = rdr.GetString(5);
                    lis.Add(rcd);
                }
                // 查询是按照降序排列的 因此需要反转
                for (var i = lis.Count - 1; i >= 0; i--)
                    lst.Add(lis[i]);
                return lst;
            }
            catch (Exception ex)
            {
                Log.E(nameof(ModulePacket), ex, "读取消息出错.");
                return lst;
            }
            finally
            {
                rdr?.Close();
                cmd?.Dispose();
            }
        }

        /// <summary>
        /// 初始化数据库 (非线程安全)
        /// </summary>
        public static void Load()
        {
            if (_instance != null)
                return;
            _instance = new ModulePacket();
            var con = default(SQLiteConnection);
            var cmd = default(SQLiteCommand);
            try
            {
                con = new SQLiteConnection($"data source = {DefaultPath}");
                con.Open();
                cmd = new SQLiteCommand(con);
                // 消息类型(枚举), 消息时间(时间戳) 均转换成整形存储
                cmd.CommandText = "create table if not exists Messages(SourceID integer not null, TargetID integer not null, GroupsID integer not null, " +
                    "MessageTime integer not null, MessageType integer not null, Message varchar not null)";
                cmd.ExecuteNonQuery();
                // 确保连接有效
                _instance._connection = con;
            }
            catch (Exception ex)
            {
                Log.E(nameof(ModulePacket), ex, "数据库启用失败.");
                con?.Dispose();
            }
            finally
            {
                cmd?.Dispose();
            }
        }

        public static void Remove(ItemPacket record)
        {
            if (_instance == null)
                return;
            if (_instance._messages.TryGetValue(record.Groups, out var lst))
                lst.Remove(record);
            if (_instance._connection == null)
                return;

            Task.Run(() =>
                {
                    var cmd = default(SQLiteCommand);
                    try
                    {
                        cmd = new SQLiteCommand(_instance._connection);
                        cmd.CommandText = "delete from Messages where GroupsID == @gid and MessageTime == @mrt";
                        cmd.Parameters.Add(new SQLiteParameter("@gid", record.Groups));
                        cmd.Parameters.Add(new SQLiteParameter("@mrt", record.Timestamp.ToBinary()));
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Log.E(nameof(ModulePacket), ex, "记录移除出错");
                    }
                    finally
                    {
                        cmd?.Dispose();
                    }
                });
        }

        /// <summary>
        /// 清除指定 <see cref="ItemPacket.Groups"/> 下的所有消息记录
        /// </summary>
        public static void Clear(int gid)
        {
            if (_instance == null)
                return;
            if (_instance._messages.TryGetValue(gid, out var lst))
                lst.Clear();
            if (_instance._connection == null)
                return;

            Task.Run(() =>
                {
                    var cmd = default(SQLiteCommand);
                    try
                    {
                        cmd = new SQLiteCommand(_instance._connection);
                        cmd.CommandText = "delete from Messages where GroupsID == @gid";
                        cmd.Parameters.Add(new SQLiteParameter("@gid", gid));
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Log.E(nameof(ModulePacket), ex, "记录清空出错");
                    }
                    finally
                    {
                        cmd?.Dispose();
                    }
                });
        }

        /// <summary>
        /// 关闭数据库
        /// </summary>
        public static void Save()
        {
            _instance?._connection?.Dispose();
            _instance = null;
        }
    }
}
