using System;
using System.Diagnostics;
using System.IO;

namespace Messenger.Foundation
{
    /// <summary>
    /// 日志类 (保存位置为系统临时文件夹)
    /// </summary>
    public class Log : TraceListener
    {
        private string _path = null;

        private void _BufferWriter(params string[] message)
        {
            var fil = default(FileStream);
            var fsw = default(StreamWriter);
            try
            {
                fil = new FileStream(_path, FileMode.Append, FileAccess.Write);
                fsw = new StreamWriter(fil);
                foreach (var m in message)
                    fsw.Write(m);
                return;
            }
            catch { }
            finally
            {
                fsw?.Dispose();
                fil?.Dispose();
            }
        }

        public Log(string filename) => _path = Path.Combine(Path.GetTempPath(), filename);

        public override void Write(string message) => _BufferWriter(message);

        public override void WriteLine(string message) => _BufferWriter(message, Environment.NewLine);

        /// <summary>
        /// 记录类名 自定义消息(可选) 和异常信息 (后接新行)
        /// </summary>
        /// <param name="title">类名</param>
        /// <param name="except">异常信息</param>
        /// <param name="message">自定义消息</param>
        public static void E(string title, Exception except, string message = null)
        {
            var str = string.Format("[{0:yyyy-MM-dd HH:mm:ss} {1}] {2}{3}{4}{3}", DateTime.Now, title, message ?? except.Message, Environment.NewLine, except.ToString());
            Trace.WriteLine(str);
        }
    }
}
