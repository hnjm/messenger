using Messenger.Models;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.IO;
using System.Xml;

namespace Messenger.Modules
{
    /// <summary>
    /// 管理用户设置
    /// </summary>
    internal class Options
    {
        private const string _Path = nameof(Messenger) + ".opt";
        private const string _Root = "options-root";
        private const string _Header = "option";
        private const string _Key = "key";
        private const string _Value = "value";

        private XmlDocument _doc = null;

        private static Options s_ins = new Options();

        private Options() { }

        [AutoLoad(0, AutoLoadFlags.OnLoad)]
        public static void Load()
        {
            if (s_ins._doc != null)
                return;
            var fst = default(FileStream);
            var doc = new XmlDocument();

            try
            {
                fst = new FileStream(_Path, FileMode.Open);
                if (fst.Length <= Links.BufferLengthLimit)
                    doc.Load(fst);
                s_ins._doc = doc;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                s_ins._doc = new XmlDocument();
            }
            finally
            {
                fst?.Dispose();
            }
        }

        [AutoLoad(int.MaxValue, AutoLoadFlags.OnExit)]
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
                str = new FileStream(_Path, FileMode.Create);
                wtr = XmlWriter.Create(str, set);
                doc.Save(wtr);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
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
            var roo = doc.SelectSingleNode($"/{_Root}") as XmlElement;
            if (roo == null)
            {
                roo = doc.CreateElement(_Root);
                doc.AppendChild(roo);
            }
            var ele = doc.CreateElement(_Header);
            ele.SetAttribute(_Key, key);
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
                var pth = $"/{_Root}/{_Header}[@{_Key}=\"{key}\"]";
                if (doc.SelectSingleNode(pth) is XmlElement ele)
                {
                    if (ele.HasAttribute(_Value))
                        return ele.GetAttribute(_Value);
                    if (empty != null)
                        ele.SetAttribute(_Value, empty);
                }
                else
                {
                    ele = _GetElement(key);
                    if (empty != null)
                        ele.SetAttribute(_Value, empty);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
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
                var pth = $"/{_Root}/{_Header}[@{_Key}=\"{key}\"]";
                var ele = doc.SelectSingleNode(pth) as XmlElement;
                if (ele == null)
                    ele = _GetElement(key);
                if (value != null)
                    ele.SetAttribute(_Value, value);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }
    }
}
