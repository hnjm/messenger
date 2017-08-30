using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;
using System;
using System.Diagnostics;
using System.Windows;

namespace Messenger.Handlers
{
    [Handler("file")]
    public class Thing : LinkPacket
    {
        [Handler("info")]
        public void Take()
        {
            var tak = default(Taker);
            try
            {
                tak = new Taker(Data, () => Transports.FindPath(Data["filename"].Pull<string>()));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return;
            }
            var trs = new Cargo(Source, tak);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Transports.Expect.Add(trs);
                Transports.Takers.Add(trs);
                tak.Started += Transports.Trans_Changed;
                tak.Disposed += Transports.Trans_Changed;
            });
            var pkt = new Packet() { Source = Source, Target = Linkers.ID, Groups = Source, Path = "file", Value = trs };
            Application.Current.Dispatcher.Invoke(() =>
            {
                var pks = Packets.Query(Source);
                pks.Add(pkt);
            });
        }
    }
}
