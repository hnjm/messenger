using Messenger.Models;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Messenger.Modules
{
    /// <summary>
    /// 负责管理图片缓存 (被动初始化)
    /// </summary>
    internal class Caches
    {
        private const string _CacheFolder = nameof(Caches);
        private const string _CacheExtension = ".jpg";

        private const int _Limit = 384;
        private const float _Density = 96;
        private const string _KeyCache = "cache-dir";
        private const string _KeyLimit = "cache-limit";
        private const string _KeyDensity = "cache-density";

        private int _imgLimit = _Limit;
        private float _imgdpi = _Density;
        private string _dir = _CacheFolder;

        private static Caches s_ins = new Caches();

        private Caches() { }

        [AutoLoad(16, AutoLoadFlag.OnLoad)]
        public static void Load()
        {
            try
            {
                s_ins._dir = Options.GetOption(_KeyCache, _CacheFolder);
                s_ins._imgLimit = int.Parse(Options.GetOption(_KeyLimit, _Limit.ToString()));
                s_ins._imgdpi = float.Parse(Options.GetOption(_KeyDensity, _Density.ToString()));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        public static string GetCode(byte[] buffer)
        {
            using (var sha = new SHA1Managed())
            {
                var buf = sha.ComputeHash(buffer);
                var str = buf.Aggregate(new StringBuilder(), (l, r) => l.AppendFormat("{0:x2}", r));
                return str.ToString();
            }
        }

        public static string GetPath(string code)
        {
            var dir = new DirectoryInfo(s_ins._dir);
            var pth = Path.Combine(dir.FullName, code + _CacheExtension);
            return pth;
        }

        public static string SetBuffer(byte[] buffer, bool returnPath, bool nothrow = true)
        {
            var fst = default(FileStream);
            var dir = default(DirectoryInfo);
            var cod = GetCode(buffer);
            var pth = default(string);
            try
            {
                dir = new DirectoryInfo(s_ins._dir);
                if (dir.Exists == false)
                    dir.Create();
                pth = Path.Combine(dir.FullName, cod + _CacheExtension);
                if (File.Exists(pth) == false)
                {
                    fst = new FileStream(pth, FileMode.CreateNew, FileAccess.Write);
                    fst.Write(buffer, 0, buffer.Length);
                }
                return returnPath ? pth : cod;
            }
            catch (Exception ex)
            {
                if (nothrow == false)
                    throw;
                Trace.WriteLine(ex);
                return null;
            }
            finally
            {
                fst?.Dispose();
                fst = null;
            }
        }

        public static byte[] ImageSquare(string filepath)
        {
            var bmp = new Bitmap(filepath);
            var src = new Rectangle();
            if (bmp.Width > bmp.Height)
                src = new Rectangle((bmp.Width - bmp.Height) / 2, 0, bmp.Height, bmp.Height);
            else
                src = new Rectangle(0, (bmp.Height - bmp.Width) / 2, bmp.Width, bmp.Width);
            var len = bmp.Width > bmp.Height ? bmp.Height : bmp.Width;
            var div = 1;
            for (div = 1; len / div > s_ins._imgLimit; div++) ;
            var dst = new Rectangle(0, 0, len / div, len / div);
            return _LoadImage(bmp, src, dst, ImageFormat.Jpeg);
        }

        public static byte[] ImageResize(string filepath)
        {
            var bmp = new Bitmap(filepath);
            var len = bmp.Size;
            var div = 1;
            for (div = 1; len.Width / div > s_ins._imgLimit || len.Height / div > s_ins._imgLimit; div++) ;

            var src = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var dst = new Rectangle(0, 0, len.Width / div, len.Height / div);

            return _LoadImage(bmp, src, dst, ImageFormat.Jpeg);
        }

        private static byte[] _LoadImage(Bitmap bmp, Rectangle src, Rectangle dst, ImageFormat format)
        {
            var img = new Bitmap(dst.Right, dst.Bottom);
            var gra = Graphics.FromImage(img);
            var mst = new MemoryStream();
            var buf = default(byte[]);

            try
            {
                img.SetResolution(s_ins._imgdpi, s_ins._imgdpi);
                gra.DrawImage(bmp, dst, src, GraphicsUnit.Pixel);
                img.Save(mst, format);
                buf = mst.ToArray();
            }
            finally
            {
                mst?.Dispose();
                gra?.Dispose();
                bmp?.Dispose();
                img?.Dispose();
            }
            return buf;
        }
    }
}
