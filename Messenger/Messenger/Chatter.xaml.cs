using Messenger.Foundation;
using Messenger.Models;
using Messenger.Modules;
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
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Packets.Receiving += ModuleMessage_Receiving;
            (Application.Current as App).TextBoxKeyDown += TextBox_KeyDown;

            _profile = Profiles.Inscope;
            if (_profile.ID <= Server.ID)
                buttonFile.Visibility = Visibility.Collapsed;
            gridProfile.DataContext = _profile;

            _messages = Packets.Query(_profile.ID);
            listboxMessage.ItemsSource = _messages;
            _messages.ListChanged += Messages_ListChanged;
            _ScrollToEnd();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            listboxMessage.ItemsSource = null;
            Packets.Receiving -= ModuleMessage_Receiving;
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
        private void ModuleMessage_Receiving(object sender, CommonEventArgs<Packet> e)
        {
            if (e.Object.Groups != _profile.ID)
                return;
            e.Finish = true;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender != textboxInput || e.Key != Key.Enter)
                return;
            var val = Settings.UseCtrlEnter;
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
            if (sender == buttonSymbol && gridSymbol.Visibility != Visibility.Visible)
                gridSymbol.Visibility = Visibility.Visible;
            else
                gridSymbol.Visibility = Visibility.Collapsed;

            if (sender == buttonFile)
            {
                var dia = new System.Windows.Forms.OpenFileDialog();
                if (dia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    _InsertTrans(dia.FileName);
            }

            if (sender == buttonText)
                _InsertText();
            else if (sender == buttonImage)
                _InsertImage();
            else if (sender == buttonClean)
                Packets.Clear(_profile.ID);
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
            listboxMessage.ScrollIntoView(_messages[idx]);
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
            Posters.Message(_profile.ID, str);
            Profiles.SetRecent(_profile);
        }

        private void _InsertImage()
        {
            var ofd = new System.Windows.Forms.OpenFileDialog() { Filter = "位图文件|*.bmp;*.png;*.jpg" };
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            try
            {
                var buf = Caches.ImageResize(ofd.FileName);
                Posters.Message(_profile.ID, buf);
                Profiles.SetRecent(_profile);
            }
            catch (Exception ex)
            {
                Entrance.ShowError("发送图片失败", ex);
            }
        }

        private void _InsertTrans(string path)
        {
            var trs = Posters.File(_profile.ID, path);
            if (trs == null)
                return;
            var pkt = new Packet() { Source = Interact.ID, Target = _profile.ID, Groups = _profile.ID, Path = "file", Value = trs };
            _messages.Add(pkt);
        }

        private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (_profile.ID <= Server.ID || e.Data.GetDataPresent(DataFormats.FileDrop) == false)
                e.Effects = DragDropEffects.None;
            else
                e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            if (_profile.ID <= Server.ID || e.Data.GetDataPresent(DataFormats.FileDrop) == false)
                return;
            var fil = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (fil == null || fil.Length < 1)
                return;
            var val = fil[0];
            if (File.Exists(val) == false)
                return;
            _InsertTrans(val);
        }
    }
}
