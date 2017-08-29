using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Mikodev.Network
{
    [Serializable]
    public class LinkException : Exception
    {
        internal readonly LinkError _error = LinkError.None;

        public LinkException(LinkError error) => error = _error;

        public LinkException(LinkError error, string message) : base(message) => error = _error;

        public LinkException(LinkError error, string message, Exception inner) : base(message, inner) => error = _error;

        protected LinkException(SerializationInfo info, StreamingContext context) : base(info, context) => _error = (LinkError)info.GetValue(nameof(LinkError), typeof(LinkError));
    }
}
