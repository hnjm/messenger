using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Mikodev.Logger
{
    public static class Log
    {
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
        }

        public static void SetPath(string path)
        {
            var log = new Logger(path);
            s_log = log;
        }

        public static void Err(Exception ex, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0, [CallerMemberName] string name = null)
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

            Trace.Write(msg);
            s_log?._Write(msg).ContinueWith(_Next);
        }

        internal static void _Next(Task t)
        {
            var x = t.Exception;
            if (x == null)
                return;
            Trace.WriteLine(x);
        }

        internal static void _Trace(string txt)
        {
            if (txt == null || txt.StartsWith("["))
                return;

            var lbr = Environment.NewLine;
            var msg = $"[时间: {DateTime.Now:u}]" + lbr +
                $"[来源: {nameof(Trace)}]" + lbr +
                $"{txt}" + lbr + lbr;

            s_log?._Write(msg).ContinueWith(_Next);
        }
    }
}
