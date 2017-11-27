using System;

namespace Messenger.Models
{
    /// <summary>
    /// 标注有此属性的静态函数将根据指定条件自动执行
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class LoaderAttribute : Attribute
    {
        private int _lev = 0;

        private LoaderFlags _flag = LoaderFlags.None;

        public int Level => _lev;

        public LoaderFlags Flag => _flag;

        public LoaderAttribute(int level, LoaderFlags flag)
        {
            _lev = level;
            _flag = flag;
        }
    }
}
