using System;

namespace Mikodev.Network
{
    public class LinkEventArgs<T> : EventArgs
    {
        public T Object { get; set; } = default(T);

        public bool Cancel { get; set; } = false;

        public bool Finish { get; set; } = false;
    }
}
