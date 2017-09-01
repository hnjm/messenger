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
    public partial class Transport : Page
    {
        public Transport()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == buttonClean)
            {
                Ports.Remove();
            }
            else if (sender == buttonChange)
            {
                var dfd = new System.Windows.Forms.FolderBrowserDialog();
                if (Directory.Exists(Ports.SavePath))
                    dfd.SelectedPath = Ports.SavePath;
                if (dfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    Ports.SavePath = dfd.SelectedPath;
            }
            else if (sender == buttonOpen)
            {
                try
                {
                    if (Directory.Exists(Ports.SavePath))
                        Process.Start("explorer", "/e," + Ports.SavePath);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }
            }
            else if (sender == buttonStopAll)
            {
                foreach (var i in Ports.Makers)
                    i.Close();
                foreach (var i in Ports.Takers)
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
                try
                {
                    await Task.Run(() => con.Start());
                }
                catch (Exception ex)
                {
                    Entrance.ShowError("接收文件失败", ex);
                }
            }
            else if (tag.Equals("Stop"))
            {
                con.Close();
            }
        }
    }
}
