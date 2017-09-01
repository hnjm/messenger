using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;
using System;
using System.Diagnostics;
using System.Windows;

namespace Messenger.Handles
{
    [Handle("file")]
    public class Thing : LinkPacket
    {
        [Handle("info")]
        public void Take()
        {
            var tak = default(PortReceiver);
            try
            {
                tak = new PortReceiver(Data, () => Ports.FindPath(Data["filename"].Pull<string>()));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return;
            }
            var trs = new Cargo(Source, tak);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Ports.Expect.Add(trs);
                Ports.Takers.Add(trs);
                tak.Started += Ports.Trans_Changed;
                tak.Disposed += Ports.Trans_Changed;
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
