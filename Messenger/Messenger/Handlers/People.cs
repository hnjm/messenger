using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;
using System.Linq;

namespace Messenger.Handlers
{
    [Handler("user")]
    public class People : LinkPacket
    {
        [Handler("request")]
        public void Request()
        {
            Posters.UserProfile(Source);
        }

        [Handler("profile")]
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

        [Handler("list")]
        public void List()
        {
            var lst = Data.PullList<int>().ToList();
            Profiles.Remove(lst);
        }
    }
}
