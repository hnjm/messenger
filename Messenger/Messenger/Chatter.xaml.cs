using Messenger.Models;
using Messenger.Modules;
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
            HistoryModule.Receiving += ModuleMessage_Receiving;
            (Application.Current as App).TextBoxKeyDown += TextBox_KeyDown;

            _profile = ProfileModule.Inscope;
            //if (_profile.ID <= Links.ID)
            //    buttonFile.Visibility = Visibility.Collapsed;
            uiProfileGrid.DataContext = _profile;

            _messages = HistoryModule.Query(_profile.ID);
            uiMessageBox.ItemsSource = _messages;
            _messages.ListChanged += Messages_ListChanged;
            _ScrollToEnd();
        }

        private void _Unloaded(object sender, RoutedEventArgs e)
        {
            uiMessageBox.ItemsSource = null;
            HistoryModule.Receiving -= ModuleMessage_Receiving;
            (Application.Current as App).TextBoxKeyDown -= TextBox_KeyDown;
            _messages.ListChanged -= Messages_ListChanged;
        }

        private void Messages_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType != ListChangedType.ItemAdded)
                return;
            _ScrollToEnd();
        }

        /// <summary>
        /// 拦截消息通知
        /// </summary>
        private void ModuleMessage_Receiving(object sender, LinkEventArgs<Packet> e)
        {
            if (e.Record.Groups != _profile.ID)
                return;
            e.Finish = true;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender != textboxInput || e.Key != Key.Enter)
                return;
            var val = SettingModule.UseCtrlEnter;
            var mod = e.KeyboardDevice.Modifiers;
            if ((mod & ModifierKeys.Shift) == ModifierKeys.Shift)
                return;
            if ((mod & ModifierKeys.Control) == ModifierKeys.Control && val == false)
                return;
            if ((mod & ModifierKeys.Control) != ModifierKeys.Control && val == true)
                return;
            _InsertText();
            e.Handled = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == buttonSymbol && uiSymbolGrid.Visibility != Visibility.Visible)
                uiSymbolGrid.Visibility = Visibility.Visible;
            else
                uiSymbolGrid.Visibility = Visibility.Collapsed;

            if (sender == buttonFile)
            {
                var dia = new System.Windows.Forms.OpenFileDialog();
                if (dia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    _Share(dia.FileName);
            }

            if (sender == buttonText)
                _InsertText();
            else if (sender == buttonImage)
                _InsertImage();
            else if (sender == buttonClean)
                HistoryModule.Clear(_profile.ID);
            textboxInput.Focus();
        }

        private void Symbol_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is string con)
                _InsertText(textboxInput, con);
            textboxInput.Focus();
        }

        private void _ScrollToEnd()
        {
            var idx = _messages.Count - 1;
            if (idx < 0)
                return;
            uiMessageBox.ScrollIntoView(_messages[idx]);
        }

        private void _InsertText(TextBox textbox, string str)
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

        private void _InsertText()
        {
            var str = textboxInput.Text.TrimEnd(new char[] { '\0', '\r', '\n', '\t', ' ' });
            if (str.Length < 1)
                return;
            textboxInput.Text = string.Empty;
            PostModule.Message(_profile.ID, str);
            ProfileModule.SetRecent(_profile);
        }

        private void _InsertImage()
        {
            var ofd = new System.Windows.Forms.OpenFileDialog() { Filter = "位图文件|*.bmp;*.png;*.jpg" };
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            try
            {
                var buf = CacheModule.ImageResize(ofd.FileName);
                PostModule.Message(_profile.ID, buf);
                ProfileModule.SetRecent(_profile);
            }
            catch (Exception ex)
            {
                Entrance.ShowError("发送图片失败", ex);
            }
        }
        
        private void _Share(string path)
        {
            var sha = default(Share);
            if (File.Exists(path))
                sha = PostModule.File(_profile.ID, path);
            else if (Directory.Exists(path))
                sha = PostModule.Directory(_profile.ID, path);
            if (sha == null)
                return;
            var pkt = new Packet() { Source = LinkModule.ID, Target = _profile.ID, Groups = _profile.ID, Path = "share", Value = sha };
            _messages.Add(pkt);
        }

        private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (/*_profile.ID <= Links.ID || */e.Data.GetDataPresent(DataFormats.FileDrop) == false)
                e.Effects = DragDropEffects.None;
            else
                e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            if (/*_profile.ID <= Links.ID ||*/ e.Data.GetDataPresent(DataFormats.FileDrop) == false)
                return;
            var fil = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (fil == null || fil.Length < 1)
                return;
            var val = fil[0];
            _Share(val);
        }
    }
}
