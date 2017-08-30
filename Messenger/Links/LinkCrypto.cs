using System.IO;
using System.Security.Cryptography;

namespace Mikodev.Network
{
    internal static class LinkCrypto
    {
        public static byte[] _Encrypt(this AesManaged aes, byte[] buffer) => _BufferWriter(buffer, 0, buffer.Length, aes.CreateEncryptor());

        public static byte[] _Decrypt(this AesManaged aes, byte[] buffer) => _BufferWriter(buffer, 0, buffer.Length, aes.CreateDecryptor());

        public static byte[] _Encrypt(this AesManaged aes, byte[] buffer, int offset, int count) => _BufferWriter(buffer, offset, count, aes.CreateEncryptor());

        public static byte[] _Decrypt(this AesManaged aes, byte[] buffer, int offset, int count) => _BufferWriter(buffer, offset, count, aes.CreateDecryptor());

        public static byte[] _BufferWriter(byte[] buffer, int offset, int count, ICryptoTransform tramsform)
        {
            var mst = new MemoryStream();
            var cst = default(CryptoStream);

            try
            {
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
