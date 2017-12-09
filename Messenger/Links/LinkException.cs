using System;
using System.Runtime.Serialization;

namespace Mikodev.Network
{
    [Serializable]
    public class LinkException : Exception
    {
        internal readonly LinkError _error = LinkError.None;

        public LinkException(LinkError error) : base(_GetMessage(error)) => error = _error;

        public LinkException(LinkError error, Exception inner) : base(_GetMessage(error), inner) => error = _error;

        protected LinkException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            _error = (LinkError)info.GetValue(nameof(LinkError), typeof(LinkError));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            info.AddValue(nameof(LinkError), _error);
            base.GetObjectData(info, context);
        }

        /// <summary>
        /// 如果传入值不为 <see cref="LinkError.Success"/>, 则抛出异常
        /// </summary>
        public static void ThrowError(LinkError error)
        {
            if (error == LinkError.Success)
                return;
            throw new LinkException(error);
        }

        internal static string _GetMessage(LinkError error)
        {
            switch (error)
            {
                case LinkError.Success:
                    return "Operation successful without error!";
                case LinkError.Overflow:
                    return "Buffer length overflow!";
                case LinkError.ProtocolMismatch:
                    return "Protocol mismatch!";
                case LinkError.CodeInvalid:
                    return "Invalid client id!";
                case LinkError.CodeConflict:
                    return "Client id conflict with current users!";
                case LinkError.CountLimited:
                    return "Client count has been limited!";
                case LinkError.GroupLimited:
                    return "Group label is too many!";
                case LinkError.QueueLimited:
                    return "Message queue full!";
                default:
                    return "Unknown error!";
            }
        }
    }
}
