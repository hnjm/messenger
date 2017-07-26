using System;

namespace Messenger.Models
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HandlerAttribute : Attribute
    {
        private string _pth = null;

        public string Path => _pth;

        public HandlerAttribute(string path) => _pth = path;
    }
}
