using System;
using System.Windows;
using System.Windows.Controls;

namespace Messenger
{
    /// <summary>
    /// ProfileFrame.xaml 的交互逻辑
    /// </summary>
    public partial class ProfileFrame : Page
    {
        private ProfilePage profilePage = null;

        public ProfileFrame()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            profilePage = new ProfilePage();
            profilePage.frameLeft.Navigate(new PageClient());
            frame.Navigate(profilePage);
            var act = (Action)delegate
                {
                    radiobuttonSwitch.IsChecked = false;
                    borderFull.Visibility = Visibility.Collapsed;
                };
            borderFull.MouseDown += (s, arg) => act.Invoke();
            borderFull.TouchDown += (s, arg) => act.Invoke();
        }

        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = e.OriginalSource as RadioButton;
            if (btn == null)
                return;

            if (btn == radiobuttonMyself)
                Navigate<ProfileShower>(frameFull);
            else if (btn == radiobuttonTransf)
                Navigate<Transfer>(frameFull);
            else if (btn == radiobuttonOption)
                Navigate<PageOption>(frameFull);
            else if (btn != radiobuttonSwitch)
                frameFull.Content = null;

            if (btn == radiobuttonSingle)
                Navigate<PageClient>(profilePage.frameLeft);
            if (btn == radiobuttonGroups)
                Navigate<PageGroups>(profilePage.frameLeft);
            if (btn == radiobuttonRecent)
                Navigate<PageRecent>(profilePage.frameLeft);

            if (gridNavigate.Width > gridNavigate.MinWidth)
                radiobuttonSwitch.IsChecked = false;
            if (btn != radiobuttonSwitch)
                profilePage.textbox.Text = null;

            borderFull.Visibility = radiobuttonSwitch.IsChecked == true ? Visibility.Visible : Visibility.Hidden;
        }

        private void Navigate<T>(Frame frame) where T : Page, new()
        {
            if (frame.Content is T == true)
                return;
            frame.Navigate(new T());
        }
    }
}
