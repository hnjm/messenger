using Messenger.Foundation;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Messenger.Launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Entrance(args);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        static void Entrance(string[] args)
        {
            Trace.Listeners.Add(new Logger($"{nameof(Launcher)}-{ DateTime.Now:yyyyMMdd}.log"));

            var nam = "Default Server";
            var max = Server.DefaultCountLimit;
            var pot = Server.DefaultPort;
            var bro = Broadcast.DefaultPort;
            var dic = new Dictionary<string, string>();
            foreach (var i in args)
            {
                var idx = i.Split(new char[] { ':' }, 2);
                if (idx.Length < 2)
                    continue;
                dic.Add(idx[0].ToLower(), idx[1]);
            }
            if (dic.TryGetValue("name", out var val))
                nam = val;
            if (dic.TryGetValue("max", out var lin))
                max = int.Parse(lin);
            if (dic.TryGetValue("port", out var por))
                pot = int.Parse(por);
            if (dic.TryGetValue("broadcast", out var bad))
                bro = int.Parse(bad);

            var srv = new Server();
            srv.Start(pot, max);

            var fuc = new Func<byte[], byte[]>((buf) =>
            {
                var rea = new PacketReader(buf);
                var str = rea["protocol"].Pull<string>();
                if (!str.Equals(Server.Protocol))
                    return null;
                var wtr = PacketWriter.Serialize(new
                {
                    protocol = Server.Protocol,
                    port = pot,
                    name = nam,
                    limit = max,
                    count = srv.Count,
                });
                return wtr.GetBytes();
            });
            var brs = new Broadcast() { Function = fuc };
            brs.Start();
        }
    }
}
