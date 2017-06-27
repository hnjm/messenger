using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Collections.Generic;
using System;

namespace Messenger.Foundation
{
    /// <summary>
    /// XML 序列化辅助类 (单例模式)
    /// </summary>
    public class Xml
    {
        private object _locker = new object();
        private Dictionary<Type, XmlSerializer> _dictionary = new Dictionary<Type, XmlSerializer>();
        private Xml() { }

        private static Xml _instance = new Xml();

        public static XmlSerializer GetSerializer(Type type)
        {
            lock (_instance._locker)
            {
                if (_instance._dictionary.ContainsKey(type) == false)
                    _instance._dictionary.Add(type, new XmlSerializer(type));
                return _instance._dictionary[type];
            }
        }

        /// <summary>
        /// 序列化源对象到 XML
        /// </summary>
        /// <param name="src">源对象</param>
        /// <returns>XML 字节流数据</returns>
        public static byte[] Serialize(object src)
        {
            using (var str = new MemoryStream())
            {
                Serialize(str, src);
                return str.ToArray();
            }
        }

        /// <summary>
        /// 序列化源对象到 XML
        /// </summary>
        /// <param name="str">目标流</param>
        /// <param name="src">源对象</param>
        /// <param name="ignoreomit">是否忽略 XML 头</param>
        /// <param name="indent">是否缩进</param>
        public static void Serialize(Stream str, object src, bool ignoreomit = true, bool indent = false)
        {
            var xns = new XmlSerializerNamespaces();
            xns.Add(string.Empty, string.Empty);
            var set = new XmlWriterSettings() { OmitXmlDeclaration = ignoreomit, Indent = indent };
            var wtr = XmlWriter.Create(str, set);
            GetSerializer(src.GetType()).Serialize(wtr, src, xns);
        }

        /// <summary>
        /// 反序列化 XML 到目标对象
        /// </summary>
        /// <typeparam name="T">目标对象类型</typeparam>
        /// <param name="buffer">XML 字节流数据</param>
        /// <param name="index">字节流起始索引</param>
        /// <param name="count">数据长度</param>
        /// <returns>目标对象</returns>
        public static T Deserialize<T>(byte[] buffer, int index, int count)
        {
            using (var ms = new MemoryStream(buffer, index, count))
            {
                return (T)GetSerializer(typeof(T)).Deserialize(ms);
            }
        }

        /// <summary>
        /// 反序列化 XML 到目标对象
        /// </summary>
        /// <typeparam name="T">目标对象类型</typeparam>
        /// <param name="buffer">XML 字节流数据</param>
        /// <returns>目标对象</returns>
        public static T Deserialize<T>(byte[] buffer)
        {
            using (var ms = new MemoryStream(buffer))
            {
                return (T)GetSerializer(typeof(T)).Deserialize(ms);
            }
        }

        /// <summary>
        /// 反序列化 XML 到目标对象
        /// </summary>
        /// <typeparam name="T">目标对象类型</typeparam>
        /// <param name="buffer">XML 字节流数据</param>
        /// <returns>目标对象</returns>
        public static T Deserialize<T>(Stream ms)
        {
            return (T)GetSerializer(typeof(T)).Deserialize(ms);
        }
    }
}
