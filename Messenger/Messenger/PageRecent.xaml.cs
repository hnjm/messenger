using Messenger.Modules;
using System.Windows;
using System.Windows.Controls;

namespace Messenger
{
    /// <summary>
    /// PageRecent.xaml 的交互逻辑
    /// </summary>
    public partial class PageRecent : Page
    {
        public PageRecent()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            PageManager.SetProfilePage(this, listbox, ProfileModule.RecentList);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var btn = e.OriginalSource as Button;
            if (btn == null)
                return;
            if (btn == buttonClear)
            {
                ProfileModule.RecentList.Clear();
                return;
            }
        }
    }
}
