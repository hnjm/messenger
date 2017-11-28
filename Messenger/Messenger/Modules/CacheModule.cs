using Messenger.Models;
using Mikodev.Logger;
using System;
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
    internal class CacheModule
    {
        private const string _CacheFolder = "Temp";
        private const string _CacheExtension = ".png";

        private const int _Limit = 384;
        private const float _Density = 96;
        private const string _KeyCache = "cache-dir";
        private const string _KeyLimit = "cache-limit";
        private const string _KeyDensity = "cache-density";

        private int _imgLimit = _Limit;
        private float _imgdpi = _Density;
        private string _dir = _CacheFolder;

        private static CacheModule s_ins = new CacheModule();

        private CacheModule() { }

        [Loader(16, LoaderFlags.OnLoad)]
        public static void Load()
        {
            try
            {
                s_ins._dir = OptionModule.GetOption(_KeyCache, _CacheFolder);
                s_ins._imgLimit = int.Parse(OptionModule.GetOption(_KeyLimit, _Limit.ToString()));
                s_ins._imgdpi = float.Parse(OptionModule.GetOption(_KeyDensity, _Density.ToString()));
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        /// <summary>
        /// 计算缓存的 SHA256 值
        /// </summary>
        public static string GetCode(byte[] buffer)
        {
            using (var sha = new SHA256Managed())
            {
                var buf = sha.ComputeHash(buffer);
                var str = buf.Aggregate(new StringBuilder(), (l, r) => l.AppendFormat("{0:x2}", r));
                return str.ToString();
            }
        }

        /// <summary>
        /// 从本地缓存查找指定 SHA256 值的图像
        /// </summary>
        public static string GetPath(string code)
        {
            var dir = new DirectoryInfo(s_ins._dir);
            var pth = Path.Combine(dir.FullName, code + _CacheExtension);
            return pth;
        }

        /// <summary>
        /// 写入本地缓存, 并将 SHA256 值作为文件名
        /// </summary>
        /// <param name="returnPath">返回完整路径(真), 返回 SHA256 值 (假)</param>
        public static string SetBuffer(byte[] buffer, bool returnPath, bool nothrow = true)
        {
            var fst = default(FileStream);
            var cod = GetCode(buffer);

            try
            {
                var dir = new DirectoryInfo(s_ins._dir);
                if (dir.Exists == false)
                    dir.Create();
                var pth = Path.Combine(dir.FullName, cod + _CacheExtension);
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
                Log.Error(ex);
                return null;
            }
            finally
            {
                fst?.Dispose();
                fst = null;
            }
        }

        /// <summary>
        /// 从图像中裁剪出正方形区域 (用于个人头像)
        /// </summary>
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
            return _LoadImage(bmp, src, dst, ImageFormat.Png);
        }

        /// <summary>
        /// 按比例缩放图像 (用于聊天)
        /// </summary>
        public static byte[] ImageResize(string filepath)
        {
            var bmp = new Bitmap(filepath);
            var len = bmp.Size;
            var div = 1;
            for (div = 1; len.Width / div > s_ins._imgLimit || len.Height / div > s_ins._imgLimit; div++) ;

            var src = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var dst = new Rectangle(0, 0, len.Width / div, len.Height / div);

            return _LoadImage(bmp, src, dst, ImageFormat.Png);
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
