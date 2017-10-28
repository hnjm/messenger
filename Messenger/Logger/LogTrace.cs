using System;
using System.Diagnostics;

namespace Mikodev.Logger
{
    internal class LogTrace : TraceListener
    {
        public override void Write(string message)
        {
            throw new NotImplementedException();
        }

        public override void WriteLine(string message)
        {
            throw new NotImplementedException();
        }
    }
}
