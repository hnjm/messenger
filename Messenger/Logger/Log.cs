using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mikodev.Logger
{
    public static class Log
    {
        private const int _MaxQueueLength = 256;

        /// <summary>
        /// 日志固定前缀 (防止循环记录日志)
        /// </summary>
        internal static readonly string _prefix = $"[{nameof(Logger)}]";
        internal static readonly Queue<string> s_queue = new Queue<string>();
        internal static int s_trace = 0;
        internal static Logger s_log = null;

        public static void SetPath(string path)
        {
            if (Interlocked.CompareExchange(ref s_trace, 1, 0) == 0)
                Trace.Listeners.Add(new LogTrace());
            var log = new Logger(path);
            s_log = log;
            Task.Run(_Monitor);
        }

        private static async Task _Monitor()
        {
            while (true)
            {
                var itr = default(IEnumerable<string>);
                lock (s_queue)
                {
                    itr = s_queue.ToArray();
                    s_queue.Clear();
                }

                try
                {
                    await s_log.Write(itr);
                }
                catch (Exception ex)
                {
                    _InternalError(ex.ToString());
                }
            }
        }

        private static void _Enqueue(string msg)
        {
            lock (s_queue)
            {
                var len = s_queue.Count + 1;
                var sub = len - _MaxQueueLength;
                if (sub > 0)
                {
                    for (int i = 0; i < len; i++)
                        s_queue.Dequeue();
                    _InternalError("Log queue full!");
                }
                s_queue.Enqueue(msg);
            }
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
            _Enqueue(msg);
        }

        internal static void _InternalError(string msg)
        {
            Trace.WriteLine(_prefix + Environment.NewLine + msg);
        }

        internal static void _Trace(string txt)
        {
            if (string.IsNullOrEmpty(txt) || txt.StartsWith(_prefix))
                return;

            var lbr = Environment.NewLine;
            var msg = $"[时间: {DateTime.Now:u}]" + lbr +
                $"[来源: {nameof(Trace)}]" + lbr +
                $"{txt}" + lbr + lbr;
            _Enqueue(msg);
        }
    }
}
