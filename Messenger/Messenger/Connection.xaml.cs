using Messenger.Foundation;
using System;
using System.ComponentModel;
using System.Net;
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
        private class AnonymousBindingObject
        {
            public string A { get; set; } = string.Empty;
            public string B { get; set; } = string.Empty;
            public string C { get; set; } = string.Empty;
        }

        /// <summary>
        /// 局域网服务器信息列表
        /// </summary>
        private BindingList<PacketServer> serverList = new BindingList<PacketServer>();

        public Connection()
        {
            InitializeComponent();
            gridTable.DataContext = new AnonymousBindingObject();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            textboxAddress.Text = ModuleServer.Current?.Address?.ToString();
            textboxPort.Text = ModuleServer.Current?.Port.ToString();
            textboxNumber.Text = ModuleProfile.Current.ID.ToString();
            listboxServer.ItemsSource = serverList;
            listboxServer.SelectionChanged += ListBox_SelectionChanged;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count < 1)
                return;
            var itm = e.AddedItems[0] as ItemServer;
            if (itm == null)
                return;
            textboxAddress.Text = itm.Address?.ToString();
            textboxPort.Text = itm.Port.ToString();
            listboxServer.SelectedIndex = -1;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == buttonBrowser)
            {
                buttonBrowser.Visibility = Visibility.Collapsed;
                buttonClear.Visibility =
                buttonRefresh.Visibility =
                gridList.Visibility = Visibility.Visible;
                RefreshAsync();
                return;
            }
            else if (sender == buttonRefresh)
            {
                RefreshAsync();
                return;
            }
            else if (sender == buttonClear)
            {
                serverList.Clear();
                return;
            }

            try
            {
                var uid = int.Parse(textboxNumber.Text);
                var prt = int.Parse(textboxPort.Text);
                var adr = IPAddress.Parse(textboxAddress.Text);
                var iep = new IPEndPoint(adr, prt);
                ConnectAsync(uid, iep);
            }
            catch (Exception ex)
            {
                MainWindow.ShowMessage("输入信息有误, 请检查.", ex);
            }
        }

        private async void ConnectAsync(int id, IPEndPoint endpoint)
        {
            buttonConnect.IsEnabled = false;
            var exc = await Task.Run(() =>
                {
                    try
                    {
                        Interact.Start(id, endpoint);
                        ModuleServer.Current = endpoint;
                        return null;
                    }
                    catch (Exception ex)
                    {
                        return ex;
                    }
                });

            if (exc == null)
                NavigationService.Navigate(new ProfileFrame());
            else
                MainWindow.ShowMessage("连接服务器失败", exc);
            buttonConnect.IsEnabled = true;
        }

        private async void RefreshAsync()
        {
            buttonRefresh.IsEnabled = false;
            var lst = await Task.Run(() => ModuleServer.Refresh());
            foreach (var inf in lst)
            {
                int idx = serverList.IndexOf(inf);
                if (idx < 0)
                    serverList.Add(inf);
                else
                    serverList[idx] = inf;
            }
            buttonRefresh.IsEnabled = true;
        }
    }
}
