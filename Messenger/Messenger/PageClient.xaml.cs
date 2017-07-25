using Messenger.Modules;
using System.Windows;
using System.Windows.Controls;

namespace Messenger
{
    /// <summary>
    /// PageClient.xaml 的交互逻辑
    /// </summary>
    public partial class PageClient : Page
    {
        public PageClient()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            PageManager.SetProfilePage(this, listbox, Profiles.ClientList);
        }
    }
}
