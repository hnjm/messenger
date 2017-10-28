using System;

namespace Messenger.Models
{
    /// <summary>
    /// 标注有此属性的函数将在收到消息时自动匹配路径执行
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HandleAttribute : Attribute
    {
        private string _pth = null;

        public string Path => _pth;

        public HandleAttribute(string path) => _pth = path;
    }
}
