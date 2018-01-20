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

        private const string _CreateTable = "create table if not exists [message](" +
            "[key] text primary key, " +
            "[datetime] datetime not null, " +
            "[index] integer not null, " +
            "[source] integer not null, " +
            "[target] integer mot null, " +
            "[path] text not null, " +
            "[text] text)";

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

        private static void _ValidatePacket(string path, ref object value)
        {
            if (path == "image" && value is byte[] buf)
                value = CacheModule.SetBuffer(buf, false);
            else if ((path == "text" || path == "notice") && value is string str)
                return;
            else if (path == "share" && (value is Share || value is ShareReceiver))
                return;
            else throw new InvalidOperationException("Invalid condition!");
        }

        public static Packet Insert(int target, string path, object value)
        {
            _ValidatePacket(path, ref value);
            var pkt = new Packet(target, LinkModule.Id, target, path, value);
            _Insert(pkt);
            return pkt;
        }

        public static Packet Insert(int source, int target, string path, object value)
        {
            _ValidatePacket(path, ref value);

            var idx = target == LinkModule.Id ? source : target;
            var pkt = new Packet(idx, source, target, path, value);
            _Insert(pkt);
            _OnReceive(pkt);
            return pkt;
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        private static void _OnReceive(Packet pkt)
        {
            var arg = new LinkEventArgs<Packet>(pkt);
            Application.Current.Dispatcher.Invoke(() =>
            {
                s_ins._rec?.Invoke(s_ins, arg);
                s_ins._han?.Invoke(s_ins, arg);

                if (arg.Finish == true && arg.Cancel == false)
                    return;
                var pro = ProfileModule.Query(pkt.Index, true);
                pro.Hint += 1;
            });
        }

        /// <summary>
        /// 向数据库写入消息记录
        /// </summary>
        private static void _Insert(Packet pkt)
        {
            var lst = Query(pkt.Index);
            Application.Current.Dispatcher.Invoke(() => lst.Add(pkt));
            var con = s_ins._con;
            if (con == null)
                return;
            var str = pkt.Object as string;
            if (str == null)
                return;

            Task.Run(() =>
            {
                var cmd = default(SQLiteCommand);
                var arg = default(SQLiteParameterCollection);

                try
                {
                    cmd = new SQLiteCommand(con) { CommandText = "insert into [message] values(@key, @tic, @idx, @src, @dst, @pth, @txt)" };
                    arg = cmd.Parameters;

                    arg.AddWithValue("@key", pkt.Key);
                    arg.AddWithValue("@tic", pkt.DateTime);
                    arg.AddWithValue("@idx", pkt.Index);
                    arg.AddWithValue("@src", pkt.Source);
                    arg.AddWithValue("@dst", pkt.Target);
                    arg.AddWithValue("@pth", pkt.Path);
                    arg.AddWithValue("@txt", str);
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
            var con = s_ins._con;
            if (con == null)
                return lst;

            var cmd = default(SQLiteCommand);
            var arg = default(SQLiteParameterCollection);
            var rea = default(SQLiteDataReader);
            var lis = new List<Packet>();

            try
            {
                cmd = new SQLiteCommand(con) { CommandText = "select * from [message] where [index] = @idx order by [datetime] limit @max" };
                arg = cmd.Parameters;

                arg.AddWithValue("@idx", gid);
                arg.AddWithValue("@max", max);
                rea = cmd.ExecuteReader();

                while (rea.Read())
                {
                    var pkt = new Packet(
                        rea.GetString(0),
                        rea.GetDateTime(1),
                        rea.GetInt32(2),
                        rea.GetInt32(3),
                        rea.GetInt32(4),
                        rea.GetString(5),
                        rea.GetString(6));
                    lst.Add(pkt);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            finally
            {
                rea?.Close();
                cmd?.Dispose();
            }
            return lst;
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
                con = new SQLiteConnection($"data source={_Path}");
                con.Open();
                cmd = new SQLiteCommand(_CreateTable, con);
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

        public static void Remove(Packet pkt)
        {
            if (s_ins._msg.TryGetValue(pkt.Index, out var lst))
                lst.Remove(pkt);
            var con = s_ins._con;
            if (con == null)
                return;

            Task.Run(() =>
            {
                var cmd = default(SQLiteCommand);
                var arg = default(SQLiteParameterCollection);

                try
                {
                    cmd = new SQLiteCommand(con) { CommandText = "delete from [message] where [key] = @key" };
                    arg = cmd.Parameters;
                    arg.AddWithValue("@key", pkt.Key);
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
        /// 清除指定 <see cref="Packet.Index"/> 下的所有消息记录
        /// </summary>
        public static void Clear(int idx)
        {
            if (s_ins._msg.TryGetValue(idx, out var lst))
                lst.Clear();
            var con = s_ins._con;
            if (con == null)
                return;

            Task.Run(() =>
            {
                var cmd = default(SQLiteCommand);
                var arg = default(SQLiteParameterCollection);

                try
                {
                    cmd = new SQLiteCommand(con) { CommandText = "delete from [message] where [index] = @idx" };
                    arg = cmd.Parameters;
                    arg.AddWithValue("@idx", idx);
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
        public static void Exit()
        {
            s_ins._con?.Dispose();
            s_ins._con = null;
        }
    }
}
