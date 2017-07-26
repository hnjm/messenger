using System;
using System.Threading.Tasks;

namespace Messenger.Foundation.Extensions
{
    /// <summary>
    /// 静态扩展类
    /// </summary>
    public static partial class Extension
    {
        /// <summary>
        /// 在后台线程上执行操作 并在超时或内部错误时抛出异常 (请手动终止线程)
        /// </summary>
        /// <param name="action">待执行操作</param>
        /// <param name="timeout">超时时长 (毫秒)</param>
        /// <exception cref="ApplicationException"></exception>
        /// <exception cref="TimeoutException"></exception>
        public static void TimeoutInvoke(this Action action, int timeout)
        {
            var tsk = Task.Run(action);
            if (tsk.Wait(timeout) == false)
                throw new TimeoutException();
            else if (tsk.IsCompleted == true && tsk.Exception != null)
                throw new ApplicationException($"Exception was thrown by the delegate.", tsk.Exception);
            else return;
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
