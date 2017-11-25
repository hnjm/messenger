using Messenger.Modules;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Messenger
{
    /// <summary>
    /// Interaction logic for Shower.xaml
    /// </summary>
    public partial class Shower : Page
    {
        public Shower()
        {
            InitializeComponent();
        }

        private void _Click(object sender, RoutedEventArgs e)
        {
            var tag = (e.OriginalSource as Button)?.Tag as string;
            if (tag == null)
                return;
            if (tag == "apply")
            {
                ProfileModule.Current.Name = uiNameBox.Text;
                ProfileModule.Current.Text = uiSignBox.Text;
                PostModule.UserProfile(Links.ID);
            }
            else if (tag == "image")
            {
                var ofd = new System.Windows.Forms.OpenFileDialog() { Filter = "位图文件|*.bmp;*.png;*.jpg" };
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
                try
                {
                    var buf = CacheModule.ImageSquare(ofd.FileName);
                    var str = CacheModule.SetBuffer(buf, true);
                    ProfileModule.ImageSource = ofd.FileName;
                    ProfileModule.ImageBuffer = buf;
                    ProfileModule.Current.Image = str;
                    PostModule.UserProfile(Links.ID);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }
    }
}
