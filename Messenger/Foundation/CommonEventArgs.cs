using System;

namespace Messenger.Foundation
{
    /// <summary>
    /// 泛型事件参数
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class CommonEventArgs<T> : EventArgs
    {
        /// <summary>
        /// 封装数据
        /// </summary>
        public T Object { get; set; } = default(T);
        /// <summary>
        /// 取消标志
        /// </summary>
        public bool Cancel { get; set; } = false;
        /// <summary>
        /// 处理标志
        /// </summary>
        public bool Finish { get; set; } = false;
        /// <summary>
        /// 事件元对象
        /// </summary>
        public object Source { get; set; } = null;
    }
}
