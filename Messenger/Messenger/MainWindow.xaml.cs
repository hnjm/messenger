using System;
using System.ComponentModel;
using System.Windows;

namespace Messenger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ModulePacket.Load();
            }
            catch (Exception ex)
            {
                MessageWindow.Show(this, $"加载数据库出错, 你可能需要安装与 {nameof(System.Data.SQLite)} 相匹配的 Visual C++ 运行库", ex);
                Application.Current.Shutdown(1);
                return;
            }

            ModuleOption.Load();
            ModuleServer.Load();
            ModuleSetting.Load();
            ModuleProfile.Load();
            ModuleTrans.Load();

            Closed += delegate
                {
                    Interact.Close();
                    ModulePacket.Save();
                    ModuleTrans.Save();
                    ModuleProfile.Save();
                    ModuleSetting.Save();
                    ModuleServer.Save();
                    ModuleOption.Save();
                };
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (Interact.IsRunning == false)
                return;
            if (WindowState != WindowState.Minimized)
                WindowState = WindowState.Minimized;
            e.Cancel = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == buttonConfirm)
            {
                gridMessage.Visibility = Visibility.Collapsed;
                return;
            }
        }

        /// <summary>
        /// 显示提示信息 (可以跨线程调用)
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="content">内容 (调用 <see cref="object.ToString"/> 方法)</param>
        public static void ShowMessage(string title, object content)
        {
            var app = Application.Current;
            var dis = app.Dispatcher;
            dis.Invoke(() =>
                {
                    var win = app.MainWindow as MainWindow;
                    if (win == null)
                        return;
                    win.textblockHeader.Text = title;
                    win.textboxContent.Text = content?.ToString() ?? "未提供信息";
                    win.gridMessage.Visibility = Visibility.Visible;
                });
        }
    }
}
