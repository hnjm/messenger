using Messenger.Models;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Messenger.Modules
{
    /// <summary>
    /// 处理消息, 并分发给各个消息处理函数
    /// </summary>
    internal class Routers
    {
        private class _Record
        {
            public Func<LinkPacket> Construct = null;
            public dynamic Function = null;
        }

        private static Routers s_ins = null;

        private Dictionary<string, _Record> _dic = new Dictionary<string, _Record>();

        private Routers() { }

        private void _Load()
        {
            var ass = typeof(Routers).Assembly;
            foreach (var t in ass.GetTypes())
            {
                if (t.IsSubclassOf(typeof(LinkPacket)) == false)
                    continue;
                var att = t.GetCustomAttributes(typeof(HandleAttribute)).FirstOrDefault() as HandleAttribute;
                if (att == null)
                    continue;
                var met = t.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                foreach (var i in met)
                {
                    var atr = i.GetCustomAttributes(typeof(HandleAttribute)).FirstOrDefault() as HandleAttribute;
                    if (atr == null)
                        continue;
                    var act = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(t), i) as dynamic;
                    var con = (Func<LinkPacket>)Expression.Lambda(Expression.New(t)).Compile();
                    _dic.Add($"{att.Path}.{atr.Path}", new _Record() { Construct = con, Function = act });
                }
            }
        }

        public static void Handle(LinkPacket arg)
        {
            if (s_ins._dic.TryGetValue(arg.Path, out var rcd))
            {
                var obj = rcd.Construct.Invoke();
                obj.LoadValue(arg.Buffer);
                rcd.Function.Invoke((dynamic)obj);
            }
            else
            {
                Log.Notice($"Path \"{arg.Path}\" not supported.");
            }
        }

        [AutoLoad(1, AutoLoadFlags.OnLoad)]
        public static void Load()
        {
            if (s_ins != null)
                return;
            s_ins = new Routers();
            s_ins._Load();
        }
    }
}
