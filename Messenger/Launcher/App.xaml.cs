using Messenger.Foundation;
using System;
using System.Diagnostics;
using System.Windows;

namespace Messenger.Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Trace.Listeners.Add(new Log($"{nameof(Launcher)}-{ DateTime.Now:yyyyMMddHHmmss}.log"));
        }
    }
}
