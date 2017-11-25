using Messenger.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace Messenger.Modules
{
    /// <summary>
    /// 管理传输并提供界面绑定功能
    /// </summary>
    internal class ShareModule : INotifyPropertyChanging, INotifyPropertyChanged
    {
        private const string _KeyPath = "share-path";

        private bool _hasexcept = false;
        private bool _hastakers = false;
        private bool _hasmakers = false;

        private string _savepath = null;

        private Stopwatch _watch = null;
        private DispatcherTimer _timer = null;

        private BindingList<Cargo> _expect = new BindingList<Cargo>();
        private BindingList<Cargo> _takers = new BindingList<Cargo>();
        private BindingList<Cargo> _makers = new BindingList<Cargo>();
        private readonly BindingList<Share> _shareList = new BindingList<Share>();

        public bool HasExcept
        {
            get => _hasexcept;
            set => _EmitChange(ref _hasexcept, value);
        }

        public bool HasTakers
        {
            get => _hastakers;
            set => _EmitChange(ref _hastakers, value);
        }

        public bool HasMakers
        {
            get => _hasmakers;
            set => _EmitChange(ref _hasmakers, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event PropertyChangingEventHandler PropertyChanging;

        private void _EmitChange<T>(ref T source, T target, [CallerMemberName] string name = null)
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
            if (Equals(source, target))
                return;
            source = target;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private ShareModule()
        {
            _expect.ListChanged += BindingList_ListChanged;
            _takers.ListChanged += BindingList_ListChanged;
            _makers.ListChanged += BindingList_ListChanged;

            _watch = new Stopwatch();
            _timer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher);
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += DispatcherTimer_Tick;
            _watch.Start();
            _timer.Start();
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            foreach (var t in _takers)
                t.Refresh(_watch.ElapsedMilliseconds);
            foreach (var t in _makers)
                t.Refresh(_watch.ElapsedMilliseconds);
        }

        private void BindingList_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (sender == _expect)
                HasExcept = _expect.Count > 0;
            else if (sender == _takers)
                HasTakers = _takers.Count > 0;
            else if (sender == _makers)
                HasMakers = _makers.Count > 0;
        }

        // ---------- ---------- ---------- ---------- ---------- ---------- ---------- ----------

        private static ShareModule s_ins = new ShareModule();

        public static long TimeTick => s_ins._watch?.ElapsedMilliseconds ?? 0L;
        public static string SavePath { get => s_ins._savepath; set => s_ins._savepath = value; }
        public static ShareModule Instance => s_ins;
        public static BindingList<Cargo> Expect => s_ins._expect;
        public static BindingList<Cargo> Takers => s_ins._takers;
        public static BindingList<Cargo> Makers => s_ins._makers;

        public static BindingList<Share> ShareList => s_ins._shareList;

        internal static void Trans_Changed(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                for (var i = 0; i < s_ins._expect.Count; i++)
                {
                    var tra = s_ins._expect[i].Port;
                    if (tra == sender)
                    {
                        s_ins._expect.RemoveAt(i);
                        tra.Started -= Trans_Changed;
                        tra.Disposed -= Trans_Changed;
                        return;
                    }
                }
            });
        }

        public static void Close()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var i in s_ins._takers)
                    i.Close();
                foreach (var i in s_ins._makers)
                    i.Close();
            });
        }

        /// <summary>
        /// 移除所有 <see cref="IManager.IsDisposed"/> 为真的项目 返回被移除的项目
        /// </summary>
        public static List<Cargo> Remove()
        {
            var lst = new List<Cargo>();
            var act = new Action<IList<Cargo>>((r) =>
            {
                var i = 0;
                while (i < r.Count)
                {
                    var trs = r[i].Port;
                    if (trs.IsDisposed)
                    {
                        lst.Add(r[i]);
                        r.RemoveAt(i);
                    }
                    else i++;
                }
            });

            Application.Current.Dispatcher.Invoke(() =>
            {
                act.Invoke(s_ins._makers);
                act.Invoke(s_ins._takers);
            });

            return lst;
        }

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
            var pth = Options.GetOption(_KeyPath);
            if (string.IsNullOrEmpty(pth))
                pth = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Received");
            s_ins._savepath = pth;
        }

        [AutoLoad(4, AutoLoadFlags.OnExit)]
        public static void Save()
        {
            Options.SetOption(_KeyPath, s_ins._savepath);
        }
    }
}
