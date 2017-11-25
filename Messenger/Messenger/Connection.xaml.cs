using Messenger.Models;
using Messenger.Modules;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Messenger
{
    /// <summary>
    /// Interaction logic for Connection.xaml
    /// </summary>
    public partial class Connection : Page
    {
        private class _Temp
        {
            public string I { get; set; } = string.Empty;
            public string P { get; set; } = string.Empty;
        }

        private BindingList<Host> _hosts = new BindingList<Host>();

        public Connection()
        {
            InitializeComponent();
            uiTableGrid.DataContext = new _Temp();
            Loaded += _Loaded;
        }

        private void _Loaded(object sender, RoutedEventArgs e)
        {
            if (HostModule.Name != null)
            {
                uiHostBox.Text = HostModule.Name;
                uiPortBox.Text = HostModule.Port.ToString();
            }
            uiCodeBox.Text = ProfileModule.Current.ID.ToString();
            uiServerList.ItemsSource = _hosts;
            uiServerList.SelectionChanged += ListBox_SelectionChanged;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count < 1)
                return;
            var itm = e.AddedItems[0] as Host;
            if (itm == null)
                return;
            uiHostBox.Text = itm.Address?.ToString();
            uiPortBox.Text = itm.Port.ToString();
            uiServerList.SelectedIndex = -1;
        }

        private async void _Click(object sender, RoutedEventArgs e)
        {
            var src = (Button)e.OriginalSource;

            async void refresh()
            {
                uiRefreshButton.IsEnabled = false;
                var lst = await Task.Run(() => HostModule.Refresh());
                foreach (var inf in lst)
                {
                    int idx = _hosts.IndexOf(inf);
                    if (idx < 0)
                        _hosts.Add(inf);
                    else _hosts[idx] = inf;
                }
                uiRefreshButton.IsEnabled = true;
            }

            if (src == uiBrowserButton)
            {
                uiBrowserButton.Visibility = Visibility.Collapsed;
                uiClearButton.Visibility =
                uiRefreshButton.Visibility =
                uiListGrid.Visibility = Visibility.Visible;
                refresh();
                return;
            }
            else if (src == uiRefreshButton)
            {
                refresh();
                return;
            }
            else if (src == uiClearButton)
            {
                _hosts.Clear();
                return;
            }

            var flg = false;
            uiConnectButton.IsEnabled = false;

            try
            {
                var uid = int.Parse(uiCodeBox.Text);
                var pot = int.Parse(uiPortBox.Text);
                var hos = uiHostBox.Text;

                await Task.Run(() =>
                {
                    var add = IPAddress.TryParse(hos, out var hst);
                    if (add == false)
                        hst = Dns.GetHostEntry(hos).AddressList.First(r => r.AddressFamily == AddressFamily.InterNetwork);
                    var iep = new IPEndPoint(hst, pot);
                    LinkModule.Start(uid, iep);
                    HostModule.Name = hos;
                    HostModule.Port = pot;
                    flg = true;
                });
            }
            catch (Exception ex)
            {
                Entrance.ShowError("连接失败", ex);
            }

            if (flg == true)
                NavigationService.Navigate(new PageFrame());
            uiConnectButton.IsEnabled = true;
        }
    }
}
