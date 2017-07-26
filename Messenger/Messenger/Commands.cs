using Messenger.Models;
using Messenger.Modules;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Messenger
{
    public static class Commands
    {
        public static RoutedUICommand CopyText { get; } = new RoutedUICommand() { Text = "复制消息内容" };

        public static RoutedUICommand Remove { get; } = new RoutedUICommand() { Text = "移除这条消息" };

        public static RoutedUICommand ViewImage { get; } = new RoutedUICommand() { Text = "在图片查看器中查看" };

        static Commands()
        {
            var cpy = new CommandBinding();
            cpy.Command = CopyText;
            cpy.CanExecute += Copy_CanExecute;
            cpy.Executed += Copy_Executed;

            var rmv = new CommandBinding();
            rmv.Command = Remove;
            rmv.CanExecute += Remove_CanExecute;
            rmv.Executed += Remove_Executed;

            var vie = new CommandBinding();
            vie.Command = ViewImage;
            vie.CanExecute += LargeImage_CanExecute;
            vie.Executed += LargeImage_Executed;

            Application.Current.MainWindow.CommandBindings.Add(cpy);
            Application.Current.MainWindow.CommandBindings.Add(rmv);
            Application.Current.MainWindow.CommandBindings.Add(vie);
        }

        private static void LargeImage_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var msg = (e.OriginalSource as FrameworkElement)?.DataContext as Packet;
            if (msg is null || msg.Path != "image")
                e.CanExecute = false;
            else
                e.CanExecute = true;
            e.Handled = true;
        }

        private static void LargeImage_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var msg = (e.OriginalSource as FrameworkElement)?.DataContext as Packet;
            if (msg is null)
                return;
            var str = msg.Value as string;
            if (str == null)
                return;
            try
            {
                var flp = Caches.GetPath(str);
                Process.Start(flp);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private static void Remove_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var msg = (e.OriginalSource as FrameworkElement)?.DataContext as Packet;
            if (msg is null)
                e.CanExecute = false;
            else
                e.CanExecute = true;
            e.Handled = true;
        }

        private static void Remove_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var msg = (e.OriginalSource as FrameworkElement)?.DataContext as Packet;
            if (msg is null)
                return;
            Packets.Remove(msg);
            e.Handled = true;
        }

        private static void Copy_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var val = (e.OriginalSource as FrameworkElement)?.DataContext as Packet;
            if (val == null || val.Path != "text")
                e.CanExecute = false;
            else
                e.CanExecute = true;
            e.Handled = true;
        }

        private static void Copy_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var msg = (e.OriginalSource as FrameworkElement)?.DataContext as Packet;
            if (msg?.MessageText is null)
                return;
            try
            {
                Clipboard.SetText(msg.MessageText);
            }
            catch (Exception ex)
            {
                Entrance.ShowError("复制消息到剪切板出错", ex);
            }
            e.Handled = true;
        }
    }
}
