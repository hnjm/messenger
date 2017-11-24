using Messenger.Models;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace Messenger.Modules
{
    internal class Posters
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
                source = Linkers.ID,
                target = target,
                path = pth,
                data = val,
            });
            var buf = wtr.GetBytes();
            Linkers.Enqueue(buf);
            Packets.Insert(target, val);
        }

        /// <summary>
        /// 向指定用户发送本机用户信息
        /// </summary>
        public static void UserProfile(int target)
        {
            var pro = Profiles.Current;
            var wtr = PacketWriter.Serialize(new
            {
                source = Linkers.ID,
                target = target,
                path = "user.profile",
                data = new
                {
                    id = pro.ID,
                    name = pro.Name,
                    text = pro.Text,
                    image = Profiles.ImageBuffer,
                },
            });
            var buf = wtr.GetBytes();
            Linkers.Enqueue(buf);
        }

        public static void UserRequest()
        {
            var wtr = PacketWriter.Serialize(new
            {
                source = Linkers.ID,
                target = Links.ID,
                path = "user.request",
            });
            var buf = wtr.GetBytes();
            Linkers.Enqueue(buf);
        }

        /// <summary>
        /// 发送请求监听的用户组
        /// </summary>
        public static void UserGroups()
        {
            var wtr = PacketWriter.Serialize(new
            {
                source = Linkers.ID,
                target = Links.ID,
                path = "user.group",
                data = Profiles.GroupIDs?.ToList(),
            });
            var buf = wtr.GetBytes();
            Linkers.Enqueue(buf);
        }

        /// <summary>
        /// 发送文件信息
        /// </summary>
        public static Share File(int target, string filepath)
        {
            var sha = new Share(new FileInfo(filepath));
            //var car = new Cargo(target, mak);
            //Application.Current.Dispatcher.Invoke(() => Ports.Makers.Add(car));
            var wtr = PacketWriter.Serialize(new
            {
                source = Linkers.ID,
                target = target,
                path = "file.info",
                data = new
                {
                    key = sha._key,
                    name = sha.Name,
                    length = sha.Length,
                    endpoints = Linkers.GetEndPoints(),
                }
            });
            var buf = wtr.GetBytes();
            Linkers.Enqueue(buf);
            return sha;
        }
    }
}
