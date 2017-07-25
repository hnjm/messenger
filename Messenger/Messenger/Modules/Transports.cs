using Messenger.Foundation;
using Messenger.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Messenger.Modules
{
    /// <summary>
    /// 管理传输并提供界面绑定功能
    /// </summary>
    class Transports : INotifyPropertyChanged
    {
        public const string TransPathKey = "transport-path";

        private bool _hasexcept = false;
        private bool _hastakers = false;
        private bool _hasmakers = false;

        private string _savepath = null;

        private Stopwatch _watch = null;
        private DispatcherTimer _timer = null;

        private BindingList<Cargo> _expect = new BindingList<Cargo>();
        private BindingList<Cargo> _takers = new BindingList<Cargo>();
        private BindingList<Cargo> _makers = new BindingList<Cargo>();

        public bool HasExcept
        {
            get => _hasexcept;
            set
            {
                _hasexcept = value;
                OnPropertyChanged(nameof(HasExcept));
            }
        }

        public bool HasTakers
        {
            get => _hastakers;
            set
            {
                _hastakers = value;
                OnPropertyChanged(nameof(HasTakers));
            }
        }

        public bool HasMakers
        {
            get => _hasmakers;
            set
            {
                _hasmakers = value;
                OnPropertyChanged(nameof(HasMakers));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private Transports()
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

        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ---------- ---------- ---------- ---------- ---------- ---------- ---------- ---------- ---------- ---------- 

        private static Transports instance = new Transports();

        public static long TimeTick => instance._watch?.ElapsedMilliseconds ?? 0L;
        public static string SavePath { get => instance._savepath; set => instance._savepath = value; }
        public static Transports Instance => instance;
        public static BindingList<Cargo> Expect => instance._expect;
        public static BindingList<Cargo> Takers => instance._takers;
        public static BindingList<Cargo> Makers => instance._makers;

        private static void Trans_Changed(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
                {
                    for (var i = 0; i < instance._expect.Count; i++)
                    {
                        var tra = instance._expect[i].Transport;
                        if (tra == sender)
                        {
                            instance._expect.RemoveAt(i);
                            tra.Started -= Trans_Changed;
                            tra.Disposed -= Trans_Changed;
                            return;
                        }
                    }
                });
        }

        /// <summary>
        /// 尝试解析文件消息 返回 <see cref="Cargo"/> 对象 (失败返回 null)
        /// </summary>
        public static Cargo Take(PacketEventArgs e)
        {
            var fil = default(Taker);
            var inf = default(TransportHeader);
            try
            {
                inf = Xml.Deserialize<TransportHeader>(e.Stream);
                fil = new Taker(inf, () => FindPath(instance._savepath, inf.FileName));
            }
            catch (Exception ex)
            {
                Log.E(nameof(Transports), ex, "接收文件出错.");
                return null;
            }
            var trs = new Cargo(e.Source, fil);
            Application.Current.Dispatcher.Invoke(() =>
                {
                    // 在注册事件之前加入列表
                    instance._expect.Add(trs);
                    instance._takers.Add(trs);
                    fil.Started += Trans_Changed;
                    fil.Disposed += Trans_Changed;
                });
            return trs;
        }

        /// <summary>
        /// 发送文件 返回 <see cref="Cargo"/> 对象 (失败返回 null)
        /// </summary>
        /// <param name="id">目标编号</param>
        /// <param name="filepath">文件路径</param>
        public static Cargo Make(int id, string filepath)
        {
            var fil = default(Maker);
            try
            {
                fil = new Maker(filepath);
            }
            catch (Exception ex)
            {
                Entrance.ShowError("发送文件失败", ex);
                return null;
            }
            var itm = new Cargo(id, fil);
            Application.Current.Dispatcher.Invoke(() => instance._makers.Add(itm));
            var lst = (from e in Interact.GetEndPoints() select $"{e.Address}:{e.Port}").ToList();
            var inf = new TransportHeader() { Key = fil.Key, FileName = fil.Name, FileLength = fil.Length, EndPoints = lst };
            Interact.Enqueue(id, PacketGenre.FileInfo, inf);
            return itm;
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
                        var trs = r[i].Transport;
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
                    act.Invoke(instance._makers);
                    act.Invoke(instance._takers);
                });

            return lst;
        }

        /// <summary>
        /// 检查文件名在指定目录下是否可用 如果冲突则添加随机后缀并重试 再次失败则抛出异常
        /// </summary>
        /// <param name="dir">目录路径</param>
        /// <param name="name">文件名</param>
        /// <exception cref="IOException"></exception>
        public static string FindPath(string dir, string name)
        {
            var dif = new DirectoryInfo(dir);
            if (!dif.Exists)
                dif.Create();
            var pth = Path.Combine(dif.FullName, name);
            var fif = new FileInfo(pth);
            if (!fif.Exists)
                return fif.FullName;
            int idx = fif.FullName.LastIndexOf(fif.Extension);
            var pathNoExt = (idx < 0 ? fif.FullName : fif.FullName.Substring(0, idx));
            var str = $"{pathNoExt} [{new Random().Next():x8}]{fif.Extension}";
            if (!File.Exists(str))
                return str;
            throw new IOException();
        }

        [AutoLoad(32)]
        public static void Load()
        {
            var pth = Options.GetOption(TransPathKey);
            if (string.IsNullOrEmpty(pth))
                pth = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Received");
            instance._savepath = pth;
        }

        [AutoSave(4)]
        public static void Save()
        {
            Options.SetOption(TransPathKey, instance._savepath);
        }
    }
}
