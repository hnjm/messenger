using Messenger.Foundation;
using System;
using System.Net;
using System.Xml.Serialization;

namespace Messenger
{
    [Serializable]
    [XmlRoot(ElementName = ServerRoot)]
    public class ItemServer : PacketServer
    {
        /// <summary>
        /// 访问延迟
        /// </summary>
        [XmlIgnore]
        public long Delay { get; set; } = 0;

        /// <summary>
        /// IP 地址
        /// </summary>
        [XmlIgnore]
        public IPAddress Address { get; set; } = null;

        /// <summary>
        /// 依据 IP 地址和端口比较两个对象
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj == this)
                return true;
            var info = obj as ItemServer;
            if (info == null)
                return false;
            if (Port != info.Port)
                return false;
            if (Address == info.Address)
                return true;
            if (Address == null || info.Address == null)
                return false;
            return Address.Equals(info.Address);
        }

        /// <summary>
        /// 调用 IPEndPoint 的 GetHashCode 方法
        /// </summary>
        public override int GetHashCode()
        {
            return Address == null ? 0 : new IPEndPoint(Address, Port).GetHashCode();
        }
    }
}
