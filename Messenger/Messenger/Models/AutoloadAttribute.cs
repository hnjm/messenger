using System;

namespace Messenger.Models
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AutoLoadAttribute : AutoAttribute
    {
        public AutoLoadAttribute(int level) : base(level) { }
    }
}
