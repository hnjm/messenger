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

        private AutoLoadFlag _flag = AutoLoadFlag.None;

        public int Level => _lev;

        public AutoLoadFlag Flag => _flag;

        public AutoLoadAttribute(int level, AutoLoadFlag flag)
        {
            _lev = level;
            _flag = flag;
        }
    }
}
