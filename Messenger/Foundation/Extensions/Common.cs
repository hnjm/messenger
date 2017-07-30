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
        public static void Invoke(Action action, params Action[] failure)
        {
            var result = false;
            try
            {
                action.Invoke();
                result = true;
            }
            finally
            {
                if (result == false && failure != null)
                    foreach (var fun in failure)
                        fun.Invoke();
            }
        }
    }
}
