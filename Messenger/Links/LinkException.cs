using System;
using System.Runtime.Serialization;

namespace Mikodev.Network
{
    [Serializable]
    public class LinkException : Exception
    {
        internal readonly LinkError _error = LinkError.None;

        public LinkException(LinkError error) : base(error.ToString()) => error = _error;

        public LinkException(LinkError error, string message) : base(message) => error = _error;

        public LinkException(LinkError error, string message, Exception inner) : base(message, inner) => error = _error;

        protected LinkException(SerializationInfo info, StreamingContext context) : base(info, context) => _error = (LinkError)info.GetValue(nameof(LinkError), typeof(LinkError));

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
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
    }
}
