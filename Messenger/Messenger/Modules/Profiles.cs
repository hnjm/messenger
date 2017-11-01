using Messenger.Extensions;
using Messenger.Models;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Messenger.Modules
{
    /// <summary>
    /// 管理用户信息
    /// </summary>
    internal class Profiles : INotifyPropertyChanging, INotifyPropertyChanged
    {
        private const string _KeyCode = "profile-code";
        private const string _KeyName = "profile-name";
        private const string _KeyText = "profile-text";
        private const string _KeyImage = "profile-image";
        private const string _KeyLabel = "profile-groups";

        private bool _hasclient = false;
        private bool _hasgroups = false;
        private bool _hasrecent = false;
        private string _grouptags = null;
        private string _imagesource = null;
        private byte[] _imagebuffer = null;
        private IEnumerable<int> _groupids = null;
        private BindingList<Profile> _recent = new BindingList<Profile>();
        private BindingList<Profile> _client = new BindingList<Profile>();
        private BindingList<Profile> _groups = new BindingList<Profile>();
        private List<WeakReference> _spaces = new List<WeakReference>();
        private Profile _local = new Profile();
        private Profile _inscope = null;
        private EventHandler _inscopechanged = null;

        public bool HasClient
        {
            get => _hasclient;
            set => _EmitChange(ref _hasclient, value);
        }

        public bool HasGroups
        {
            get => _hasgroups;
            set => _EmitChange(ref _hasgroups, value);
        }

        public bool HasRecent
        {
            get => _hasrecent;
            set => _EmitChange(ref _hasrecent, value);
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

        private Profiles()
        {
            Profile.InstancePropertyChanged += (s, e) =>
            {
                if (e.PropertyName.Equals(nameof(Profile.Hint)))
                    _Changed();
            };
            _client.ListChanged += (s, e) => _Changed();
            _groups.ListChanged += (s, e) => _Changed();
            _recent.ListChanged += (s, e) => _Changed();
        }

        /// <summary>
        /// 重新计算未读消息数量
        /// </summary>
        private void _Changed()
        {
            var cli = _client.Sum(r => r.Hint);
            var gro = _groups.Sum(r => r.Hint);
            var rec = _recent.Sum(r => (r.Hint < 1 || _client.FirstOrDefault(t => t.ID == r.ID) != null || _groups.FirstOrDefault(t => t.ID == r.ID) != null) ? 0 : r.Hint);
            HasClient = cli > 0;
            HasGroups = gro > 0;
            HasRecent = rec > 0;
        }

        // ---------- ---------- ---------- ---------- ---------- ---------- ---------- ---------- ---------- ---------- 

        private static Profiles s_ins = new Profiles();

        public static Profiles Instance => s_ins;
        public static Profile Current => s_ins._local;
        public static Profile Inscope => s_ins._inscope;
        public static string GroupLabels => s_ins._grouptags;
        public static string ImageSource { get => s_ins._imagesource; set => s_ins._imagesource = value; }
        public static byte[] ImageBuffer { get => s_ins._imagebuffer; set => s_ins._imagebuffer = value; }
        public static IEnumerable<int> GroupIDs => s_ins._groupids;
        public static BindingList<Profile> RecentList => s_ins._recent;
        public static BindingList<Profile> ClientList => s_ins._client;
        public static BindingList<Profile> GroupsList => s_ins._groups;
        public static event EventHandler InscopeChanged { add => s_ins._inscopechanged += value; remove => s_ins._inscopechanged -= value; }

        public static void Clear()
        {
            var clt = s_ins._client;
            Application.Current.Dispatcher.Invoke(() => clt.Clear());
        }

        /// <summary>
        /// 添加或更新用户信息 (添加返回真, 更新返回假)
        /// </summary>
        public static void Insert(Profile profile)
        {
            var clt = s_ins._client;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var res = Query(profile.ID, true);
                res.CopyFrom(profile);
                var tmp = clt.FirstOrDefault(r => r.ID == profile.ID);
                if (tmp == null)
                    clt.Add(res);
            });
        }

        /// <summary>
        /// 根据编号查找用户信息
        /// </summary>
        /// <param name="id">编号</param>
        /// <param name="create">指定编号不存在时创建对象</param>
        public static Profile Query(int id, bool create = false)
        {
            var ins = s_ins;
            if (id == ins._local.ID)
                return ins._local;
            var spa = ins._spaces;

            var idx = 0;
            var pro = default(Profile);
            while (idx < spa.Count)
            {
                var tar = (Profile)spa[idx].Target;
                if (tar != null)
                {
                    if (pro == null && tar.ID == id)
                        pro = tar;
                    idx++;
                    continue;
                }
                spa.RemoveAt(idx);
            }

            if (pro != null)
                return pro;
            pro = ins._client.Concat(ins._groups).Concat(ins._recent).FirstOrDefault(t => t.ID == id);
            if (pro != null)
                return pro;
            if (create == false)
                return null;
            pro = new Profile() { ID = id, Name = $"未知 [{id}]" };
            spa.Add(new WeakReference(pro));
            return pro;
        }

        /// <summary>
        /// 移除所有 ID 不在给定集合的项目 并把含有未读消息的项目添加到最近列表
        /// </summary>
        /// <param name="ids">ID 集合</param>
        public static List<Profile> Remove(IEnumerable<int> ids)
        {
            var clt = s_ins._client;
            var lst = default(List<Profile>);
            Application.Current.Dispatcher.Invoke(() =>
            {
                lst = clt._Remove(r => ids.Contains(r.ID) == false);
                foreach (var r in lst)
                    if (r.Hint > 0)
                        SetRecent(r);
            });
            return lst;
        }

        /// <summary>
        /// 设置组标签 不区分大小写 以空格分开 超出个数限制返回 false
        /// </summary>
        public static bool SetGroupLabels(string args)
        {
            var val = (args ?? string.Empty).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var tmp = (from s in val select new { Value = s, Hash = s.ToLower().GetHashCode() | 1 << 31 }).ToList();
            var kvs = tmp._Distinct((a, b) => a.Hash == b.Hash);
            if (kvs.Count > Links.Group)
                return false;
            var ids = from i in kvs select i.Hash;
            s_ins._grouptags = args;
            s_ins._groupids = ids;
            var gro = s_ins._groups;
            Posters.UserGroups();
            Application.Current.Dispatcher.Invoke(() =>
            {
                var lst = gro._Remove(r => ids.Contains(r.ID) == false);
                foreach (var r in lst)
                    if (r.Hint > 0)
                        SetRecent(r);
                var add = from r in kvs where gro.FirstOrDefault(t => t.ID == r.Hash) == null select r;
                foreach (var i in add)
                {
                    var pro = Query(i.Hash, true);
                    pro.Name = i.Value;
                    pro.Text = i.Hash.ToString("X8");
                    gro.Add(pro);
                }
            });
            return true;
        }

        /// <summary>
        /// 设置当前联系人
        /// </summary>
        public static void SetInscope(Profile profile)
        {
            if (profile == null)
            {
                s_ins._inscope = null;
                return;
            }

            profile.Hint = 0;
            if (ReferenceEquals(profile, s_ins._inscope))
                return;

            s_ins._inscope = profile;
            s_ins._inscopechanged?.Invoke(s_ins, new EventArgs());
        }

        /// <summary>
        /// 添加联系人到最近列表
        /// </summary>
        public static void SetRecent(Profile profile)
        {
            var rec = s_ins._recent;
            for (var i = 0; i < rec.Count; i++)
            {
                if (rec[i].ID == profile.ID)
                {
                    if (ReferenceEquals(rec[i], profile))
                        return;
                    rec.RemoveAt(i);
                    break;
                }
            }
            rec.Add(profile);
        }

        [AutoLoad(16, AutoLoadFlag.OnLoad)]
        public static void Load()
        {
            try
            {
                s_ins._local.ID = int.Parse(Options.GetOption(_KeyCode, new Random().Next(1, int.MaxValue).ToString()));
                s_ins._local.Name = Options.GetOption(_KeyName);
                s_ins._local.Text = Options.GetOption(_KeyText);
                var lbs = Options.GetOption(_KeyLabel);
                SetGroupLabels(lbs);
                var pth = Options.GetOption(_KeyImage);
                if (pth == null)
                    return;
                var buf = Caches.ImageSquare(pth);
                var sha = Caches.SetBuffer(buf, false);
                s_ins._local.Image = Caches.GetPath(sha);
                s_ins._imagesource = pth;
                s_ins._imagebuffer = buf;
            }
            catch (Exception ex)
            {
                Log.Err(ex);
                return;
            }
        }

        [AutoLoad(8, AutoLoadFlag.OnExit)]
        public static void Save()
        {
            Options.SetOption(_KeyCode, s_ins._local.ID.ToString());
            Options.SetOption(_KeyName, s_ins._local.Name);
            Options.SetOption(_KeyText, s_ins._local.Text);
            Options.SetOption(_KeyImage, s_ins._imagesource);
            Options.SetOption(_KeyLabel, s_ins._grouptags);
        }
    }
}
