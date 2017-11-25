using System;

namespace Messenger.Models
{
    /// <summary>
    /// 标注有此属性的静态函数将根据指定条件自动执行
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AutoLoadAttribute : Attribute
    {
        private int _lev = 0;

        private AutoLoadFlags _flag = AutoLoadFlags.None;

        public int Level => _lev;

        public AutoLoadFlags Flag => _flag;

        public AutoLoadAttribute(int level, AutoLoadFlags flag)
        {
            _lev = level;
            _flag = flag;
        }
    }
}
