using Messenger.Foundation;
using Messenger.Models;
using Messenger.Modules;
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
        /// <summary>
        /// 顶层 Frame 是否有内容
        /// </summary>
        private bool _vis = false;
        private ProfilePage _profPage = null;

        public ProfileFrame()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _profPage = new ProfilePage();
            _profPage.frameLeft.Navigate(new PageClient());
            frame.Navigate(_profPage);
            var act = (Action)delegate
                {
                    radiobuttonSwitch.IsChecked = false;
                    borderFull.Visibility = Visibility.Collapsed;
                };
            borderFull.MouseDown += (s, arg) => act.Invoke();
            borderFull.TouchDown += (s, arg) => act.Invoke();
            Packets.Receiving += ModulePacket_Receiving;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Packets.Receiving -= ModulePacket_Receiving;
        }

        /// <summary>
        /// 如果顶层 Frame 有内容 说明下层 Frame 不可见 因此消息提示也应存在
        /// </summary>
        private void ModulePacket_Receiving(object sender, GenericEventArgs<Packet> e)
        {
            if (_vis == false)
                return;
            e.Cancel = true;
        }

        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = e.OriginalSource as RadioButton;
            if (btn == null)
                return;

            _vis = true;
            if (btn == radiobuttonMyself)
                Navigate<ProfileShower>(frameFull);
            else if (btn == radiobuttonTransf)
                Navigate<Transfer>(frameFull);
            else if (btn == radiobuttonOption)
                Navigate<PageOption>(frameFull);
            else if (btn != radiobuttonSwitch)
                _vis = false;
            // 隐藏上层 Frame, 同时将下层 Frame 中当前聊天未读计数置 0
            if (_vis == false)
            {
                frameFull.Content = null;
                var sco = Profiles.Inscope;
                if (sco != null)
                    sco.Hint = 0;
            }

            if (btn == radiobuttonSingle)
                Navigate<PageClient>(_profPage.frameLeft);
            else if (btn == radiobuttonGroups)
                Navigate<PageGroups>(_profPage.frameLeft);
            else if (btn == radiobuttonRecent)
                Navigate<PageRecent>(_profPage.frameLeft);

            if (gridNavigate.Width > gridNavigate.MinWidth)
                radiobuttonSwitch.IsChecked = false;
            if (btn != radiobuttonSwitch)
                _profPage.textbox.Text = null;

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
