using Messenger.Modules;
using System;

namespace Messenger.Models
{
    /// <summary>
    /// 消息记录
    /// </summary>
    public class Packet
    {
        private int _target = 0;
        private int _source = 0;
        private int _groups = 0;
        private string _image = null;
        private object _value = null;
        private Profile _profile = null;
        private DateTime _time = DateTime.Now;
        private string _path = null;

        /// <summary>
        /// 收信人编号
        /// </summary>
        public int Target { get => _target; set => _target = value; }

        /// <summary>
        /// 发信人编号
        /// </summary>
        public int Source { get => _source; set => _source = value; }

        /// <summary>
        /// 分组编号
        /// </summary>
        public int Groups { get => _groups; set => _groups = value; }

        /// <summary>
        /// 消息类型
        /// </summary>
        public string Path { get => _path; set => _path = value; }

        /// <summary>
        /// 底层数据 (怎么解读取决于 <see cref="Path"/>)
        /// </summary>
        public object Value { get => _value; set => _value = value; }

        /// <summary>
        /// 消息时间
        /// </summary>
        public DateTime Timestamp { get => _time; set => _time = value; }

        /// <summary>
        /// 消息文本
        /// </summary>
        public string MessageText
        {
            get
            {
                if (_value is string str && _path == "text")
                    return str;
                return null;
            }
        }

        /// <summary>
        /// 图像路径
        /// </summary>
        public string MessageImage
        {
            get
            {
                if (_image == null && _value is string str && _path == "image")
                    _image = Caches.GetPath(str);
                return _image;
            }
        }

        /// <summary>
        /// 用户信息
        /// </summary>
        public Profile Profile
        {
            get
            {
                if (_profile == null)
                    _profile = Profiles.Query(_source, true);
                return _profile;
            }
        }
    }
}
