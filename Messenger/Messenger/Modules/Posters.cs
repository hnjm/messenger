using Messenger.Models;
using Mikodev.Network;
using System;
using System.Diagnostics;
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

        public static void UserGroups()
        {
            var wtr = PacketWriter.Serialize(new
            {
                source = Linkers.ID,
                target = Links.ID,
                path = "user.groups",
                data = Profiles.GroupIDs?.ToList(),
            });
            var buf = wtr.GetBytes();
            Linkers.Enqueue(buf);
        }

        public static Cargo File(int target, string filepath)
        {
            var mak = default(Maker);
            try
            {
                mak = new Maker(filepath);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return null;
            }
            var car = new Cargo(target, mak);
            Application.Current.Dispatcher.Invoke(() => Transports.Makers.Add(car));
            var wtr = PacketWriter.Serialize(new
            {
                source = Linkers.ID,
                target = target,
                path = "file.info",
                data = new
                {
                    filename = mak.Name,
                    filesize = mak.Length,
                    guid = mak.Key,
                    endpoints = Linkers.GetEndPoints(),
                }
            });
            var buf = wtr.GetBytes();
            Linkers.Enqueue(buf);
            return car;
        }
    }
}
