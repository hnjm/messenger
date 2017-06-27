using System.IO;
using System.Security.Cryptography;

namespace Messenger.Foundation
{
    /// <summary>
    /// 加密扩展
    /// </summary>
    public static class Crypto
    {
        /// <summary>
        /// 加密数据
        /// </summary>
        /// <param name="buffer">字符流</param>
        /// <returns>加密后的数据</returns>
        public static byte[] Encrypt(this AesManaged aes, byte[] buffer) => BufferWriter(buffer, 0, buffer.Length, aes.CreateEncryptor());

        /// <summary>
        /// 解密数据
        /// </summary>
        /// <param name="buffer">字符流</param>
        /// <returns>解密后的数据</returns>
        public static byte[] Decrypt(this AesManaged aes, byte[] buffer) => BufferWriter(buffer, 0, buffer.Length, aes.CreateDecryptor());

        /// <summary>
        /// 加密数据
        /// </summary>
        /// <param name="buffer">字符流</param>
        /// <param name="offset">起始索引</param>
        /// <param name="count">字符数量</param>
        /// <returns>加密后的数据</returns>
        public static byte[] Encrypt(this AesManaged aes, byte[] buffer, int offset, int count) => BufferWriter(buffer, offset, count, aes.CreateEncryptor());

        /// <summary>
        /// 解密数据
        /// </summary>
        /// <param name="buffer">字符流</param>
        /// <param name="offset">起始索引</param>
        /// <param name="count">字符数量</param>
        /// <returns>解密后的数据</returns>
        public static byte[] Decrypt(this AesManaged aes, byte[] buffer, int offset, int count) => BufferWriter(buffer, offset, count, aes.CreateDecryptor());

        public static byte[] BufferWriter(byte[] buffer, int offset, int count, ICryptoTransform tramsform)
        {
            var mst = default(MemoryStream);
            var cst = default(CryptoStream);

            try
            {
                mst = new MemoryStream();
                cst = new CryptoStream(mst, tramsform, CryptoStreamMode.Write);
                cst.Write(buffer, offset, count);
                cst.Dispose();
                return mst.ToArray();
            }
            finally
            {
                mst?.Dispose();
                cst?.Dispose();
            }
        }
    }
}
