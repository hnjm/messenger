using Messenger.Models;
using Mikodev.Network;
using System;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace Messenger.Modules
{
    internal class Options
    {
        public const string DefaultPath = nameof(Messenger) + ".xml";
        public const string DefaultRoot = "options-root";
        public const string DefaultHeader = "option";
        public const string DefaultKey = "key";
        public const string DefaultValue = "value";

        private XmlDocument _doc = null;

        private static Options s_ins = new Options();

        private Options() { }

        [AutoLoad(0, AutoLoadFlag.OnLoad)]
        public static void Load()
        {
            if (s_ins._doc != null)
                return;
            var fst = default(FileStream);
            var doc = new XmlDocument();

            try
            {
                fst = new FileStream(DefaultPath, FileMode.Open);
                if (fst.Length <= Links.BufferLimit)
                    doc.Load(fst);
                s_ins._doc = doc;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                s_ins._doc = new XmlDocument();
            }
            finally
            {
                fst?.Dispose();
            }
        }

        [AutoLoad(int.MaxValue, AutoLoadFlag.OnExit)]
        public static void Save()
        {
            var str = default(FileStream);
            var wtr = default(XmlWriter);
            var set = new XmlWriterSettings() { Indent = true };
            try
            {
                var doc = s_ins?._doc;
                if (doc == null)
                    return;
                str = new FileStream(DefaultPath, FileMode.Create);
                wtr = XmlWriter.Create(str, set);
                doc.Save(wtr);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
            finally
            {
                wtr?.Dispose();
                str?.Dispose();
            }
        }

        private static XmlElement _GetElement(string key)
        {
            var doc = s_ins._doc;
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
            var doc = s_ins?._doc;
            if (doc == null)
                throw new InvalidOperationException();
            try
            {
                var pth = $"/{DefaultRoot}/{DefaultHeader}[@{DefaultKey}=\"{key}\"]";
                if (doc.SelectSingleNode(pth) is XmlElement ele)
                {
                    if (ele.HasAttribute(DefaultValue))
                        return ele.GetAttribute(DefaultValue);
                    if (empty != null)
                        ele.SetAttribute(DefaultValue, empty);
                }
                else
                {
                    ele = _GetElement(key);
                    if (empty != null)
                        ele.SetAttribute(DefaultValue, empty);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
            return empty;
        }

        public static bool SetOption(string key, string value)
        {
            var doc = s_ins?._doc;
            if (doc == null)
                throw new InvalidOperationException();
            try
            {
                var pth = $"/{DefaultRoot}/{DefaultHeader}[@{DefaultKey}=\"{key}\"]";
                var ele = doc.SelectSingleNode(pth) as XmlElement;
                if (ele == null)
                    ele = _GetElement(key);
                if (value != null)
                    ele.SetAttribute(DefaultValue, value);
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return false;
            }
        }
    }
}
