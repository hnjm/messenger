using Messenger.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Messenger.Modules
{
    /// <summary>
    /// 管理传输并提供界面绑定功能
    /// </summary>
    internal class ShareModule : INotifyPropertyChanging, INotifyPropertyChanged
    {
        private const string _KeyPath = "share-path";

        //private bool _hasexcept = false;
        //private bool _hastakers = false;
        //private bool _hasmakers = false;

        private bool _hasShare = false;
        private bool _hasReceiver = false;
        private bool _hasPending = false;

        private string _savepath = null;

        //private BindingList<Cargo> _expect = new BindingList<Cargo>();
        //private BindingList<Cargo> _takers = new BindingList<Cargo>();
        //private BindingList<Cargo> _makers = new BindingList<Cargo>();

        private readonly BindingList<Share> _shareList = new BindingList<Share>();
        private readonly BindingList<ShareReceiver> _receiverList = new BindingList<ShareReceiver>();
        private readonly BindingList<ShareReceiver> _pendingList = new BindingList<ShareReceiver>();

        //public bool HasExcept
        //{
        //    get => _hasexcept;
        //    set => _EmitChange(ref _hasexcept, value);
        //}

        //public bool HasTakers
        //{
        //    get => _hastakers;
        //    set => _EmitChange(ref _hastakers, value);
        //}

        //public bool HasMakers
        //{
        //    get => _hasmakers;
        //    set => _EmitChange(ref _hasmakers, value);
        //}

        public bool HasShare
        {
            get => _hasShare;
            set => OnPropertyChange(ref _hasShare, value);
        }

        public bool HasReceiver
        {
            get => _hasReceiver;
            set => OnPropertyChange(ref _hasReceiver, value);
        }

        public bool HasPending
        {
            get => _hasPending;
            set => OnPropertyChange(ref _hasPending, value);
        }

        #region PropertyChange
        public event PropertyChangedEventHandler PropertyChanged;

        public event PropertyChangingEventHandler PropertyChanging;

        private void OnPropertyChange<T>(ref T source, T target, [CallerMemberName] string name = null)
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
            if (Equals(source, target))
                return;
            source = target;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion

        private ShareModule()
        {
            _shareList.ListChanged += (s, e) => HasShare = _shareList.Count > 0;
            _receiverList.ListChanged += (s, e) => HasReceiver = _receiverList.Count > 0;
            _pendingList.ListChanged += (s, e) => HasPending = _pendingList.Count > 0;
            //_expect.ListChanged += BindingList_ListChanged;
            //_takers.ListChanged += BindingList_ListChanged;
            //_makers.ListChanged += BindingList_ListChanged;
        }

        //private void DispatcherTimer_Tick(object sender, EventArgs e)
        //{
        //    foreach (var t in _takers)
        //        t.Refresh(_watch.ElapsedMilliseconds);
        //    foreach (var t in _makers)
        //        t.Refresh(_watch.ElapsedMilliseconds);
        //}

        //private void BindingList_ListChanged(object sender, ListChangedEventArgs e)
        //{
        //    if (sender == _expect)
        //        HasExcept = _expect.Count > 0;
        //    else if (sender == _takers)
        //        HasTakers = _takers.Count > 0;
        //    else if (sender == _makers)
        //        HasMakers = _makers.Count > 0;
        //}

        // ---------- ---------- ---------- ---------- ---------- ---------- ---------- ----------

        private static ShareModule s_ins = new ShareModule();

        public static string SavePath { get => s_ins._savepath; set => s_ins._savepath = value; }
        public static ShareModule Instance => s_ins;
        //public static BindingList<Cargo> Expect => s_ins._expect;
        //public static BindingList<Cargo> Takers => s_ins._takers;
        //public static BindingList<Cargo> Makers => s_ins._makers;

        public static BindingList<Share> ShareList => s_ins._shareList;
        public static BindingList<ShareReceiver> ReceiverList => s_ins._receiverList;
        public static BindingList<ShareReceiver> PendingList => s_ins._pendingList;

        public static void Register(ShareReceiver receiver)
        {
            s_ins._receiverList.Add(receiver);
            s_ins._pendingList.Add(receiver);
            receiver.PropertyChanged += _RemovePending;
        }

        internal static void _RemovePending(object sender, PropertyChangedEventArgs e)
        {
            var pro = e.PropertyName;
            if (pro != nameof(ShareReceiver.IsStarted) && pro != nameof(ShareReceiver.IsDisposed))
                return;
            var obj = s_ins._pendingList.FirstOrDefault(r => ReferenceEquals(r, sender));
            if (obj == null)
                return;
            s_ins._pendingList.Remove(obj);
            obj.PropertyChanged -= _RemovePending;
        }

        //internal static void Trans_Changed(object sender, EventArgs e)
        //{
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        for (var i = 0; i < s_ins._expect.Count; i++)
        //        {
        //            var tra = s_ins._expect[i].Port;
        //            if (tra == sender)
        //            {
        //                s_ins._expect.RemoveAt(i);
        //                tra.Started -= Trans_Changed;
        //                tra.Disposed -= Trans_Changed;
        //                return;
        //            }
        //        }
        //    });
        //}

        //public static void Close()
        //{
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        foreach (var i in s_ins._takers)
        //            i.Close();
        //        foreach (var i in s_ins._makers)
        //            i.Close();
        //    });
        //}

        public static void Close()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var i in s_ins._shareList)
                    ((IDisposable)i).Dispose();
                foreach (var i in s_ins._receiverList)
                    ((IDisposable)i).Dispose();
            });
        }

        /// <summary>
        /// 移除所有 <see cref="IDisposed.IsDisposed"/> 值为真的项目, 返回被移除的项目
        /// </summary>
        public static List<IDisposed> Remove()
        {
            var lst = new List<IDisposed>();
            void remove<T>(IList<T> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var val = (IDisposed)list[i];
                    if (val.IsDisposed == false)
                        continue;
                    lst.Add(val);
                    list.RemoveAt(i);
                    i--;
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                remove(s_ins._shareList);
                remove(s_ins._receiverList);
            });
            return lst;
        }

        #region Other methods
        /// <summary>
        /// 检查文件名在指定目录下是否可用 如果冲突则添加随机后缀并重试 再次失败则抛出异常
        /// </summary>
        /// <param name="name">文件名</param>
        /// <exception cref="IOException"></exception>
        public static FileInfo AvailableFile(string name)
        {
            var dif = new DirectoryInfo(s_ins._savepath);
            if (dif.Exists == false)
                dif.Create();
            var pth = Path.Combine(dif.FullName, name);
            var fif = new FileInfo(pth);
            if (fif.Exists == false)
                return fif;
            int idx = fif.FullName.LastIndexOf(fif.Extension);
            var pathNoExt = (idx < 0 ? fif.FullName : fif.FullName.Substring(0, idx));
            var str = $"{pathNoExt} [{DateTime.Now:yyyyMMdd-HHmmss-fff}-{new Random().Next():x8}]{fif.Extension}";
            var inf = new FileInfo(str);
            if (inf.Exists)
                throw new IOException();
            return inf;
        }

        /// <summary>
        /// 检查目录名在指定目录下是否可用 如果冲突则添加随机后缀并重试 再次失败则抛出异常
        /// </summary>
        /// <param name="name">目录名</param>
        /// <exception cref="IOException"></exception>
        public static DirectoryInfo AvailableDirectory(string name)
        {
            var dif = new DirectoryInfo(s_ins._savepath);
            if (dif.Exists == false)
                dif.Create();
            var pth = Path.Combine(dif.FullName, name);
            var fif = new DirectoryInfo(pth);
            if (fif.Exists == false)
                return fif;

            var str = $"{name} [{DateTime.Now:yyyyMMdd-HHmmss-fff}-{new Random().Next():x8}]";
            var inf = new DirectoryInfo(Path.Combine(dif.FullName, str));
            if (inf.Exists)
                throw new IOException();
            return inf;
        }

        [AutoLoad(32, AutoLoadFlags.OnLoad)]
        public static void Load()
        {
            var pth = OptionModule.GetOption(_KeyPath);
            if (string.IsNullOrEmpty(pth))
                pth = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Received");
            s_ins._savepath = pth;
        }

        [AutoLoad(4, AutoLoadFlags.OnExit)]
        public static void Save()
        {
            OptionModule.SetOption(_KeyPath, s_ins._savepath);
        }
        #endregion
    }
}
