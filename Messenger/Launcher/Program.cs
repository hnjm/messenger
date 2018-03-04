﻿using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Launcher
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Run($"{nameof(Launcher)}.log");

            var add = IPAddress.Any;
            var nam = default(string);
            var max = Links.ServerSocketLimit;
            var pot = Links.Port;
            var bro = Links.BroadcastPort;
            var dic = new Dictionary<string, string>();

            foreach (var i in args)
            {
                var idx = i.Split(new char[] { ':' }, 2);
                if (idx.Length < 2)
                    continue;
                dic.Add(idx[0].ToLower(), idx[1]);
            }

            try
            {
                if (dic.TryGetValue("name", out var str))
                    nam = str;
                if (dic.TryGetValue("addr", out str))
                    add = IPAddress.Parse(str);
                if (dic.TryGetValue("max", out str))
                    max = int.Parse(str);
                if (dic.TryGetValue("tcpport", out str))
                    pot = int.Parse(str);
                if (dic.TryGetValue("udpport", out str))
                    bro = int.Parse(str);

                await LinkListener.Run(add, pot, bro, max, nam);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            Log.Close();
        }
    }
}
