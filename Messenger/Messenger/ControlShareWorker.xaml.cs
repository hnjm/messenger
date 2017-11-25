using Messenger.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Messenger
{
    /// <summary>
    /// ControlShareWorker.xaml 的交互逻辑
    /// </summary>
    public partial class ControlShareWorker : UserControl
    {
        public ControlShareWorker()
        {
            InitializeComponent();
        }

        private void _Click(object sender, RoutedEventArgs e)
        {
            var btn = e.OriginalSource as Button;
            if (btn == null)
                return;
            var con = btn.DataContext as ShareReceiver;
            var tag = btn.Tag as string;
            if (con == null || tag == null)
                return;
            if (tag == "play")
                con.Start();
            else if (tag == "stop")
                con.Dispose();
            return;
        }
    }
}
