using System;
using System.Xml.Serialization;

namespace Messenger.Foundation
{
    /// <summary>
    /// 服务器信息
    /// </summary>
    [Serializable]
    [XmlRoot(ElementName = ServerRoot)]
    public class PacketServer
    {
        public const string ServerRoot = "serverinfo";

        /// <summary>
        /// 端口
        /// </summary>
        [XmlElement(ElementName = "port")]
        public int Port { get; set; }

        /// <summary>
        /// 服务器当前连接的客户端数
        /// </summary>
        [XmlElement(ElementName = "count")]
        public int Count { get; set; }

        /// <summary>
        /// 服务器最大客户端数
        /// </summary>
        [XmlElement(ElementName = "max")]
        public int CountLimited { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [XmlElement(ElementName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// 协议字符串
        /// </summary>
        [XmlAttribute(AttributeName = "protocol")]
        public string Protocol { get; set; }
    }
}
