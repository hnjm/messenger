using System.Windows;
using System.Windows.Controls;

namespace Messenger
{
    /// <summary>
    /// PageGroups.xaml 的交互逻辑
    /// </summary>
    public partial class PageGroups : Page
    {
        public PageGroups()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            PageManager.SetProfilePage(this, listbox, ModuleProfile.GroupsList);
        }
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var btn = e.OriginalSource as Button;
            if (btn == null)
                return;
            if (btn == buttonEdit)
            {
                var vis = gridEdit.Visibility;
                gridEdit.Visibility = vis == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (btn == buttonApply && string.Equals(textboxEdit.Text, ModuleProfile.GroupLabels) == false)
            {
                ModuleProfile.SetGroupLabels(textboxEdit.Text);
            }
        }
    }
}
