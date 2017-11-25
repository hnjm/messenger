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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var src = e.OriginalSource as Button;
            if (src == null)
                return;
            if (src == buttonApply)
            {
                ProfileModule.Current.Name = textboxName.Text;
                ProfileModule.Current.Text = textboxText.Text;
                PostModule.UserProfile(Links.ID);
            }
            else if (src == buttonImage)
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
