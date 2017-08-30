using System;

namespace Messenger.Foundation
{
    public class LinkOldEventArgs<T> : EventArgs
    {
        public T Record { get; set; } = default(T);

        public bool Cancel { get; set; } = false;

        public bool Finish { get; set; } = false;

        public object Source { get; set; } = null;
    }
}
