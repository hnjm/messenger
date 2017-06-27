using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Messenger
{
    /// <summary>
    /// ProfilePage.xaml 的交互逻辑
    /// </summary>
    public partial class ProfilePage : Page
    {
        public ProfilePage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ModuleProfile.InscopeChanged += ModuleProfile_InscopeChanged;
            listbox.SelectionChanged += PageManager.ListBox_SelectionChanged;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            ModuleProfile.InscopeChanged -= ModuleProfile_InscopeChanged;
            listbox.SelectionChanged -= PageManager.ListBox_SelectionChanged;
        }

        private void ModuleProfile_InscopeChanged(object sender, EventArgs e)
        {
            var pag = frameRight.Content as Chatter;
            if (pag == null || pag.Profile.ID != ModuleProfile.Inscope.ID)
                frameRight.Navigate(new Chatter());
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (e.OriginalSource != textbox)
                return;
            var lst = listbox.ItemsSource as ICollection<Profile>;
            lst?.Clear();
            if (string.IsNullOrWhiteSpace(textbox.Text) == true)
            {
                listbox.ItemsSource = null;
                grid.Visibility = Visibility.Collapsed;
            }
            else
            {
                var txt = textbox.Text.ToLower();
                var val = (from i in ModuleProfile.ClientList.Union(ModuleProfile.GroupsList).Union(ModuleProfile.RecentList)
                           where i.Name?.ToLower()?.Contains(txt) == true || i.Text?.ToLower()?.Contains(txt) == true
                           select i).ToList();
                var idx = val.IndexOf(ModuleProfile.Inscope);
                listbox.ItemsSource = val;
                listbox.SelectedIndex = idx;
                grid.Visibility = Visibility.Visible;
            }
        }
    }
}
