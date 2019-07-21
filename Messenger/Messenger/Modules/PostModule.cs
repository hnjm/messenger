using Messenger.Models;
using Mikodev.Network;
using System.IO;
using System.Windows;

namespace Messenger.Modules
{
    internal class PostModule
    {
        public static void Text(int targetId, string item)
        {
            var buffer = LinkExtension.Generator.ToBytes(new
            {
                source = LinkModule.Id,
                target = targetId,
                path = "msg.text",
                data = item,
            });
            LinkModule.Enqueue(buffer);
            _ = HistoryModule.Insert(targetId, "text", item);
        }

        public static void Image(int targetId, byte[] item)
        {
            var buffer = LinkExtension.Generator.ToBytes(new
            {
                source = LinkModule.Id,
                target = targetId,
                path = "msg.image",
                data = item,
            });
            LinkModule.Enqueue(buffer);
            _ = HistoryModule.Insert(targetId, "image", item);
        }

        /// <summary>
        /// Post feedback message
        /// </summary>
        public static void Notice(int dst, string genre, string arg)
        {
            var buffer = LinkExtension.Generator.ToBytes(new
            {
                source = LinkModule.Id,
                target = dst,
                path = "msg.notice",
                data = new
                {
                    type = genre,
                    parameter = arg,
                },
            });
            LinkModule.Enqueue(buffer);
            // you don't have to notice yourself in history module
        }

        /// <summary>
        /// 向指定用户发送本机用户信息
        /// </summary>
        public static void UserProfile(int targetId)
        {
            var profile = ProfileModule.Current;
            var buffer = LinkExtension.Generator.ToBytes(new
            {
                source = LinkModule.Id,
                target = targetId,
                path = "user.profile",
                data = new
                {
                    id = ProfileModule.Id,
                    name = profile.Name,
                    text = profile.Text,
                    image = ProfileModule.ImageBuffer,
                },
            });
            LinkModule.Enqueue(buffer);
        }

        public static void UserRequest()
        {
            var buffer = LinkExtension.Generator.ToBytes(new
            {
                source = LinkModule.Id,
                target = Links.Id,
                path = "user.request",
            });
            LinkModule.Enqueue(buffer);
        }

        /// <summary>
        /// 发送请求监听的用户组
        /// </summary>
        public static void UserGroups()
        {
            var buffer = LinkExtension.Generator.ToBytes(new
            {
                source = LinkModule.Id,
                target = Links.Id,
                path = "user.group",
                data = ProfileModule.GroupIds,
            });
            LinkModule.Enqueue(buffer);
        }

        /// <summary>
        /// 发送文件信息
        /// </summary>
        public static void File(int dst, string filepath)
        {
            var share = new Share(new FileInfo(filepath));
            Application.Current.Dispatcher.Invoke(() => ShareModule.ShareList.Add(share));
            var buffer = LinkExtension.Generator.ToBytes(new
            {
                source = LinkModule.Id,
                target = dst,
                path = "share.info",
                data = new
                {
                    key = share._key,
                    type = "file",
                    name = share.Name,
                    length = share.Length,
                    endpoints = LinkModule.GetEndPoints(),
                }
            });
            LinkModule.Enqueue(buffer);
            _ = HistoryModule.Insert(dst, "share", share);
        }

        public static void Directory(int dst, string directory)
        {
            var share = new Share(new DirectoryInfo(directory));
            Application.Current.Dispatcher.Invoke(() => ShareModule.ShareList.Add(share));
            var buffer = LinkExtension.Generator.ToBytes(new
            {
                source = LinkModule.Id,
                target = dst,
                path = "share.info",
                data = new
                {
                    key = share._key,
                    type = "dir",
                    name = share.Name,
                    endpoints = LinkModule.GetEndPoints(),
                }
            });
            LinkModule.Enqueue(buffer);
            _ = HistoryModule.Insert(dst, "share", share);
        }
    }
}
