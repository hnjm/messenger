using Messenger.Foundation;
using System;
using System.IO;
using System.Threading;
using System.Xml;

namespace Messenger.Modules
{
    class Options
    {
        /// <summary>
        /// 允许载入内存的配置文件大小限制
        /// </summary>
        public const long DefaultLengthLimit = 32768;
        public const string DefaultPath = nameof(Messenger) + ".option";
        public const string DefaultRoot = "options-root";
        public const string DefaultHeader = "option";
        public const string DefaultKey = "key";
        public const string DefaultValue = "value";

        private object locker = new object();
        private XmlDocument document = null;

        private static Options instance = null;

        private Options() { }

        public static void Load()
        {
            if (instance == null)
                Interlocked.CompareExchange(ref instance, new Options(), null);
            lock (instance.locker)
            {
                if (instance.document != null)
                    return;
                var fst = default(FileStream);
                try
                {
                    fst = new FileStream(DefaultPath, FileMode.Open);
                    if (fst.Length > DefaultLengthLimit == false)
                    {
                        var doc = new XmlDocument();
                        doc.Load(fst);
                        instance.document = doc;
                    }
                }
                catch { }
                finally { fst?.Dispose(); }
                if (instance.document == null)
                    instance.document = new XmlDocument();
                return;
            }
        }

        public static void Save()
        {
            var str = default(FileStream);
            var wtr = default(XmlWriter);
            var set = new XmlWriterSettings() { Indent = true };
            try
            {
                var doc = instance?.document;
                if (doc == null)
                    return;
                str = new FileStream(DefaultPath, FileMode.Create);
                wtr = XmlWriter.Create(str, set);
                doc.Save(wtr);
            }
            catch (Exception ex)
            {
                Log.E(nameof(Options), ex, "保存配置出错");
            }
            finally
            {
                wtr?.Dispose();
                str?.Dispose();
            }
        }

        private static XmlElement GetElement(string key)
        {
            var doc = instance.document;
            var roo = doc.SelectSingleNode($"/{DefaultRoot}") as XmlElement;
            if (roo == null)
            {
                roo = doc.CreateElement(DefaultRoot);
                doc.AppendChild(roo);
            }
            var ele = doc.CreateElement(DefaultHeader);
            ele.SetAttribute(DefaultKey, key);
            roo.AppendChild(ele);
            return ele;
        }

        public static string GetOption(string key, string empty = null)
        {
            var doc = instance?.document;
            if (doc == null)
                throw new InvalidOperationException();
            try
            {
                var pth = $"/{DefaultRoot}/{DefaultHeader}[@{DefaultKey}=\"{key}\"]";
                var ele = doc.SelectSingleNode(pth) as XmlElement;
                if (ele != null)
                {
                    if (ele.HasAttribute(DefaultValue))
                        return ele.GetAttribute(DefaultValue);
                    if (empty != null)
                        ele.SetAttribute(DefaultValue, empty);
                }
                else
                {
                    ele = GetElement(key);
                    if (empty != null)
                        ele.SetAttribute(DefaultValue, empty);
                }
            }
            catch (Exception ex)
            {
                Log.E(nameof(Options), ex, "读取配置出错");
            }
            return empty;
        }

        public static bool SetOption(string key, string value)
        {
            var doc = instance?.document;
            if (doc == null)
                throw new InvalidOperationException();

            try
            {
                var pth = $"/{DefaultRoot}/{DefaultHeader}[@{DefaultKey}=\"{key}\"]";
                var ele = doc.SelectSingleNode(pth) as XmlElement;
                if (ele == null)
                    ele = GetElement(key);
                if (value != null)
                    ele.SetAttribute(DefaultValue, value);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
