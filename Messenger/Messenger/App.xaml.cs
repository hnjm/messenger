using Messenger.Modules;
using Mikodev.Logger;
using System;
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

            Log.SetPath(nameof(Messenger));
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.KeyDownEvent, new KeyEventHandler((s, arg) => TextBoxKeyDown?.Invoke(s, arg)));

            DispatcherUnhandledException += (s, arg) =>
            {
                arg.Handled = true;
                Fallback.Show(null, "Unhandled Exception", arg.Exception);
                Shutdown(1);
            };

            SessionEnding += (s, arg) => LinkModule.Shutdown();
            Exit += (s, _) => Framework.Close();

            Framework.Start();
        }
    }
}
