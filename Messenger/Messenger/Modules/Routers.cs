using Messenger.Foundation;
using Messenger.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Messenger.Modules
{
    internal class Routers
    {
        private class _Record
        {
            public Func<Router> Construct = null;
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
                if (t.IsSubclassOf(typeof(Router)) == false)
                    continue;
                var att = t.GetCustomAttributes(typeof(HandlerAttribute)).FirstOrDefault() as HandlerAttribute;
                if (att == null)
                    continue;
                var met = t.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                foreach (var i in met)
                {
                    var atr = i.GetCustomAttributes(typeof(HandlerAttribute)).FirstOrDefault() as HandlerAttribute;
                    if (atr == null)
                        continue;
                    var act = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(t), i) as dynamic;
                    var con = (Func<Router>)Expression.Lambda(Expression.New(t)).Compile();
                    _dic.Add($"{att.Path}.{atr.Path}", new _Record() { Construct = con, Function = act });
                }
            }
        }

        public static void Handle(Router arg)
        {
            var rcd = s_ins._dic[arg.Path];
            var obj = rcd.Construct.Invoke();
            obj.Load(arg.Buffer);
            rcd.Function.Invoke((dynamic)obj);
        }

        [AutoLoad(1)]
        public static void Load()
        {
            if (s_ins != null)
                return;
            s_ins = new Routers();
            s_ins._Load();
        }
    }
}
