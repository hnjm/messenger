using Messenger.Models;
using Messenger.Modules;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.Windows;

namespace Messenger.Controllers
{
    /// <summary>
    /// 处理共享信息
    /// </summary>
    [Handle("share")]
    public class ShareController : LinkPacket
    {
        /// <summary>
        /// 处理共享信息
        /// </summary>
        [Handle("info")]
        public void Take()
        {
            var rec = new ShareReceiver(Source, Data);
            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //    ShareModule.Expect.Add(trs);
            //    ShareModule.Takers.Add(trs);
            //    tak.Started += ShareModule.Trans_Changed;
            //    tak.Disposed += ShareModule.Trans_Changed;
            //});
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShareModule.Register(rec);
                var pkt = new Packet() { Source = Source, Target = LinkModule.ID, Groups = Source, Path = "share", Value = rec };
                var pks = HistoryModule.Query(Source);
                pks.Add(pkt);
            });
        }

        ///// <summary>
        ///// 处理传入的文件信息
        ///// </summary>
        //[Handle("dir")]
        //public void Directory()
        //{
        //    var tak = default(PortTaker);
        //    try
        //    {
        //        tak = new PortTaker(Data);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error(ex);
        //        return;
        //    }
        //    var trs = new Cargo(Source, tak);
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        ShareModule.Expect.Add(trs);
        //        ShareModule.Takers.Add(trs);
        //        tak.Started += ShareModule.Trans_Changed;
        //        tak.Disposed += ShareModule.Trans_Changed;
        //    });
        //    var pkt = new Packet() { Source = Source, Target = Linkers.ID, Groups = Source, Path = "share", Value = trs };
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        var pks = Packets.Query(Source);
        //        pks.Add(pkt);
        //    });
        //}
    }
}
