using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Messenger
{
    /// <summary>
    /// ProfileFrame.xaml 的交互逻辑
    /// </summary>
    public partial class PageFrame : Page
    {
        /// <summary>
        /// 顶层 Frame 是否有内容
        /// </summary>
        private bool _vis = false;
        private PageProfile _profPage = null;

        public PageFrame()
        {
            InitializeComponent();
            Loaded += _Loaded;
            Unloaded += _Unloaded;
        }

        private void _Loaded(object sender, RoutedEventArgs e)
        {
            _profPage = new PageProfile();
            _profPage.uiLeftFrame.Navigate(new PageClient());
            uiFrame.Navigate(_profPage);
            var act = (Action)delegate
            {
                uiSwitchRadio.IsChecked = false;
                uiMainBorder.Visibility = Visibility.Collapsed;
            };
            uiMainBorder.MouseDown += (s, arg) => act.Invoke();
            uiMainBorder.TouchDown += (s, arg) => act.Invoke();
            HistoryModule.Receiving += _HistoryReceiving;
        }

        private void _Unloaded(object sender, RoutedEventArgs e)
        {
            HistoryModule.Receiving -= _HistoryReceiving;
        }

        /// <summary>
        /// 如果顶层 Frame 有内容 说明下层 Frame 不可见 因此消息提示也应存在
        /// </summary>
        private void _HistoryReceiving(object sender, LinkEventArgs<Packet> e)
        {
            if (_vis == false)
                return;
            e.Cancel = true;
        }

        private void _Click(object sender, RoutedEventArgs e)
        {
            var tag = (e.OriginalSource as RadioButton)?.Tag as string;
            if (tag == null)
                return;
            _vis = true;

            if (tag == "self")
                Navigate<Shower>(uiMainFrame);
            else if (tag == "share")
                Navigate<PageShare>(uiMainFrame);
            else if (tag == "setting")
                Navigate<PageOption>(uiMainFrame);
            else if (tag != "switch")
                _vis = false;
            // 隐藏上层 Frame, 同时将下层 Frame 中当前聊天未读计数置 0
            if (_vis == false)
            {
                uiMainFrame.Content = null;
                var sco = ProfileModule.Inscope;
                if (sco != null)
                    sco.Hint = 0;
            }

            if (tag == "user")
                Navigate<PageClient>(_profPage.uiLeftFrame);
            else if (tag == "group")
                Navigate<PageGroups>(_profPage.uiLeftFrame);
            else if (tag == "recent")
                Navigate<PageRecent>(_profPage.uiLeftFrame);

            if (uiNavigateGrid.Width > uiNavigateGrid.MinWidth)
                uiSwitchRadio.IsChecked = false;
            if (tag != "switch")
                _profPage.uiSearchBox.Text = null;

            uiMainBorder.Visibility = uiSwitchRadio.IsChecked == true ? Visibility.Visible : Visibility.Hidden;
        }

        private void Navigate<Target>(Frame frame) where Target : Page, new()
        {
            if (frame.Content is Target == true)
                return;
            frame.Navigate(new Target());
        }
    }
}
