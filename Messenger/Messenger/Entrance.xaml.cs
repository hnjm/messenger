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
        private class AutoLoadInfo
        {
            internal MethodInfo info;
            internal AutoLoadAttribute attribute;
        }

        public Entrance()
        {
            InitializeComponent();
            Loaded += _Loaded;
            Closing += _Closing;
        }

        private void _Loaded(object sender, RoutedEventArgs e)
        {
            IEnumerable<AutoLoadInfo> find()
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

                        yield return new AutoLoadInfo() { info = i, attribute = (AutoLoadAttribute)att };
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

        private void _Closing(object sender, CancelEventArgs e)
        {
            if (LinkModule.IsRunning == false)
                return;
            if (WindowState != WindowState.Minimized)
                WindowState = WindowState.Minimized;
            e.Cancel = true;
        }

        private void _Click(object sender, RoutedEventArgs e)
        {
            if (sender == uiConfirmButton)
            {
                uiMessagePanel.Visibility = Visibility.Collapsed;
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
                win.uiHeadText.Text = title;
                win.uiContentText.Text = content?.ToString() ?? "未提供信息";
                win.uiMessagePanel.Visibility = Visibility.Visible;
            });
        }
    }
}
