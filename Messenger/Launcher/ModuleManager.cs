using Messenger.Foundation;
using System;
using System.Windows.Forms;

namespace Messenger.Launcher
{
    class ModuleManager : IDisposable
    {
        private Server _server = new Server();
        private Broadcast _broadcast = new Broadcast();
        private NotifyIcon _notifyicon = null;
        private ToolStripMenuItem _menuShudown = null;
        private ToolStripMenuItem _menuRestart = null;

        private static ModuleManager instance = new ModuleManager();

        public static NotifyIcon NotifyIcon => instance._notifyicon;
        public static Server Server { get => instance._server; set => instance._server = value; }
        public static Broadcast Broadcast { get => instance._broadcast; set => instance._broadcast = value; }

        private ModuleManager()
        {
            var not = new NotifyIcon();
            var str = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Prompt.ico"));
            not.Icon = new System.Drawing.Icon(str.Stream);
            not.BalloonTipText = "服务器正在运行";

            _menuShudown = new ToolStripMenuItem();
            _menuShudown.Text = "退出";
            _menuShudown.Click += MenuItem_Click;
            _menuShudown.ShortcutKeys = Keys.Control | Keys.E;
            _menuRestart = new ToolStripMenuItem();
            _menuRestart.Text = "重启";
            _menuRestart.Click += MenuItem_Click;
            _menuRestart.ShortcutKeys = Keys.Control | Keys.R;

            var con = new ContextMenuStrip();
            con.Items.Add(_menuRestart);
            con.Items.Add(new ToolStripSeparator());
            con.Items.Add(_menuShudown);
            not.ContextMenuStrip = con;

            _notifyicon = not;
        }

        private void MenuItem_Click(object sender, EventArgs e)
        {
            _notifyicon.Visible = false;
            if (sender == _menuRestart)
                Application.Restart();
            System.Windows.Application.Current.Shutdown();
        }

        public void Dispose()
        {
            _server?.Dispose();
            _broadcast?.Dispose();
            _menuRestart?.Dispose();
            _menuShudown?.Dispose();
        }
    }
}
