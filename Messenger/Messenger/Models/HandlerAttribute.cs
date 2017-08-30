using System;

namespace Messenger.Models
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HandleAttribute : Attribute
    {
        private string _pth = null;

        public string Path => _pth;

        public HandleAttribute(string path) => _pth = path;
    }
}
