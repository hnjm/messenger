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
    internal class RouteModule
    {
        private class Controller
        {
            public Func<LinkPacket> Construct = null;
            public dynamic Function = null;
        }

        private static readonly RouteModule s_ins = new RouteModule();

        private readonly Dictionary<string, Controller> _dic = new Dictionary<string, Controller>();

        private RouteModule() { }

        private void _Load()
        {
            /* 利用反射识别所有控制器
             * 同时构建表达式以便提升运行速度 */
            var ass = typeof(RouteModule).Assembly;
            foreach (var t in ass.GetTypes())
            {
                if (t.IsSubclassOf(typeof(LinkPacket)) == false)
                    continue;
                var att = t.GetCustomAttributes(typeof(RouteAttribute)).FirstOrDefault() as RouteAttribute;
                if (att == null)
                    continue;
                var met = t.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                foreach (var i in met)
                {
                    var atr = i.GetCustomAttributes(typeof(RouteAttribute)).FirstOrDefault() as RouteAttribute;
                    if (atr == null)
                        continue;
                    // 构建表达式
                    var act = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(t), i) as dynamic;
                    var con = (Func<LinkPacket>)Expression.Lambda(Expression.New(t)).Compile();
                    _dic.Add($"{att.Path}.{atr.Path}", new Controller() { Construct = con, Function = act });
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
            s_ins._Load();
        }
    }
}
