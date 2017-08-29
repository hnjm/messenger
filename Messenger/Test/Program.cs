using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mikodev.Network;
using System.Net;
using System.Threading;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var lis = new LinkListener();
            lis.Start();

            Task.Run(() =>
            {
                var iep = new IPEndPoint(IPAddress.Loopback, Links.Port);
                var clt = new LinkClient(100);
                clt.Start(iep);
                clt.Received += (s, e) =>
                {
                    var rcd = e.Record;
                    var str = rcd.Data.Pull<string>();
                    Console.WriteLine(str);
                };

                var oth = new LinkClient(10);
                oth.Start(iep);
                oth.Enqueue(PacketWriter.Serialize(new
                {
                    source = 10,
                    target = 100,
                    path = "msg.text",
                    data = Encoding.UTF8.GetBytes("Hello"),

                }).GetBytes());
                Thread.Sleep(100);
                oth.Dispose();
            });
            Console.ReadLine();
        }
    }
}
