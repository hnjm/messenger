using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Messenger.Foundation
{
    /// <summary>
    /// 文件传输状态
    /// </summary>
    public enum TransportStatus : int
    {
        默认 = 0,
        等待 = 1,
        运行 = 2,
        暂停 = 4,
        中断 = 8 | 终态,
        取消 = 16 | 终态,
        成功 = 32 | 终态,

        终态 = 1 << 31,
    }

    /// <summary>
    /// 文件信息及发送者地址
    /// </summary>
    [Serializable]
    [XmlRoot(ElementName = "fileinfo")]
    public class TransportHeader
    {
        [XmlElement(ElementName = "guid")]
        public Guid Key { get; set; }

        [XmlElement(ElementName = "name")]
        public string FileName { get; set; } = null;

        [XmlElement(ElementName = "size")]
        public long FileLength { get; set; } = 0;

        [XmlElement(ElementName = "addr")]
        public List<string> EndPoints { get; set; } = null;
    }
}
