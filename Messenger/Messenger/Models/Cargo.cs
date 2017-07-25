using Messenger.Foundation;
using Messenger.Modules;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace Messenger.Models
{
    /// <summary>
    /// 包装一个 <see cref="Foundation.Transport"/> 对象 并提供传输控制和界面绑定功能
    /// </summary>
    class Cargo : INotifyPropertyChanged
    {
        private class _Record
        {
            public long TimeTick = 0;
            public long Position = 0;
            public double Speed = 0;
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string str = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(str ?? string.Empty));
        #endregion

        private const int _Limit = 10;

        private int _tid = 0;
        private bool _final = false;
        private double _speed = 0;
        private double _progress = 0;
        private Profile _profile = null;
        private Transport _trans = null;
        private TimeSpan _remain = TimeSpan.Zero;
        private List<_Record> _list = new List<_Record>();

        public double Speed => _speed;
        public double Progress => _progress;
        public TimeSpan Remain => _remain;
        public Transport Transport => _trans;

        public Profile Profile
        {
            get
            {
                if (_profile == null)
                    _profile = Profiles.Query(_tid, true);
                return _profile;
            }
        }

        public Cargo(int id, Transport val)
        {
            _tid = id;
            _trans = val;

            if (val is Maker mak)
            {
                Interact.Requests += mak.Transport_Requests;
                val.Disposed += delegate
                    {
                        Interact.Requests -= mak.Transport_Requests;
                    };
            }
        }

        /// <summary>
        /// 计算和设置平均速度和进度
        /// </summary>
        public void Refresh(long tick)
        {
            if (_final == true)
                return;
            if (_trans.IsDisposed == true)
                _final = true;

            var spd = _AverageSpeed(tick);
            _speed = spd * 1000;  // 毫秒 -> 秒
            _remain = (spd > 0 && _trans.Position > 0) ? TimeSpan.FromMilliseconds((_trans.Length - _trans.Position) / spd) : TimeSpan.Zero;
            _progress = (_trans.Length > 0) ? (100.0 * _trans.Position / _trans.Length) : (_trans.Status == TransportStatus.成功 ? 100 : 0);

            OnPropertyChanged(nameof(Speed));
            OnPropertyChanged(nameof(Remain));
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(Transport));
        }

        private double _AverageSpeed(long tick)
        {
            var sum = 0.0;
            var cur = new _Record() { TimeTick = tick, Position = _trans.Position };
            if (_list.Count > 0)
            {
                var pre = _list[_list.Count - 1];
                var pos = cur.Position - pre.Position;
                var tim = cur.TimeTick - pre.TimeTick;
                cur.Speed = 1.0 * pos / tim;
            }
            _list.Add(cur);
            if (_list.Count > _Limit)
                _list.RemoveRange(0, _list.Count - _Limit);
            foreach (var h in _list)
                sum += h.Speed;
            return sum / _list.Count;
        }

        public void Start()
        {
            _trans.Start();
            Application.Current.Dispatcher.Invoke(() => Refresh(0L));
        }

        public void Close()
        {
            _trans.Dispose();
            Application.Current.Dispatcher.Invoke(() => Refresh(0L));
        }
    }
}
