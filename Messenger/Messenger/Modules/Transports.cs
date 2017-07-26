using Messenger.Foundation;
using Messenger.Models;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

        private static Transports s_ins = new Transports();

        public static long TimeTick => s_ins._watch?.ElapsedMilliseconds ?? 0L;
        public static string SavePath { get => s_ins._savepath; set => s_ins._savepath = value; }
        public static Transports Instance => s_ins;
        public static BindingList<Cargo> Expect => s_ins._expect;
        public static BindingList<Cargo> Takers => s_ins._takers;
        public static BindingList<Cargo> Makers => s_ins._makers;

        internal static void Trans_Changed(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
                {
                    for (var i = 0; i < s_ins._expect.Count; i++)
                    {
                        var tra = s_ins._expect[i].Transport;
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

        ///// <summary>
        ///// 尝试解析文件消息 返回 <see cref="Cargo"/> 对象 (失败返回 null)
        ///// </summary>
        //public static Cargo Take(PacketReader reader)
        //{
        //    var fil = default(Taker);
        //    var inf = default(PacketReader);
        //    try
        //    {
        //        inf = new PacketReader(reader["data"]);
        //        fil = new Taker(inf, () => FindPath(s_ins._savepath, inf["filename"].Pull<string>()));
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.E(nameof(Transports), ex, "接收文件出错.");
        //        return null;
        //    }
        //    var trs = new Cargo(e.Source, fil);
        //    Application.Current.Dispatcher.Invoke(() =>
        //        {
        //            // 在注册事件之前加入列表
        //            s_ins._expect.Add(trs);
        //            s_ins._takers.Add(trs);
        //            fil.Started += Trans_Changed;
        //            fil.Disposed += Trans_Changed;
        //        });
        //    return trs;
        //}

        ///// <summary>
        ///// 发送文件 返回 <see cref="Cargo"/> 对象 (失败返回 null)
        ///// </summary>
        ///// <param name="id">目标编号</param>
        ///// <param name="filepath">文件路径</param>
        //public static Cargo Make(int id, string filepath)
        //{
        //    var fil = default(Maker);
        //    try
        //    {
        //        fil = new Maker(filepath);
        //    }
        //    catch (Exception ex)
        //    {
        //        Entrance.ShowError("发送文件失败", ex);
        //        return null;
        //    }
        //    var itm = new Cargo(id, fil);
        //    Application.Current.Dispatcher.Invoke(() => s_ins._makers.Add(itm));
        //    var inf = new PacketWriter().
        //        Push("filename", fil.Name).
        //        Push("filesize", fil.Length).
        //        Push("guid", fil.Key).
        //        PushList("endpoints", Interact.GetEndPoints());
        //    // Interact.Enqueue(id, PacketGenre.FileInfo, inf.GetBytes());
        //    return itm;
        //}

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
                    act.Invoke(s_ins._makers);
                    act.Invoke(s_ins._takers);
                });

            return lst;
        }

        /// <summary>
        /// 检查文件名在指定目录下是否可用 如果冲突则添加随机后缀并重试 再次失败则抛出异常
        /// </summary>
        /// <param name="dir">目录路径</param>
        /// <param name="name">文件名</param>
        /// <exception cref="IOException"></exception>
        public static string FindPath(string name)
        {
            var dif = new DirectoryInfo(s_ins._savepath);
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
            s_ins._savepath = pth;
        }

        [AutoSave(4)]
        public static void Save()
        {
            Options.SetOption(TransPathKey, s_ins._savepath);
        }
    }
}
