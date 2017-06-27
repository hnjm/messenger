using Messenger.Foundation;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Messenger
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public event EventHandler<KeyEventArgs> TextBoxKeyDown;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Trace.Listeners.Add(new Log($"{nameof(Messenger)}-{DateTime.Now:yyyyMMddHHmmss}.log"));
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.KeyDownEvent, new KeyEventHandler((s, arg) => TextBoxKeyDown?.Invoke(s, arg)));
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            MessageWindow.Show(null, "程序出现未处理异常, 准备退出", e.Exception);
            Shutdown(1);
        }

        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Interact.Close();
        }
    }
}
