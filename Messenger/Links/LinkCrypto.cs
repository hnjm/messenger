using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Mikodev.Network
{
    internal static class LinkCrypto
    {
        internal const int _Key = 32;

        internal const int _Block = 16;

        [ThreadStatic]
        internal static AesManaged s_aes = null;

        internal readonly static Random s_ran = new Random();

        internal static AesManaged _Instance()
        {
            if (s_aes == null)
                Interlocked.CompareExchange(ref s_aes, new AesManaged() { KeySize = _Key * 8, BlockSize = _Block * 8 }, null);
            return s_aes;
        }

        public static byte[] GetKey()
        {
            var buf = new byte[_Key];
            lock (s_ran)
                s_ran.NextBytes(buf);
            return buf;
        }

        public static byte[] GetBlock()
        {
            var buf = new byte[_Block];
            lock (s_ran)
                s_ran.NextBytes(buf);
            return buf;
        }

        public static byte[] Encrypt(byte[] buffer, byte[] key, byte[] iv)
        {
            var aes = _Instance();
            aes.Key = key;
            aes.IV = iv;
            var val = _Writer(buffer, 0, buffer.Length, aes.CreateEncryptor());
            return val;
        }

        public static byte[] Decrypt(byte[] buffer, byte[] key, byte[] iv)
        {
            var aes = _Instance();
            aes.Key = key;
            aes.IV = iv;
            var val = _Writer(buffer, 0, buffer.Length, aes.CreateDecryptor());
            return val;
        }

        internal static byte[] _Writer(byte[] buffer, int offset, int count, ICryptoTransform tramsform)
        {
            var mst = new MemoryStream();
            var cst = new CryptoStream(mst, tramsform, CryptoStreamMode.Write);

            try
            {
                cst.Write(buffer, offset, count);
                cst.Dispose();
                mst.Dispose();
                return mst.ToArray();
            }
            catch (Exception)
            {
                cst.Dispose();
                mst.Dispose();
                throw;
            }
        }
    }
}
