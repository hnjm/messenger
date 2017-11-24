using Messenger.Models;
using Messenger.Modules;
using Mikodev.Logger;
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
    public partial class PageShare : Page
    {
        public PageShare()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var src = e.OriginalSource;
            if (src == buttonClean)
            {
                Ports.Remove();
            }
            else if (src == buttonChange)
            {
                var dfd = new System.Windows.Forms.FolderBrowserDialog();
                if (Directory.Exists(Ports.SavePath))
                    dfd.SelectedPath = Ports.SavePath;
                if (dfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    Ports.SavePath = dfd.SelectedPath;
            }
            else if (src == buttonOpen)
            {
                Task.Run(() =>
                {
                    if (Directory.Exists(Ports.SavePath) == false)
                        return;
                    Process.Start("explorer", "/e," + Ports.SavePath);
                })
                .ContinueWith(task =>
                {
                    Log.Err(task.Exception);
                });
            }
            else if (src == buttonStopAll)
            {
                Ports.Close();
            }
        }

        private void ButtonItem_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
                return;
            var con = btn.DataContext as Cargo;
            var tag = btn.Tag as string;
            if (con == null || tag == null)
                return;

            if (tag.Equals("Play"))
                con.Start();
            else if (tag.Equals("Stop"))
                con.Close();
            return;
        }
    }
}
