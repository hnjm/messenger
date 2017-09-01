using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Mikodev.Network
{
    public class LinkLogger : TraceListener
    {
        internal readonly string _path = null;

        internal async void _Flush(params string[] message)
        {
            var wtr = default(StreamWriter);

            await Task.Run(async () =>
            {
                wtr = new StreamWriter(_path, true, Encoding.UTF8);
                var stb = new StringBuilder();
                stb.AppendFormat("{0:u}", DateTime.Now);
                stb.AppendLine();
                foreach (var i in message)
                    stb.Append(i);
                await wtr.WriteLineAsync(stb.ToString());
                await wtr.FlushAsync();
            })
            .ContinueWith(t =>
            {
                wtr?.Dispose();
            });
        }

        public LinkLogger(string path) => _path = path;

        public override void Write(string message) => _Flush(message);

        public override void WriteLine(string message) => _Flush(message, Environment.NewLine);
    }
}
