using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;
using System.Linq;

namespace Messenger.Controllers
{
    /// <summary>
    /// 处理用户信息
    /// </summary>
    [Route("user")]
    public class ProfileController : LinkPacket
    {
        /// <summary>
        /// 向发送者返回本机的用户信息
        /// </summary>
        [Route("request")]
        public void Request()
        {
            PostModule.UserProfile(Source);
        }

        /// <summary>
        /// 处理传入的用户信息
        /// </summary>
        [Route("profile")]
        public void Profile()
        {
            var pro = new Profile()
            {
                ID = Data["id"].Pull<int>(),
                Name = Data["name"].Pull<string>(),
                Text = Data["text"].Pull<string>(),
            };

            var buf = Data["image"].PullList();
            if (buf.Length > 0)
                pro.Image = CacheModule.SetBuffer(buf, true);
            ProfileModule.Insert(pro);
        }

        /// <summary>
        /// 处理服务器返回的用户 ID 列表
        /// </summary>
        [Route("list")]
        public void List()
        {
            var lst = Data.PullList<int>().ToList();
            ProfileModule.Remove(lst);
        }
    }
}
