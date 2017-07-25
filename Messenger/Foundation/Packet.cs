using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Messenger.Foundation
{
    /// <summary>
    /// 消息类型
    /// </summary>
    public enum PacketGenre : long
    {
        /// <summary>
        /// 默认值
        /// </summary>
        None = 0,
        Text = 1,
        Image = 2,
        Binary = 3,

        /// <summary>
        /// 连接标志
        /// </summary>
        Link = 1 << 8,
        LinkShutdown,
        /// <summary>
        /// 用户信息标志
        /// </summary>
        User = 2 << 8,
        UserIDs,
        UserImage = Raw | User | Image,
        UserProfile = User + 3,
        UserRequest,
        /// <summary>
        /// 用户监听的组列表
        /// </summary>
        UserGroups,
        /// <summary>
        /// 文件标志
        /// </summary>
        File = 3 << 8,
        FileInfo = Raw | File | Binary,
        /// <summary>
        /// 消息标志
        /// </summary>
        Message = 4 << 8,
        MessageText = Message | Text,
        MessageImage = Raw | Message | Image,

        /// <summary>
        /// RAW 格式 (没有此标识则默认采用 XML 序列化)
        /// </summary>
        Raw = 1 << 31,
    }

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

    /// <summary>
    /// 数据报头接口
    /// </summary>
    public interface IPacketHeader
    {
        int Target { get; }
        int Source { get; }
        PacketGenre Genre { get; }
    }

    /// <summary>
    /// 数据报头
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader : IPacketHeader
    {
        internal int _target;
        internal int _source;
        internal PacketGenre _genre;

        public int Target => _target;
        public int Source => _source;
        public PacketGenre Genre => _genre;

        public PacketHeader(int target, int source, PacketGenre genre)
        {
            _target = target;
            _source = source;
            _genre = genre;
        }

        /// <summary>
        /// 获取结构体大小
        /// </summary>
        public static int GetLength() => Marshal.SizeOf(typeof(PacketHeader));
    }

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

    /// <summary>
    /// 信息接收事件参数
    /// </summary>
    public class PacketEventArgs : EventArgs, IPacketHeader, IDisposable
    {
        private byte[] _buffer = null;
        private PacketHeader _header;
        private MemoryStream _memstr = null;

        public int Target => _header._target;
        public int Source => _header._source;
        public PacketGenre Genre => _header._genre;

        /// <summary>
        /// 源字节流 (不应当修改其中的数据)
        /// </summary>
        public byte[] Buffer => _buffer;
        /// <summary>
        /// 消息数据 (不含消息报头)
        /// </summary>
        public MemoryStream Stream
        {
            get
            {
                if (_memstr == null)
                {
                    var len = PacketHeader.GetLength();
                    _memstr = new MemoryStream(_buffer, len, _buffer.Length - len);
                }
                return _memstr;
            }
        }

        /// <summary>
        /// 创建一个事件参数
        /// </summary>
        /// <param name="buf">消息字节数组</param>
        public PacketEventArgs(byte[] buf)
        {
            _buffer = buf;
            _header = buf.ToStruct<PacketHeader>();
        }

        public void Dispose()
        {
            _memstr?.Dispose();
            _memstr = null;
        }
    }
}
