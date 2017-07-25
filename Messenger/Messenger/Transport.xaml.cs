using Messenger.Foundation;
using Messenger.Models;
using Messenger.Modules;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Messenger
{
    /// <summary>
    /// Interaction logic for Transform.xaml
    /// </summary>
    public partial class Transfer : Page
    {
        public Transfer()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == buttonClean)
            {
                Transports.Remove();
            }
            else if (sender == buttonChange)
            {
                var dfd = new System.Windows.Forms.FolderBrowserDialog();
                if (Directory.Exists(Transports.SavePath))
                    dfd.SelectedPath = Transports.SavePath;
                if (dfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    Transports.SavePath = dfd.SelectedPath;
            }
            else if (sender == buttonOpen)
            {
                try
                {
                    if (Directory.Exists(Transports.SavePath))
                        Process.Start("explorer", "/e," + Transports.SavePath);
                }
                catch (Exception ex)
                {
                    Log.E(nameof(Transfer), ex, "启动资源管理器出错.");
                }
            }
            else if (sender == buttonStopAll)
            {
                foreach (var i in Transports.Makers)
                    i.Close();
                foreach (var i in Transports.Takers)
                    i.Close();
            }
        }

        private async void ButtonItem_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
                return;
            var con = btn.DataContext as Cargo;
            var tag = btn.Tag as string;
            if (con == null || tag == null)
                return;

            if (tag.Equals("Play"))
            {
                var obj = await Task.Run(() =>
                    {
                        try
                        {
                            con.Start();
                            return null;
                        }
                        catch (Exception ex)
                        {
                            con.Close();
                            return ex;
                        }
                    });
                if (obj != null)
                    Entrance.ShowError("接收文件失败", obj);
                return;
            }
            if (tag.Equals("Stop"))
            {
                con.Close();
                return;
            }
        }
    }
}
