using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
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
        internal static int s_trace = 0;

        public static void SetPath(string path)
        {
            if (Interlocked.CompareExchange(ref s_trace, 1, 0) == 0)
                Trace.Listeners.Add(new LogTrace());
            var log = new Logger(path);
            s_log = log;
        }

        /// <summary>
        /// 记录异常 (如果异常为空则不记录)
        /// </summary>
        public static void Error(Exception err, [CallerMemberName] string name = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            if (err == null)
                return;
            while (err is AggregateException a && a.InnerExceptions?.Count == 1 && a.InnerException is Exception val)
                err = val;
            Info(err.ToString(), name, file, line);
        }

        /// <summary>
        /// 记录自定义消息 (如果异常为空则不记录)
        /// </summary>
        public static void Info(string message, [CallerMemberName] string name = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            if (message == null)
                return;
            var lbr = Environment.NewLine;

            var msg = $"[时间: {DateTime.Now:u}]" + lbr +
                $"[文件: {file}]" + lbr +
                $"[行号: {line}]" + lbr +
                $"[方法: {name}]" + lbr +
                $"{message}" + lbr + lbr;

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
            if (string.IsNullOrEmpty(txt) || txt.StartsWith(_prefix))
                return;

            var lbr = Environment.NewLine;
            var msg = $"[时间: {DateTime.Now:u}]" + lbr +
                $"[来源: {nameof(Trace)}]" + lbr +
                $"{txt}" + lbr + lbr;

            s_log?._Write(msg).ContinueWith(_Next);
        }
    }
}
