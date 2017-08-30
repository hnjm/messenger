using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;
using System.Linq;

namespace Messenger.Handles
{
    [Handle("user")]
    public class People : LinkPacket
    {
        [Handle("request")]
        public void Request()
        {
            Posters.UserProfile(Source);
        }

        [Handle("profile")]
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
                pro.Image = Caches.SetBuffer(buf, true);
            Profiles.Insert(pro);
        }

        [Handle("list")]
        public void List()
        {
            var lst = Data.PullList<int>().ToList();
            Profiles.Remove(lst);
        }
    }
}
