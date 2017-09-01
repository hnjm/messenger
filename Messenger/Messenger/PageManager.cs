using Messenger.Models;
using Messenger.Modules;
using System.ComponentModel;
using System.Windows.Controls;

namespace Messenger
{
    public static class PageManager
    {
        public static void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var lst = e.AddedItems;
            if (lst.Count < 1)
                return;
            var itm = lst[0] as Profile;
            Profiles.SetInscope(itm);
        }

        public static void SetSelectedProfile(ListBox listbox, BindingList<Profile> list)
        {
            var idx = list.IndexOf(Profiles.Inscope);
            listbox.SelectedIndex = idx;
        }

        public static void SetProfilePage(Page page, ListBox listbox, BindingList<Profile> list)
        {
            SetSelectedProfile(listbox, list);

            var hdr = new ListChangedEventHandler((s, e) =>
            {
                if (e.ListChangedType != ListChangedType.ItemAdded)
                    return;
                SetSelectedProfile(listbox, list);
            });

            list.ListChanged += hdr;
            listbox.SelectionChanged += ListBox_SelectionChanged;

            page.Unloaded += delegate
            {
                list.ListChanged -= hdr;
                listbox.SelectionChanged -= ListBox_SelectionChanged;
            };
        }
    }
}
