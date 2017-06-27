using System;
using System.ComponentModel;
using System.Text;

namespace Messenger
{
    /// <summary>
    /// 提供 Unicode 表情文字
    /// </summary>
    class ModuleSymbols
    {
        private BindingList<string> list = null;
        private object locker = new object();

        private static ModuleSymbols instance = new ModuleSymbols();

        public static BindingList<string> List
        {
            get
            {
                lock (instance.locker)
                {
                    if (instance.list == null)
                    {
                        var lst = new BindingList<string>();
                        var offset = 0x1F600;
                        for (var i = 0; i < 69; i++)
                        {
                            var buf = BitConverter.GetBytes(offset + i);
                            var str = Encoding.UTF32.GetString(buf);
                            lst.Add(str);
                        }
                        instance.list = lst;
                    }
                }
                return instance.list;
            }
        }
    }
}
