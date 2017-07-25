using Messenger.Foundation;
using System;

namespace Messenger.Models
{
    [AttributeUsage(AttributeTargets.Method)]
    public class HandlerAttribute : Attribute
    {
        private PacketGenre _genre = PacketGenre.None;

        public PacketGenre Genre => _genre;

        public HandlerAttribute(PacketGenre genre)
        {
            _genre = genre;
        }
    }
}
