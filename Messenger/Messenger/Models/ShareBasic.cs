using Messenger.Modules;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Messenger.Models
{
    public abstract class ShareBasic : INotifyPropertyChanged
    {
        internal class Tick
        {
            public long Time = 0;
            public long Position = 0;
            public double Speed = 0;
        }

        /// <summary>
        /// 历史记录上限
        /// </summary>
        private const int _tickLimit = 10;

        private const int _delay = 300;

        private static Action s_action = null;

        private static readonly Stopwatch s_watch = new Stopwatch();

        private static readonly Task s_task = new Task(async () =>
        {
            while (true)
            {
                s_action?.Invoke();
                await Task.Delay(_delay);
            }
        });

        /// <summary>
        /// 注册以便实时计算传输进度 (当 <see cref="IsDisposed"/> 为真时自动取消注册)
        /// </summary>
        protected void Register() => s_action += _Refresh;

        #region PropertyChange
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string str = null) =>
            Application.Current.Dispatcher.Invoke(() =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(str ?? string.Empty)));
        #endregion

        private double _speed = 0;
        private double _progress = 0;
        private TimeSpan _remain = TimeSpan.Zero;
        private readonly List<Tick> _ticks = new List<Tick>();

        protected abstract int ID { get; }

        public abstract long Length { get; }

        public abstract bool IsBatch { get; }

        public abstract bool IsDisposed { get; }

        public abstract string Name { get; }

        public abstract string Path { get; }

        public abstract long Position { get; }

        public abstract ShareStatus Status { get; }

        public Profile Profile => ProfileModule.Query(ID, true);

        public TimeSpan Remain => _remain;

        public double Speed => _speed;

        public double Progress => _progress;

        private void _Refresh()
        {
            var unreg = IsDisposed;

            var avg = _AverageSpeed();
            _speed = avg * 1000; // 毫秒 -> 秒
            _progress = (Length > 0)
                ? (100.0 * Position / Length)
                : (Status & ShareStatus.终止) == 0
                    ? 0
                    : 100;

            if (IsBatch == false)
            {
                var spa = (avg > 0 && Position > 0)
                    ? TimeSpan.FromMilliseconds((Length - Position) / avg)
                    : TimeSpan.Zero;
                // 移除毫秒部分
                _remain = new TimeSpan(spa.Days, spa.Hours, spa.Minutes, spa.Seconds);
                OnPropertyChanged(nameof(Remain));
            }

            OnPropertyChanged(nameof(Speed));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Position));
            OnPropertyChanged(nameof(Progress));

            // 确保 IsDisposed 为真后再计算一次
            if (unreg)
            {
                s_action -= _Refresh;
            }
        }

        private double _AverageSpeed()
        {
            var tic = s_watch.ElapsedMilliseconds;
            var cur = new Tick { Time = tic, Position = Position };
            if (_ticks.Count > 0)
            {
                var pre = _ticks[_ticks.Count - 1];
                var pos = cur.Position - pre.Position;
                var sub = cur.Time - pre.Time;
                cur.Speed = 1.0 * pos / sub;
            }
            _ticks.Add(cur);
            // 计算最近几条记录的平均速度
            if (_ticks.Count > _tickLimit)
                _ticks.RemoveRange(0, _ticks.Count - _tickLimit);
            return _ticks.Average(r => r.Speed);
        }

        [AutoLoad(16, AutoLoadFlags.OnLoad)]
        public static void Load()
        {
            s_task.Start();
            s_watch.Start();
        }
    }
}
