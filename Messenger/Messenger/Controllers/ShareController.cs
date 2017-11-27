using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;
using System.Windows;

namespace Messenger.Controllers
{
    /// <summary>
    /// 处理共享信息
    /// </summary>
    public class ShareController : LinkPacket
    {
        [Route("share.info")]
        public void Take()
        {
            var rec = new ShareReceiver(Source, Data);
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShareModule.Register(rec);
                var pkt = new Packet() { Source = Source, Target = LinkModule.ID, Groups = Source, Path = "share", Value = rec };
                var pks = HistoryModule.Query(Source);
                pks.Add(pkt);
            });
        }
    }
}
