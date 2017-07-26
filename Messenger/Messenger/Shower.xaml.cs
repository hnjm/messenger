using Messenger.Foundation;
using Messenger.Modules;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Messenger
{
    /// <summary>
    /// Interaction logic for ProfileShower.xaml
    /// </summary>
    public partial class ProfileShower : Page
    {
        public ProfileShower()
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
                Profiles.Current.Name = textboxName.Text;
                Profiles.Current.Text = textboxText.Text;
                // Interact.Enqueue(Server.ID, PacketGenre.UserProfile, Profiles.Current);
                Posters.UserProfile(Server.ID);
            }
            else if (src == buttonImage)
            {
                var ofd = new System.Windows.Forms.OpenFileDialog() { Filter = "位图文件|*.bmp;*.png;*.jpg" };
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
                try
                {
                    var buf = Caches.ImageSquare(ofd.FileName);
                    var str = Caches.SetBuffer(buf, true);
                    Profiles.ImageSource = ofd.FileName;
                    Profiles.ImageBuffer = buf;
                    Profiles.Current.Image = str;
                    // Interact.Enqueue(Server.ID, PacketGenre.UserImage, buf);
                    Posters.UserProfile(Server.ID);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }
            }
        }
    }
}
