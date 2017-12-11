using Messenger.Models;
using Mikodev.Network;
using System.IO;
using System.Windows;

namespace Messenger.Modules
{
    internal class PostModule
    {
        public static void Text(int target, string val)
        {
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.Id,
                target = target,
                path = "msg.text",
                data = val,
            });
            var buf = wtr.GetBytes();
            LinkModule.Enqueue(buf);
            HistoryModule.Insert(target, "text", val);
        }

        public static void Image(int target, byte[] val)
        {
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.Id,
                target = target,
                path = "msg.image",
                data = val,
            });
            var buf = wtr.GetBytes();
            LinkModule.Enqueue(buf);
            HistoryModule.Insert(target, "image", val);
        }

        /// <summary>
        /// Post feedback message
        /// </summary>
        public static void Notice(int target, string type, string parameter)
        {
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.Id,
                target = target,
                path = "msg.notice",
                data = new
                {
                    type = type,
                    parameter = parameter,
                },
            });
            var buf = wtr.GetBytes();
            LinkModule.Enqueue(buf);
            // you don't have to notice yourself in history module
        }

        /// <summary>
        /// 向指定用户发送本机用户信息
        /// </summary>
        public static void UserProfile(int target)
        {
            var pro = ProfileModule.Current;
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.Id,
                target = target,
                path = "user.profile",
                data = new
                {
                    id = pro.Id,
                    name = pro.Name,
                    text = pro.Text,
                    image = ProfileModule.ImageBuffer,
                },
            });
            var buf = wtr.GetBytes();
            LinkModule.Enqueue(buf);
        }

        public static void UserRequest()
        {
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.Id,
                target = Links.Id,
                path = "user.request",
            });
            var buf = wtr.GetBytes();
            LinkModule.Enqueue(buf);
        }

        /// <summary>
        /// 发送请求监听的用户组
        /// </summary>
        public static void UserGroups()
        {
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.Id,
                target = Links.Id,
                path = "user.group",
                data = ProfileModule.GroupIds,
            });
            var buf = wtr.GetBytes();
            LinkModule.Enqueue(buf);
        }

        /// <summary>
        /// 发送文件信息
        /// </summary>
        public static void File(int target, string filepath)
        {
            var sha = new Share(new FileInfo(filepath));
            Application.Current.Dispatcher.Invoke(() => ShareModule.ShareList.Add(sha));
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.Id,
                target = target,
                path = "share.info",
                data = new
                {
                    key = sha._key,
                    type = "file",
                    name = sha.Name,
                    length = sha.Length,
                    endpoints = LinkModule.GetEndPoints(),
                }
            });
            var buf = wtr.GetBytes();
            LinkModule.Enqueue(buf);
            HistoryModule.Insert(target, "share", sha);
        }

        public static void Directory(int target, string directory)
        {
            var sha = new Share(new DirectoryInfo(directory));
            Application.Current.Dispatcher.Invoke(() => ShareModule.ShareList.Add(sha));
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.Id,
                target = target,
                path = "share.info",
                data = new
                {
                    key = sha._key,
                    type = "dir",
                    name = sha.Name,
                    endpoints = LinkModule.GetEndPoints(),
                }
            });
            var buf = wtr.GetBytes();
            LinkModule.Enqueue(buf);
            HistoryModule.Insert(target, "share", sha);
        }
    }
}
