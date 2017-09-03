using System;
using System.Runtime.InteropServices;

namespace Messenger.Extensions
{
    internal static class NativeMethods
    {
        /// <summary>
        /// 任务栏闪烁
        /// </summary>
        [DllImport("user32.dll", EntryPoint = "FlashWindow")]
        internal static extern bool FlashWindow(IntPtr handle, bool invert);
    }
}
