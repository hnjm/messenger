using Messenger.Modules;
using Mikodev.Network;
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
            PageManager.SetProfilePage(this, listbox, ProfileModule.GroupsList);
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
            else if (btn == buttonApply && string.Equals(textboxEdit.Text, ProfileModule.GroupLabels) == false)
            {
                var res = ProfileModule.SetGroupLabels(textboxEdit.Text);
                if (res == false)
                    Entrance.ShowError($"最多允许 {Links.GroupLabelLimit} 个群组标签", null);
                return;
            }
        }
    }
}
