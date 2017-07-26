using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Messenger.Modules
{
    /// <summary>
    /// 负责管理图片缓存 (被动初始化)
    /// </summary>
    internal class Caches
    {
        public const string CacheFolder = "Cache";
        public const string CacheExtension = ".jpg";

        public const int DefaultLimit = 512;
        public const float DefaultDensity = 96;
        public const string KeyLimit = "cache-limit";
        public const string KeyDensity = "cache-density";

        private int imagelmt = DefaultLimit;
        private float imageDpi = DefaultDensity;

        private static Caches instance = new Caches();

        private Caches()
        {
            try
            {
                imagelmt = int.Parse(Options.GetOption(KeyLimit, DefaultLimit.ToString()));
                imageDpi = float.Parse(Options.GetOption(KeyDensity, DefaultDensity.ToString()));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        /// <summary>
        /// 计算 SHA1 值
        /// </summary>
        public static string GetCode(byte[] buffer)
        {
            using (var sha = new SHA1Managed())
            {
                var buf = sha.ComputeHash(buffer);
                var str = buf.Aggregate(string.Empty, (l, r) => $"{l}{r:x2}");
                return str;
            }
        }

        /// <summary>
        /// 获取缓存文件完整路径
        /// </summary>
        public static string GetPath(string code)
        {
            var dir = new DirectoryInfo(CacheFolder);
            return Path.Combine(dir.FullName, code + CacheExtension);
        }

        /// <summary>
        /// 保存图片缓存并返回 SHA1 值或完整路径
        /// </summary>
        public static string SetBuffer(byte[] buffer, bool returnPath, bool nothrow = true)
        {
            var fst = default(FileStream);
            var dir = default(DirectoryInfo);
            var cod = GetCode(buffer);
            var pth = default(string);
            try
            {
                dir = new DirectoryInfo(CacheFolder);
                if (dir.Exists == false)
                    dir.Create();
                pth = Path.Combine(dir.FullName, cod + CacheExtension);
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

        /// <summary>
        /// 加载图片并自动剪裁出正方形区域 (注意 IO 异常)
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
            for (div = 1; len / div > instance.imagelmt; div++) ;
            var dst = new Rectangle(0, 0, len / div, len / div);
            return LoadImage(bmp, src, dst, ImageFormat.Jpeg);
        }

        /// <summary>
        /// 加载图片并剪裁到指定尺寸以下 (注意 IO 异常)
        /// </summary>
        public static byte[] ImageResize(string filepath)
        {
            var bmp = new Bitmap(filepath);
            var len = bmp.Size;
            var div = 1;
            for (div = 1; len.Width / div > instance.imagelmt || len.Height / div > instance.imagelmt; div++) ;

            var src = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var dst = new Rectangle(0, 0, len.Width / div, len.Height / div);

            return LoadImage(bmp, src, dst, ImageFormat.Jpeg);
        }

        /// <summary>
        /// 将现有 Bitmap 对象指定区域剪裁保存
        /// </summary>
        /// <param name="bmp">Bitmap 对象 (返回时析构)</param>
        /// <param name="src">源区域</param>
        /// <param name="dst">目标区域</param>
        /// <param name="format">图像格式</param>
        private static byte[] LoadImage(Bitmap bmp, Rectangle src, Rectangle dst, ImageFormat format)
        {
            var img = new Bitmap(dst.Right, dst.Bottom);
            var gra = Graphics.FromImage(img);
            var mst = default(MemoryStream);
            var buf = default(byte[]);

            try
            {
                img.SetResolution(instance.imageDpi, instance.imageDpi);
                gra.DrawImage(bmp, dst, src, GraphicsUnit.Pixel);
                mst = new MemoryStream();
                img.Save(mst, format);
                mst.Dispose();
                buf = mst.ToArray();
                mst = null;
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
