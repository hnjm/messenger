using System;
using System.Runtime.InteropServices;

namespace Messenger.Extensions
{
    internal static class NativeMethods
    {
        public const int FLASHW_CAPTION = 0x1;
        public const int GWL_STYLE = -16;
        public const int WM_NCACTIVATE = 0x86;
        public const int WS_SYSMENU = 0x80000;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// 任务栏闪烁
        /// </summary>
        [DllImport("user32.dll", EntryPoint = "FlashWindow")]
        internal static extern bool FlashWindow(IntPtr handle, bool invert);
    }
}
