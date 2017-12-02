using Messenger.Models;
using Mikodev.Network;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace Messenger.Modules
{
    internal class PostModule
    {
        /// <summary>
        /// Post text message or image
        /// </summary>
        public static void Message(int target, object val)
        {
            var pth = string.Empty;
            if (val is string)
                pth = "msg.text";
            else if (val is byte[])
                pth = "msg.image";
            else throw new ApplicationException();

            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.ID,
                target = target,
                path = pth,
                data = val,
            });
            var buf = wtr.GetBytes();
            LinkModule.Enqueue(buf);
            HistoryModule.Insert(target, val);
        }

        /// <summary>
        /// 向指定用户发送本机用户信息
        /// </summary>
        public static void UserProfile(int target)
        {
            var pro = ProfileModule.Current;
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.ID,
                target = target,
                path = "user.profile",
                data = new
                {
                    id = pro.ID,
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
                source = LinkModule.ID,
                target = Links.ID,
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
                source = LinkModule.ID,
                target = Links.ID,
                path = "user.group",
                data = ProfileModule.GroupIDs,
            });
            var buf = wtr.GetBytes();
            LinkModule.Enqueue(buf);
        }

        /// <summary>
        /// 发送文件信息
        /// </summary>
        public static Share File(int target, string filepath)
        {
            var sha = new Share(new FileInfo(filepath));
            Application.Current.Dispatcher.Invoke(() => ShareModule.ShareList.Add(sha));
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.ID,
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
            return sha;
        }

        public static Share Directory(int target, string directory)
        {
            var sha = new Share(new DirectoryInfo(directory));
            Application.Current.Dispatcher.Invoke(() => ShareModule.ShareList.Add(sha));
            var wtr = PacketWriter.Serialize(new
            {
                source = LinkModule.ID,
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
            return sha;
        }
    }
}
