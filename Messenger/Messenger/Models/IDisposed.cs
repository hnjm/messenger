using System;

namespace Messenger.Models
{
    internal interface IDisposed : IDisposable
    {
        bool IsDisposed { get; }
    }
}
