using System;
using System.Threading.Tasks;

namespace Messenger.Foundation
{
    /// <summary>
    /// 文件传输基类
    /// </summary>
    public abstract class Transport : Manageable
    {
        protected long _length = 0;
        protected long _position = 0;
        protected string _name = null;
        protected Guid _key = Guid.NewGuid();
        protected Exception _exception = null;
        protected TransportStatus _status = TransportStatus.默认;

        /// <summary>
        /// 文件传输标识
        /// </summary>
        public Guid Key => _key;
        /// <summary>
        /// 文件大小
        /// </summary>
        public long Length => _length;
        /// <summary>
        /// 当前位置
        /// </summary>
        public long Position => _position;
        /// <summary>
        /// 文件名
        /// </summary>
        public string Name => _name;
        /// <summary>
        /// 当前文件传输状态
        /// </summary>
        public TransportStatus Status => _status;
        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception => _exception;
        /// <summary>
        /// 传输启动事件 (后台执行)
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// 对象释放事件 (后台执行)
        /// </summary>
        public event EventHandler Disposed;

        /// <summary>
        /// 触发 <see cref="Started"/> 事件 (后台执行)
        /// </summary>
        protected void _OnStarted() => Task.Run(() => Started?.Invoke(this, new EventArgs()));

        /// <summary>
        /// 触发 <see cref="Disposed"/> 事件 (后台执行)
        /// </summary>
        protected void _OnDisposed() => Task.Run(() => Disposed?.Invoke(this, new EventArgs()));
    }
}
