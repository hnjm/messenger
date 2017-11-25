using Messenger.Models;
using Messenger.Modules;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace Messenger
{
    /// <summary>
    /// Interaction logic for Entrance.xaml
    /// </summary>
    public partial class Entrance : Window
    {
        private class _Info
        {
            internal MethodInfo info;
            internal AutoLoadAttribute attribute;
        }

        public Entrance()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IEnumerable<_Info> find()
            {
                var ass = typeof(Entrance).Assembly;
                foreach (var t in ass.GetTypes())
                {
                    var met = t.GetMethods(BindingFlags.Static | BindingFlags.Public);
                    foreach (var i in met)
                    {
                        var att = i.GetCustomAttributes(typeof(AutoLoadAttribute)).FirstOrDefault();
                        if (att == null)
                            continue;

                        yield return new _Info() { info = i, attribute = (AutoLoadAttribute)att };
                    }
                }
            }

            var loa = find().Where(r => r.attribute.Flag == AutoLoadFlags.OnLoad).ToList();
            loa.Sort((a, b) => a.attribute.Level - b.attribute.Level);
            var sav = find().Where(r => r.attribute.Flag == AutoLoadFlags.OnExit).ToList();
            sav.Sort((a, b) => a.attribute.Level - b.attribute.Level);

            loa.ForEach(m => m.info.Invoke(null, null));
            Closed += (s, arg) => sav.ForEach(m => m.info.Invoke(null, null));
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (Linkers.IsRunning == false)
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
        public static void ShowError(string title, object content)
        {
            var app = Application.Current;
            var dis = app.Dispatcher;
            dis.Invoke(() =>
                {
                    var win = app.MainWindow as Entrance;
                    if (win == null)
                        return;
                    win.textblockHeader.Text = title;
                    win.textboxContent.Text = content?.ToString() ?? "未提供信息";
                    win.gridMessage.Visibility = Visibility.Visible;
                });
        }
    }
}
