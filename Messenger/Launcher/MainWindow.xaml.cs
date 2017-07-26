using Messenger.Foundation;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Messenger.Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int port = 0;
        private int max = 0;
        private string name = null;
        private Exception err = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                name = textboxName.Text;
                port = int.Parse(textboxPort.Text);
                max = int.Parse(textboxMax.Text);
            }
            catch (Exception ex)
            {
                MessageWindow.Show(this, "输入信息有误, 请检查", ex);
                return;
            }

            StartAsync();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ModuleManager.Server?.Shutdown();
            ModuleManager.Broadcast?.Dispose();
        }

        private async void StartAsync()
        {
            buttonStart.IsEnabled = false;
            var srv = default(Server);
            var bro = default(Broadcast);
            var exc = default(Exception);
            exc = await Task.Run(() =>
                {
                    try
                    {
                        srv = new Server();
                        srv.Start(port, max);
                        srv.CountChanged += SetNotifyText;
                        ModuleManager.Server = srv;
                        return null;
                    }
                    catch (Exception ex)
                    {
                        return ex;
                    }
                });
            if (exc != null)
            {
                MessageWindow.Show(this, "服务器启动失败", exc);
                buttonStart.IsEnabled = true;
                return;
            }
            if (checkboxBroadcast.IsChecked == true)
            {
                err = await Task.Run(() =>
                {
                    try
                    {
                        var fuc = new Func<byte[], byte[]>((buf) =>
                            {
                                var rea = new PacketReader(buf);
                                var str = rea["protocol"].Pull<string>();
                                if (!str.Equals(Server.Protocol))
                                    return null;
                                var wtr = PacketWriter.Serialize(new Dictionary<string, object>()
                                {
                                    ["protocol"] = Server.Protocol,
                                    ["port"] = port,
                                    ["name"] = name,
                                    ["limit"] = max,
                                    ["count"] = ModuleManager.Server.Count,
                                });
                                return wtr.GetBytes();
                            });
                        bro = new Broadcast() { Function = fuc };
                        bro.Start();
                        ModuleManager.Broadcast = bro;
                        return null;
                    }
                    catch (Exception ex)
                    {
                        return ex;
                    }
                });
                if (err != null)
                    MessageWindow.Show(this, "服务器启动成功, 但广播服务启动失败", err);
            }

            Visibility = Visibility.Collapsed;
            SetNotifyText(srv, null);
            ModuleManager.NotifyIcon.Visible = true;
            ModuleManager.NotifyIcon.BalloonTipTitle = name;
            ModuleManager.NotifyIcon.ShowBalloonTip(3000);
        }

        private void SetNotifyText(object sender, EventArgs e)
        {
            var srv = sender as Server;
            if (srv is null)
                return;
            var stb = new StringBuilder()
                .AppendFormat("名称: {0}", name)
                .AppendLine()
                .AppendFormat("端口: {0}", port)
                .AppendLine()
                .AppendFormat("连接: {0} / {1}", srv.Count, max)
                .AppendLine()
                .AppendFormat("广播: {0}", err == null ? "启用" : "禁用");
            var str = stb.ToString();
            Dispatcher.Invoke(() => ModuleManager.NotifyIcon.Text = str);
        }
    }
}
