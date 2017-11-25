using Messenger.Modules;
using System.Windows;
using System.Windows.Controls;

namespace Messenger
{
    /// <summary>
    /// PageOption.xaml 的交互逻辑
    /// </summary>
    public partial class PageOption : Page
    {
        public PageOption()
        {
            InitializeComponent();
            Loaded += _Loaded;
        }

        private void _Loaded(object sender, RoutedEventArgs e)
        {
            var val = SettingModule.UseCtrlEnter;
            if (val == false)
                uiEnterRadio.IsChecked = true;
            else
                uiCtrlEnterRadio.IsChecked = true;
        }

        private void _ButtonClick(object sender, RoutedEventArgs e)
        {
            var tag = (e.OriginalSource as Button)?.Tag as string;
            if (tag == null)
                return;
            if (tag == "exit")
            {
                LinkModule.Shutdown();
                Application.Current.MainWindow.Close();
            }
            else if (tag == "out")
            {
                var mai = Application.Current.MainWindow as Entrance;
                if (mai == null)
                    return;
                LinkModule.Shutdown();
                ProfileModule.SetInscope(null);
                mai.frame.Navigate(new Connection());
            }
        }

        private void _RadioClick(object sender, RoutedEventArgs e)
        {
            var src = e.OriginalSource;
            if (src == uiEnterRadio)
                SettingModule.UseCtrlEnter = false;
            else if (src == uiCtrlEnterRadio)
                SettingModule.UseCtrlEnter = true;
        }
    }
}
