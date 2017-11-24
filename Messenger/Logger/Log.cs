using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Mikodev.Logger
{
    public static class Log
    {
        /// <summary>
        /// 日志固定前缀 (防止循环记录日志)
        /// </summary>
        internal static readonly string _prefix = $"[{nameof(Logger)}]";
        internal static Logger s_log = null;
        internal static string s_pre = null;
        internal static int s_idx = 0;

        internal static string _FilePath([CallerFilePath] string file = null) => file;

        static Log()
        {
            var pth = _FilePath();
            var idx = 0;
            if (pth == null || (idx = pth.LastIndexOf(nameof(Mikodev.Logger)) - 1) < 0)
                Environment.FailFast("日志类路径前缀无法正确解析");
            var pre = pth.Substring(0, idx);
            s_pre = pre;
            s_idx = pre.Length;
            Trace.Listeners.Add(new LogTrace());
        }

        public static void SetPath(string path)
        {
            var log = new Logger(path);
            s_log = log;
        }

        /// <summary>
        /// 记录异常和自定义消息 (如果异常为空则不记录)
        /// </summary>
        public static void Err(Exception ex, [CallerMemberName] string name = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            if (ex == null)
                return;
            if (ex is AggregateException a && a.InnerExceptions.Count == 1)
                ex = a.InnerException;
            var lbr = Environment.NewLine;

            if (file != null && file.StartsWith(s_pre))
                file = '~' + file.Substring(s_idx);

            var msg = $"[时间: {DateTime.Now:u}]" + lbr +
                $"[文件: {file}]" + lbr +
                $"[行号: {line}]" + lbr +
                $"[方法: {name}]" + lbr +
                $"{ex}" + lbr + lbr;

            Trace.Write(_prefix + Environment.NewLine + msg);
            s_log?._Write(msg).ContinueWith(_Next);
        }

        internal static void _Next(Task t)
        {
            var x = t.Exception;
            if (x == null)
                return;
            Trace.WriteLine(_prefix + Environment.NewLine + x);
        }

        internal static void _Trace(string txt)
        {
            if (string.IsNullOrEmpty(txt) || txt.Contains(_prefix))
                return;

            var lbr = Environment.NewLine;
            var msg = $"[时间: {DateTime.Now:u}]" + lbr +
                $"[来源: {nameof(Trace)}]" + lbr +
                $"{txt}" + lbr + lbr;

            s_log?._Write(msg).ContinueWith(_Next);
        }
    }
}
