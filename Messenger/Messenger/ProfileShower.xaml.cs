using Messenger.Foundation;
using System;
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
                ModuleProfile.Current.Name = textboxName.Text;
                ModuleProfile.Current.Text = textboxText.Text;
                Interact.Enqueue(Server.ID, PacketGenre.UserProfile, ModuleProfile.Current);
            }
            else if (src == buttonImage)
            {
                var ofd = new System.Windows.Forms.OpenFileDialog() { Filter = "位图文件|*.bmp;*.png;*.jpg" };
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
                try
                {
                    var buf = Cache.ImageSquare(ofd.FileName);
                    var str = Cache.SetBuffer(buf, true);
                    ModuleProfile.ImageSource = ofd.FileName;
                    ModuleProfile.ImageBuffer = buf;
                    ModuleProfile.Current.Image = str;
                    Interact.Enqueue(Server.ID, PacketGenre.UserImage, buf);
                }
                catch (Exception ex)
                {
                    Log.E(nameof(ProfileShower), ex, "设置头像出错.");
                }
            }
        }
    }
}
