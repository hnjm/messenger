using Messenger.Foundation.Extensions;
using Messenger.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace Messenger.Modules
{
    class Profiles : INotifyPropertyChanged
    {
        public const int GroupLimit = 32;
        public const string KeyCode = "profile-code";
        public const string KeyName = "profile-name";
        public const string KeyText = "profile-text";
        public const string KeyImage = "profile-image";
        public const string KeyLabel = "profile-groups";

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
        private List<WeakReference<Profile>> _spaces = new List<WeakReference<Profile>>();
        private Profile _local = new Profile();
        private Profile _inscope = null;
        private event EventHandler _InscopeChanged = null;

        public bool HasClient
        {
            get => _hasclient;
            set
            {
                _hasclient = value;
                OnPropertyChanged(nameof(HasClient));
            }
        }

        public bool HasGroups
        {
            get => _hasgroups;
            set
            {
                _hasgroups = value;
                OnPropertyChanged(nameof(HasGroups));
            }
        }

        public bool HasRecent
        {
            get => _hasrecent;
            set
            {
                _hasrecent = value;
                OnPropertyChanged(nameof(HasRecent));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private Profiles()
        {
            Profile.StaticPropertyChanged += (s, e) =>
                {
                    if (e.PropertyName.Equals(nameof(Profile.Hint)))
                        OnChanged();
                };
            _client.ListChanged += (s, e) => OnChanged();
            _groups.ListChanged += (s, e) => OnChanged();
            _recent.ListChanged += (s, e) => OnChanged();
        }

        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// 重新计算未读消息数量
        /// </summary>
        private void OnChanged()
        {
            var cli = _client.Sum(r => r.Hint);
            var gro = _groups.Sum(r => r.Hint);
            var rec = _recent.Sum(r => (r.Hint < 1 || _client.FirstOrDefault(t => t.ID == r.ID) != null || _groups.FirstOrDefault(t => t.ID == r.ID) != null) ? 0 : r.Hint);
            HasClient = cli > 0;
            HasGroups = gro > 0;
            HasRecent = rec > 0;
        }

        // ---------- ---------- ---------- ---------- ---------- ---------- ---------- ---------- ---------- ---------- 

        private static Profiles instance = new Profiles();

        public static Profiles Instance => instance;
        public static Profile Current => instance._local;
        public static Profile Inscope => instance._inscope;
        public static string GroupLabels => instance._grouptags;
        public static string ImageSource { get => instance._imagesource; set => instance._imagesource = value; }
        public static byte[] ImageBuffer { get => instance._imagebuffer; set => instance._imagebuffer = value; }
        public static IEnumerable<int> GroupIDs => instance._groupids;
        public static BindingList<Profile> RecentList => instance._recent;
        public static BindingList<Profile> ClientList => instance._client;
        public static BindingList<Profile> GroupsList => instance._groups;
        public static event EventHandler InscopeChanged { add => instance._InscopeChanged += value; remove => instance._InscopeChanged -= value; }

        /// <summary>
        /// 添加或更新用户信息 (添加返回真, 更新返回假)
        /// </summary>
        public static void Insert(Profile profile)
        {
            var clt = instance._client;
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
            if (id == instance._local.ID)
                return instance._local;
            instance._spaces.Remove((r) => r.TryGetTarget(out var _) == false);
            foreach (var i in instance._spaces)
                if (i.TryGetTarget(out var pro) && pro.ID == id)
                    return pro;
            var res = instance._client.Concat(instance._groups).Concat(instance._recent);
            var val = res.FirstOrDefault(t => t.ID == id);
            if (val != null)
                return val;
            if (create == false)
                return null;
            var tmp = new Profile() { ID = id, Name = $"未知 [{id}]" };
            instance._spaces.Add(new WeakReference<Profile>(tmp));
            return tmp;
        }

        /// <summary>
        /// 移除所有 ID 不在给定集合的项目 并把含有未读消息的项目添加到最近列表
        /// </summary>
        /// <param name="ids">ID 集合</param>
        public static IList<Profile> Remove(IEnumerable<int> ids)
        {
            var clt = instance._client;
            var lst = new List<Profile>() as IList<Profile>;
            Application.Current.Dispatcher.Invoke(() =>
                {
                    lst = clt.Remove(r => ids.Contains(r.ID) == false);
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
            var kvs = tmp.Distinct((a, b) => a.Hash == b.Hash).ToList();
            if (kvs.Count > GroupLimit)
                return false;
            var ids = from i in kvs select i.Hash;
            instance._grouptags = args;
            instance._groupids = ids;
            var gro = instance._groups;
            Posters.UserGroups();
            Application.Current.Dispatcher.Invoke(() =>
                {
                    var lst = gro.Remove(r => ids.Contains(r.ID) == false);
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
                instance._inscope = null;
                return;
            }

            profile.Hint = 0;
            if (ReferenceEquals(profile, instance._inscope))
                return;

            instance._inscope = profile;
            instance._InscopeChanged?.Invoke(instance, new EventArgs());
        }

        /// <summary>
        /// 添加联系人到最近列表
        /// </summary>
        public static void SetRecent(Profile profile)
        {
            var rec = instance._recent;
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

        [AutoLoad(16)]
        public static void Load()
        {
            try
            {
                instance._local.ID = int.Parse(Options.GetOption(KeyCode, new Random().Next(1, int.MaxValue).ToString()));
                instance._local.Name = Options.GetOption(KeyName);
                instance._local.Text = Options.GetOption(KeyText);
                var lbs = Options.GetOption(KeyLabel);
                SetGroupLabels(lbs);
                var pth = Options.GetOption(KeyImage);
                if (pth == null)
                    return;
                var buf = Caches.ImageSquare(pth);
                var sha = Caches.SetBuffer(buf, false);
                instance._local.Image = Caches.GetPath(sha);
                instance._imagesource = pth;
                instance._imagebuffer = buf;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return;
            }
        }

        [AutoSave(8)]
        public static void Save()
        {
            Options.SetOption(KeyCode, instance._local.ID.ToString());
            Options.SetOption(KeyName, instance._local.Name);
            Options.SetOption(KeyText, instance._local.Text);
            Options.SetOption(KeyImage, instance._imagesource);
            Options.SetOption(KeyLabel, instance._grouptags);
        }
    }
}
