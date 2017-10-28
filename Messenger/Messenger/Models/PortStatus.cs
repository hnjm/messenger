namespace Messenger.Models
{
    /// <summary>
    /// 文件传输状态
    /// </summary>
    public enum PortStatus : int
    {
        默认 = 0,
        等待 = 1,
        运行 = 2,
        暂停 = 4,
        中断 = 8 | 终止,
        取消 = 16 | 终止,
        成功 = 32 | 终止,

        终止 = 1 << 31,
    }
}
