using Messenger.Models;
using Messenger.Modules;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using static Messenger.Extensions.NativeMethods;
using static System.Windows.ResizeMode;
using static System.Windows.WindowState;

namespace Messenger
{
    /// <summary>
    /// Interaction logic for Entrance.xaml
    /// </summary>
    public partial class Entrance : Window
    {
        private class AutoLoadInfo
        {
            public MethodInfo Method;
            public AutoLoadAttribute Attribute;
        }

        public Entrance()
        {
            InitializeComponent();
            Loaded += _Loaded;
            Closing += _Closing;
        }

        private void _Loaded(object sender, RoutedEventArgs e)
        {
            #region Flat window style
            var han = new WindowInteropHelper(this).Handle;
            var now = GetWindowLong(han, GWL_STYLE);
            var res = SetWindowLong(han, GWL_STYLE, now & ~WS_SYSMENU);
            #endregion

            // 利用反射识别所有标识有 AutoLoad 函数
            IEnumerable<AutoLoadInfo> _Find()
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
                        yield return new AutoLoadInfo() { Method = i, Attribute = (AutoLoadAttribute)att };
                    }
                }
            }

            var loa = _Find().Where(r => r.Attribute.Flag == AutoLoadFlags.OnLoad).ToList();
            loa.Sort((a, b) => a.Attribute.Level - b.Attribute.Level);
            var sav = _Find().Where(r => r.Attribute.Flag == AutoLoadFlags.OnExit).ToList();
            sav.Sort((a, b) => a.Attribute.Level - b.Attribute.Level);

            loa.ForEach(m => m.Method.Invoke(null, null));
            Closed += (s, arg) => sav.ForEach(m => m.Method.Invoke(null, null));
        }

        private void _Closing(object sender, CancelEventArgs e)
        {
            if (LinkModule.IsRunning == false)
                return;
            if (WindowState != Minimized)
                WindowState = Minimized;
            e.Cancel = true;
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

        private void _Click(object sender, RoutedEventArgs e)
        {
            var tag = (e.OriginalSource as Button)?.Tag as string;

            if (tag == "confirm")
                uiMessagePanel.Visibility = Visibility.Collapsed;
            #region Flat window style
            else if (tag == "min")
                WindowState = Minimized;
            else if (tag == "max")
                _Toggle();
            else if (tag == "exit")
                Close();
            #endregion
            return;
        }

        #region Flat window style
        private void _Toggle()
        {
            if (ResizeMode != CanResize && ResizeMode != CanResizeWithGrip)
                return;
            WindowState = (WindowState == Maximized) ? Normal : Maximized;
        }

        private void _MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                _Toggle();
            else
                DragMove();
            return;
        }
        #endregion
    }
}
