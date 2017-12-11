using Messenger.Models;
using Messenger.Modules;
using Microsoft.Win32;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Messenger
{
    /// <summary>
    /// Interaction logic for Chatter.xaml
    /// </summary>
    public partial class Chatter : Page
    {
        private Profile _profile = null;
        private BindingList<Packet> _messages = null;

        public Profile Profile => _profile;

        public Chatter()
        {
            InitializeComponent();
            Loaded += _Loaded;
            Unloaded += _Unloaded;
        }

        private void _Loaded(object sender, RoutedEventArgs e)
        {
            HistoryModule.Receive += _HistoryReceiving;
            (Application.Current as App).TextBoxKeyDown += _TextBoxKeyDown;

            _profile = ProfileModule.Inscope;
            _messages = HistoryModule.Query(_profile.Id);
            uiProfileGrid.DataContext = _profile;
            uiMessageBox.ItemsSource = _messages;
            _messages.ListChanged += _ListChanged;
            _ScrollDown();
        }

        private void _Unloaded(object sender, RoutedEventArgs e)
        {
            uiMessageBox.ItemsSource = null;
            HistoryModule.Receive -= _HistoryReceiving;
            (Application.Current as App).TextBoxKeyDown -= _TextBoxKeyDown;
            _messages.ListChanged -= _ListChanged;
        }

        private void _ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType != ListChangedType.ItemAdded)
                return;
            _ScrollDown();
        }

        /// <summary>
        /// 拦截消息通知
        /// </summary>
        private void _HistoryReceiving(object sender, LinkEventArgs<Packet> e)
        {
            if (e.Object.Groups != _profile.Id)
                return;
            e.Finish = true;
        }

        private void _TextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (sender != uiInputBox || e.Key != Key.Enter)
                return;
            var val = SettingModule.UseCtrlEnter;
            var mod = e.KeyboardDevice.Modifiers;
            if ((mod & ModifierKeys.Shift) == ModifierKeys.Shift)
                return;
            if ((mod & ModifierKeys.Control) == ModifierKeys.Control && val == false)
                return;
            if ((mod & ModifierKeys.Control) != ModifierKeys.Control && val == true)
                return;
            _SendText();
            e.Handled = true;
        }

        private void _Click(object sender, RoutedEventArgs e)
        {
            var tag = (e.OriginalSource as Button)?.Tag as string;
            if (tag == "symbol" && uiSymbolGrid.Visibility != Visibility.Visible)
                uiSymbolGrid.Visibility = Visibility.Visible;
            else
                uiSymbolGrid.Visibility = Visibility.Collapsed;

            if (tag == "text")
                _SendText();
            else if (tag == "image")
                _PushImage();
            else if (tag == "clean")
                HistoryModule.Clear(_profile.Id);
            uiInputBox.Focus();
        }

        private void _SymbolClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is string con)
                _TextBoxInsert(uiInputBox, con);
            uiInputBox.Focus();
        }

        private void _ScrollDown()
        {
            var idx = _messages.Count - 1;
            if (idx < 0)
                return;
            uiMessageBox.ScrollIntoView(_messages[idx]);
        }

        private void _TextBoxInsert(TextBox textbox, string str)
        {
            var txt = textbox.Text;
            var sta = textbox.SelectionStart;
            var len = textbox.SelectionLength;
            var bef = txt.Substring(0, sta);
            var aft = txt.Substring(sta + len);
            var val = string.Concat(bef, str, aft);
            textbox.Text = val;
            textbox.SelectionStart = sta + str.Length;
            textbox.SelectionLength = 0;
        }

        private void _SendText()
        {
            var str = uiInputBox.Text.TrimEnd(new char[] { '\0', '\r', '\n', '\t', ' ' });
            if (str.Length < 1)
                return;
            uiInputBox.Text = string.Empty;
            PostModule.Text(_profile.Id, str);
            ProfileModule.SetRecent(_profile);
        }

        private void _PushImage()
        {
            var ofd = new OpenFileDialog() { Filter = "位图文件|*.bmp;*.png;*.jpg" };
            if (ofd.ShowDialog() != true)
                return;
            try
            {
                var buf = CacheModule.ImageZoom(ofd.FileName);
                PostModule.Image(_profile.Id, buf);
                ProfileModule.SetRecent(_profile);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                Entrance.ShowError("发送图片失败", ex);
            }
        }

        private void _Share(string path)
        {
            if (File.Exists(path))
                PostModule.File(_profile.Id, path);
            else if (Directory.Exists(path))
                PostModule.Directory(_profile.Id, path);
            return;
        }

        private void _TextBoxPreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) == false)
                e.Effects = DragDropEffects.None;
            else
                e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void _TextBoxPreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) == false)
                return;
            var arr = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (arr == null || arr.Length < 1)
                return;
            var val = arr[0];
            _Share(val);
        }
    }
}
