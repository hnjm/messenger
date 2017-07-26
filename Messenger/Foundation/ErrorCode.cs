namespace Messenger.Foundation
{
    /// <summary>
    /// 错误代码
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// 默认值
        /// </summary>
        None = 0,
        /// <summary>
        /// 成功完成操作
        /// </summary>
        Success,
        /// <summary>
        /// 服务器已满
        /// </summary>
        Filled,
        /// <summary>
        /// ID 不在容许范围内
        /// </summary>
        Invalid,
        /// <summary>
        /// ID 存在冲突
        /// </summary>
        Conflict,
        /// <summary>
        /// 服务器已关闭或不再接收新连接
        /// </summary>
        Shutdown,
        /// <summary>
        /// 内部错误
        /// </summary>
        InnerError,
    }
}
