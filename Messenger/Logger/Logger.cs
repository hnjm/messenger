using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Mikodev.Logger
{
    internal class Logger
    {
        internal const string _ext = ".log";

        internal string _filepath;

        internal Logger(string filepath)
        {
            _filepath = filepath + _ext;
        }

        internal async Task Write(IEnumerable<string> arr)
        {
            using (var fst = new StreamWriter(_filepath, true, Encoding.UTF8))
            {
                foreach (var i in arr)
                {
                    await fst.WriteAsync(i);
                }
            }
        }
    }
}
