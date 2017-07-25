using System;

namespace Messenger.Models
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AutoAttribute : Attribute
    {
        private int _lev = 0;

        public int Level => _lev;

        public AutoAttribute(int level) => _lev = level;
    }
}
