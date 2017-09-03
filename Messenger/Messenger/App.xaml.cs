using Messenger.Modules;
using Mikodev.Network;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Messenger
{
    public partial class App : Application
    {
        public event EventHandler<KeyEventArgs> TextBoxKeyDown;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var lis = new LinkLogger($"{nameof(Messenger)}.log");
            Trace.Listeners.Add(lis);
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.KeyDownEvent, new KeyEventHandler((s, arg) => TextBoxKeyDown?.Invoke(s, arg)));

            DispatcherUnhandledException += (s, arg) =>
            {
                arg.Handled = true;
                Fallback.Show(null, "Unhandled Exception", arg.Exception);
                Shutdown(1);
            };

            SessionEnding += (s, arg) =>
            {
                Linkers.Shutdown();
            };
        }
    }
}
