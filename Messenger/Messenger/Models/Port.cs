using System;
using System.Threading.Tasks;

namespace Messenger.Models
{
    /// <summary>
    /// 文件传输基类
    /// </summary>
    public abstract class Port
    {
        protected bool _started = false;
        protected bool _disposed = false;
        protected object _loc = new object();

        protected long _length = 0;
        protected long _position = 0;
        protected string _name = null;
        protected Guid _key = Guid.NewGuid();
        protected Exception _exception = null;
        protected PortStatus _status = PortStatus.默认;

        public Guid Key => _key;

        public long Length => _length;

        public long Position => _position;

        public string Name => _name;

        public PortStatus Status => _status;

        public Exception Exception => _exception;

        public event EventHandler Started;

        public event EventHandler Disposed;

        protected void _EmitStarted() => Task.Run(() => Started?.Invoke(this, new EventArgs()));

        protected void _EmitDisposed() => Task.Run(() => Disposed?.Invoke(this, new EventArgs()));

        public virtual bool CanStart => IsStarted == false && IsDisposed == false;

        public virtual bool IsStarted => _started;

        public virtual bool IsDisposed => _disposed;

        public void Dispose() { lock (_loc) { _Dispose(); } }

        protected abstract void _Dispose();
    }
}
