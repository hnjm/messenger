using System;
using System.Threading.Tasks;

namespace Messenger.Foundation.Extensions
{
    public static partial class Extension
    {
        public static void TimeoutInvoke(this Action action, int timeout)
        {
            var tsk = Task.Run(action);
            if (tsk.Wait(timeout) == false)
                throw new TimeoutException();
            return;
        }

        /// <summary>
        /// Run an delegate with try-finally, run next delegate if fail
        /// </summary>
        public static void Invoke(Action act, Action fail)
        {
            var tag = false;
            try
            {
                act.Invoke();
                tag = true;
            }
            finally
            {
                if (tag == false)
                    fail.Invoke();
            }
        }
    }
}
