using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;
using System.Windows;

namespace Messenger.Controllers
{
    [Route("share")]
    public class ShareController : LinkPacket
    {
        /// <summary>
        /// 处理共享信息
        /// </summary>
        [Route("info")]
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
