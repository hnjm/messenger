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
        private bool _visible = false;
        private readonly PageProfile _profPage = new PageProfile();

        public PageFrame()
        {
            InitializeComponent();
            Loaded += _Loaded;
            Unloaded += _Unloaded;
        }

        private void _Loaded(object sender, RoutedEventArgs e)
        {
            _profPage.uiLeftFrame.Content = new PageClient();
            uiFrame.Content = _profPage;

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
            if (_visible == false)
                return;
            e.Cancel = true;
        }

        private void _Click(object sender, RoutedEventArgs e)
        {
            var tag = (e.OriginalSource as RadioButton)?.Tag as string;
            if (tag == null)
                return;
            _visible = true;

            var cur = uiMainFrame;
            if (tag == "self")
                cur.Content = new Shower();
            else if (tag == "share")
                cur.Content = new PageShare();
            else if (tag == "setting")
                cur.Content = new PageOption();
            else if (tag != "switch")
                _visible = false;

            if (_visible)
            {
                // 隐藏下层 Frame
                if (tag != "switch")
                    uiFrame.Content = null;
            }
            else
            {
                // 隐藏上层 Frame, 同时将下层 Frame 中当前聊天未读计数置 0
                uiFrame.Content = _profPage;
                uiMainFrame.Content = null;
                var sco = ProfileModule.Inscope;
                if (sco != null)
                    sco.Hint = 0;
                _profPage.uiSearchBox.Text = null;
            }

            var lef = _profPage.uiLeftFrame;
            if (tag == "user")
                lef.Content = new PageClient();
            else if (tag == "group")
                lef.Content = new PageGroups();
            else if (tag == "recent")
                lef.Content = new PageRecent();

            if (uiNavigateGrid.Width > uiNavigateGrid.MinWidth)
                uiSwitchRadio.IsChecked = false;

            uiMainBorder.Visibility = uiSwitchRadio.IsChecked == true ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
