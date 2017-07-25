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
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var val = Settings.UseCtrlEnter;
            if (val == false)
                radioEnter.IsChecked = true;
            else
                radioCtrlEnter.IsChecked = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var btn = e.OriginalSource as Button;
            if (btn == null)
                return;
            if (btn == buttonExit)
            {
                Interact.Close();
                Application.Current.MainWindow.Close();
            }
            else if (btn == buttonOut)
            {
                var mai = Application.Current.MainWindow as MainWindow;
                if (mai == null)
                    return;
                Interact.Close();
                Profiles.SetInscope(null);
                mai.frame.Navigate(new Connection());
            }
        }

        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            var src = e.OriginalSource;
            if (src == radioEnter)
                Settings.UseCtrlEnter = false;
            else if (src == radioCtrlEnter)
                Settings.UseCtrlEnter = true;
        }
    }
}
