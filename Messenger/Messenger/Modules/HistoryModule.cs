using Messenger.Models;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.Windows;

namespace Messenger.Modules
{
    /// <summary>
    /// 管理用户聊天记录
    /// </summary>
    internal class HistoryModule
    {
        /// <summary>
        /// 默认数据库路径
        /// </summary>
        private const string _Path = nameof(Messenger) + ".db";

        /// <summary>
        /// 数据库实例 (为 null 说明出错, 此时相当于 "阅后即焚")
        /// </summary>
        private SQLiteConnection _con = null;
        private ConcurrentDictionary<int, BindingList<Packet>> _msg = new ConcurrentDictionary<int, BindingList<Packet>>();
        private EventHandler<LinkEventArgs<Packet>> _rec = null;
        private EventHandler<LinkEventArgs<Packet>> _han = null;

        private static HistoryModule s_ins = new HistoryModule();

        /// <summary>
        /// 消息接收事件
        /// </summary>
        public static event EventHandler<LinkEventArgs<Packet>> Receive { add => s_ins._rec += value; remove => s_ins._rec -= value; }

        /// <summary>
        /// 消息接收事件处理后
        /// </summary>
        public static event EventHandler<LinkEventArgs<Packet>> Handled { add => s_ins._han += value; remove => s_ins._han -= value; }

        private static Packet _SetPacket(Packet pkt, string path, object value)
        {
            if ((path == "text" || path == "notice") && value is string)
                pkt.Object = value;
            else if (path == "image" && value is byte[] buf)
                pkt.Object = CacheModule.SetBuffer(buf, false);
            else if (path == "share" && (value is Share || value is ShareReceiver))
                pkt.Object = value;
            else throw new InvalidOperationException("Invalid condition!");
            pkt.Path = path;
            return pkt;
        }

        public static Packet Insert(int target, string path, object obj)
        {
            var pkt = new Packet() { Source = LinkModule.Id, Target = target, Group = target };
            _SetPacket(pkt, path, obj);
            _Insert(pkt);
            return pkt;
        }

        public static Packet Insert(int source, int target, string path, object value)
        {
            var gid = target == LinkModule.Id ? source : target;
            var pkt = new Packet() { Source = source, Target = target, Group = gid };
            _SetPacket(pkt, path, value);
            _Insert(pkt);
            _OnReceive(pkt);
            return pkt;
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        private static void _OnReceive(Packet rcd)
        {
            var arg = new LinkEventArgs<Packet>(rcd);
            Application.Current.Dispatcher.Invoke(() =>
            {
                s_ins._rec?.Invoke(s_ins, arg);
                s_ins._han?.Invoke(s_ins, arg);

                if (arg.Finish == true && arg.Cancel == false)
                    return;
                var pro = ProfileModule.Query(rcd.Group, true);
                pro.Hint += 1;
            });
        }

        /// <summary>
        /// 向数据库写入消息记录
        /// </summary>
        private static void _Insert(Packet pkt)
        {
            var lst = Query(pkt.Group);
            Application.Current.Dispatcher.Invoke(() => lst.Add(pkt));
            if (s_ins._con == null)
                return;
            var str = pkt.Object as string;
            if (str == null)
                return;

            Task.Run(() =>
            {
                var cmd = default(SQLiteCommand);
                try
                {
                    cmd = new SQLiteCommand(s_ins._con) { CommandText = "insert into messages values(@sid, @tid, @gid, @tim, @typ, @msg)" };
                    cmd.Parameters.Add(new SQLiteParameter("@sid", pkt.Source));
                    cmd.Parameters.Add(new SQLiteParameter("@tid", pkt.Target));
                    cmd.Parameters.Add(new SQLiteParameter("@gid", pkt.Group));
                    cmd.Parameters.Add(new SQLiteParameter("@tim", pkt.Timestamp));
                    cmd.Parameters.Add(new SQLiteParameter("@typ", pkt.Path));
                    cmd.Parameters.Add(new SQLiteParameter("@msg", str));
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
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
        public static BindingList<Packet> Query(int gid, int max = 32)
        {
            var lst = s_ins._msg.GetOrAdd(gid, _ => new BindingList<Packet>());
            if (lst.Count > 0)
                return lst;
            if (s_ins._con == null)
                return lst;

            var cmd = default(SQLiteCommand);
            var rdr = default(SQLiteDataReader);
            var lis = new List<Packet>();
            try
            {
                cmd = new SQLiteCommand(s_ins._con) { CommandText = "select * from messages where port = @gid order by tick desc limit 0,@max" };
                cmd.Parameters.Add(new SQLiteParameter("@gid", gid));
                cmd.Parameters.Add(new SQLiteParameter("@max", max));
                rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var rcd = new Packet
                    {
                        Source = rdr.GetInt32(0),
                        Target = rdr.GetInt32(1),
                        Group = rdr.GetInt32(2),
                        Timestamp = rdr.GetDateTime(3),
                        Path = rdr.GetString(4),
                        Object = rdr.GetString(5)
                    };
                    lis.Add(rcd);
                }
                // 查询是按照降序排列的 因此需要反转
                for (var i = lis.Count - 1; i >= 0; i--)
                    lst.Add(lis[i]);
                return lst;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
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
        [Loader(1, LoaderFlags.OnLoad)]
        public static void Load()
        {
            var con = default(SQLiteConnection);
            var cmd = default(SQLiteCommand);
            try
            {
                con = new SQLiteConnection($"data source = {_Path}");
                con.Open();
                cmd = new SQLiteCommand(con)
                {
                    CommandText = "create table if not exists messages(" +
                    "source integer not null, target integer not null, port integer not null, " +
                    "tick timestamp not null, path varchar not null, text varchar not null)"
                };
                cmd.ExecuteNonQuery();
                // 确保连接有效
                s_ins._con = con;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                con?.Dispose();
            }
            finally
            {
                cmd?.Dispose();
            }
        }

        public static void Remove(Packet record)
        {
            if (s_ins._msg.TryGetValue(record.Group, out var lst))
                lst.Remove(record);
            if (s_ins._con == null)
                return;

            Task.Run(() =>
            {
                var cmd = default(SQLiteCommand);
                try
                {
                    cmd = new SQLiteCommand(s_ins._con) { CommandText = "delete from messages where port == @gid and tick == @mrt" };
                    cmd.Parameters.Add(new SQLiteParameter("@gid", record.Group));
                    cmd.Parameters.Add(new SQLiteParameter("@mrt", record.Timestamp));
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
                finally
                {
                    cmd?.Dispose();
                }
            });
        }

        /// <summary>
        /// 清除指定 <see cref="Packet.Group"/> 下的所有消息记录
        /// </summary>
        public static void Clear(int gid)
        {
            if (s_ins._msg.TryGetValue(gid, out var lst))
                lst.Clear();
            if (s_ins._con == null)
                return;

            Task.Run(() =>
            {
                var cmd = default(SQLiteCommand);
                try
                {
                    cmd = new SQLiteCommand(s_ins._con) { CommandText = "delete from messages where port == @gid" };
                    cmd.Parameters.Add(new SQLiteParameter("@gid", gid));
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
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
        [Loader(1, LoaderFlags.OnExit)]
        public static void Save()
        {
            s_ins._con?.Dispose();
            s_ins._con = null;
        }
    }
}
