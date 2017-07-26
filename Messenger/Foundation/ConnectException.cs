using System;

namespace Messenger.Foundation
{
    /// <summary>
    /// 连接中出现的异常
    /// </summary>
    [Serializable]
    public sealed class ConnectException : ApplicationException
    {
        private ErrorCode _error = ErrorCode.None;

        /// <summary>
        /// 获取错误代码
        /// </summary>
        public ErrorCode ErrorCode => _error;
        /// <summary>
        /// 获取错误信息
        /// </summary>
        public override string Message => GetMessage(_error);

        /// <summary>
        /// 以指定错误代码初始化
        /// </summary>
        /// <param name="code">错误代码</param>
        public ConnectException(ErrorCode code) => _error = code;

        /// <summary>
        /// 提供预定义的错误提示
        /// </summary>
        /// <param name="code">错误代码</param>
        /// <returns></returns>
        public static string GetMessage(ErrorCode code)
        {
            switch (code)
            {
                case ErrorCode.Success:
                    return "操作成功完成.";
                case ErrorCode.Filled:
                    return "服务器已满.";
                case ErrorCode.Invalid:
                    return "当前编号不在容许范围内";
                case ErrorCode.Conflict:
                    return "当前编号与服务器现有用户冲突.";
                case ErrorCode.Shutdown:
                    return "服务器已关闭或不再接收新连接.";
                case ErrorCode.InnerError:
                    return "内部错误.";
                default:
                    return "未定义.";
            }
        }
    }
}
