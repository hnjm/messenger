using System;

namespace Messenger.Models
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AutoSaveAttribute : AutoAttribute
    {
        public AutoSaveAttribute(int level) : base(level) { }
    }
}
