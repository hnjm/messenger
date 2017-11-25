using Messenger.Modules;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Models
{
    internal abstract class ShareBasic : INotifyPropertyChanging, INotifyPropertyChanged
    {
        internal class Tick
        {
            public long Time = 0;
            public long Position = 0;
            public double Speed = 0;
        }

        private const int _tickLimit = 10;

        private const int _delay = 500;

        private static Action s_action = null;

        private static Stopwatch s_watch = new Stopwatch();

        private static Task s_task = new Task(async () =>
        {
            while (true)
            {
                s_action?.Invoke();
                await Task.Delay(_delay);
            }
        });

        /// <summary>
        /// 注册以便实时计算传输进度 (当 <see cref="IsClosed"/> 为真时自动取消注册)
        /// </summary>
        protected void Register() => s_action += _Refresh;

        #region PropertyChange
        public event PropertyChangingEventHandler PropertyChanging;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void PropertyChange<T>(ref T source, T target, [CallerMemberName] string name = null)
        {
            var eva = new PropertyChangingEventArgs(name);
            PropertyChanging?.Invoke(this, eva);

            if (Equals(source, target))
                return;
            source = target;

            var evb = new PropertyChangedEventArgs(name);
            PropertyChanged?.Invoke(this, evb);
        }

        protected void OnPropertyChanged(string str = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(str ?? string.Empty));
        #endregion

        private double _speed = 0;
        private double _progress = 0;
        private TimeSpan _remain = TimeSpan.Zero;
        private readonly List<Tick> _ticks = new List<Tick>();

        protected abstract int ID { get; }

        public abstract long Length { get; }

        public abstract bool IsBatch { get; }

        public abstract bool IsClosed { get; }

        public abstract string Name { get; }

        public abstract string Path { get; }

        public abstract long Position { get; }

        public abstract ShareStatus Status { get; }

        public Profile Profile => Profiles.Query(ID, true);

        public TimeSpan Remain => _remain;

        public double Speed => _speed;

        public double Progress => IsBatch ? 100 : _progress;

        private void _Refresh()
        {
            var unreg = IsClosed;

            var avg = _AverageSpeed();
            _speed = avg * 1000; // 毫秒 -> 秒

            if (IsBatch == false)
            {
                _remain = (avg > 0 && Position > 0) ? TimeSpan.FromMilliseconds((Length - Position) / avg) : TimeSpan.Zero;
                _progress = (Length > 0)
                    ? (100.0 * Position / Length)
                    : (Status == ShareStatus.成功)
                        ? 100
                        : 0;

                OnPropertyChanged(nameof(Remain));
                OnPropertyChanged(nameof(Progress));
            }

            OnPropertyChanged(nameof(Speed));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Position));

            // 确保 IsClosed 为真后再计算一次
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
            if (_ticks.Count > _tickLimit)
                _ticks.RemoveRange(0, _ticks.Count - _tickLimit);
            var sum = 0.0;
            foreach (var i in _ticks)
                sum += i.Speed;
            return sum / _ticks.Count;
        }

        [AutoLoad(16, AutoLoadFlags.OnLoad)]
        public static void Load()
        {
            s_task.Start();
            s_watch.Start();
        }
    }
}
