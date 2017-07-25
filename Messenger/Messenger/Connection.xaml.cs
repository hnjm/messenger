﻿using Messenger.Foundation;
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
        private class _TmpObj
        {
            public string I { get; set; } = string.Empty;
            public string P { get; set; } = string.Empty;
        }

        /// <summary>
        /// 局域网服务器信息列表
        /// </summary>
        private BindingList<PacketServer> _hosts = new BindingList<PacketServer>();

        public Connection()
        {
            InitializeComponent();
            gridTable.DataContext = new _TmpObj();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (Hosts.Name != null)
            {
                textboxHost.Text = Hosts.Name;
                textboxPort.Text = Hosts.Port.ToString();
            }
            textboxNumber.Text = Profiles.Current.ID.ToString();
            listboxServer.ItemsSource = _hosts;
            listboxServer.SelectionChanged += ListBox_SelectionChanged;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count < 1)
                return;
            var itm = e.AddedItems[0] as Host;
            if (itm == null)
                return;
            textboxHost.Text = itm.Address?.ToString();
            textboxPort.Text = itm.Port.ToString();
            listboxServer.SelectedIndex = -1;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            async void refresh()
            {
                buttonRefresh.IsEnabled = false;
                var lst = await Task.Run(() => Hosts.Refresh());
                foreach (var inf in lst)
                {
                    int idx = _hosts.IndexOf(inf);
                    if (idx < 0)
                        _hosts.Add(inf);
                    else _hosts[idx] = inf;
                }
                buttonRefresh.IsEnabled = true;
            }

            if (sender == buttonBrowser)
            {
                buttonBrowser.Visibility = Visibility.Collapsed;
                buttonClear.Visibility =
                buttonRefresh.Visibility =
                gridList.Visibility = Visibility.Visible;
                refresh();
                return;
            }
            else if (sender == buttonRefresh)
            {
                refresh();
                return;
            }
            else if (sender == buttonClear)
            {
                _hosts.Clear();
                return;
            }

            var flg = false;
            try
            {
                buttonConnect.IsEnabled = false;
                var uid = int.Parse(textboxNumber.Text);
                var pot = int.Parse(textboxPort.Text);
                var hos = textboxHost.Text;

                await Task.Run(() =>
                {
                    var hst = Dns.GetHostEntry(hos).AddressList.First(r => r.AddressFamily == AddressFamily.InterNetwork);
                    var iep = new IPEndPoint(hst, pot);
                    Interact.Start(uid, iep);
                    Hosts.Name = hos;
                    Hosts.Port = pot;
                    flg = true;
                });
            }
            catch (SocketException ex)
            {
                Entrance.ShowError("网络连接失败.", ex);
            }
            catch (Exception ex)
            {
                Entrance.ShowError("输入信息有误.", ex);
            }
            finally
            {
                if (flg == true)
                    NavigationService.Navigate(new ProfileFrame());
                buttonConnect.IsEnabled = true;
            }
        }
    }
}
