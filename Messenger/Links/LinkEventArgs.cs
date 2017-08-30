using System;

namespace Mikodev.Network
{
    public class LinkEventArgs<T> : EventArgs
    {
        public T Record { get; set; } = default(T);

        public bool Cancel { get; set; } = false;

        public bool Finish { get; set; } = false;

        public object Source { get; set; } = null;
    }
}
