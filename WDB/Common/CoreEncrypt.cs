using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WDB.Common
{
    public class CoreEncrypt
    {

        /// <summary>
        /// 32位MD5加密
        /// </summary>
        /// <param name="str">待加密的文本</param>
        /// <returns>返回32位MD5</returns>
        public static string Md5(string str)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] byData = Encoding.Default.GetBytes(str);
            byte[] result = md5.ComputeHash(byData);
            string md5Str = BitConverter.ToString(result);
            md5Str = md5Str.Replace("-", "");
            return md5Str;
        }

        /// <summary>
        /// 16位MD5加密
        /// </summary>
        /// <param name="str">待加密的文本</param>
        /// <returns>返回16位MD5</returns>
        public static string Md516(string str)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] byData = Encoding.Default.GetBytes(str);
            byte[] result = md5.ComputeHash(byData);
            string md5Str = BitConverter.ToString(result, 4, 8);
            md5Str = md5Str.Replace("-", "");
            return md5Str;
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="str">加密字符</param>
        /// <param name="key">加密的key</param>
        /// <param name="isBase64">是否返回Base64字符串(默认16进制字符串)</param>
        /// <param name="cipherMode">加密模式(默认 ECB)</param>
        /// <param name="paddingMode">填充模式(默认pk-cs5padding)</param>
        /// <param name="iv">偏移量(默认与秘钥相同)</param>
        /// <param name="encoding">编码(默认UTF-8)</param>
        /// <returns></returns>
        public static string AesEncrypt(string str, string key, bool isBase64 = false, CipherMode cipherMode = CipherMode.ECB, PaddingMode paddingMode = PaddingMode.PKCS7, string iv = null, string encoding = "utf-8")
        {
            if (string.IsNullOrEmpty(str)) return null;
            var toEncryptArray = Encoding.GetEncoding(encoding).GetBytes(str);
            var keyArray = GetKey(Encoding.GetEncoding(encoding).GetBytes(key));
            var rm = new RijndaelManaged
            {
                Key = keyArray,
                Mode = cipherMode,
                Padding = paddingMode
            };
            if (cipherMode != CipherMode.ECB)
            {
                rm.IV = iv == null ? keyArray : Encoding.GetEncoding(encoding).GetBytes(iv);
            }
            var cTransform = rm.CreateEncryptor();
            var resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return isBase64 ? Convert.ToBase64String(resultArray, 0, resultArray.Length) : BytesToHex(resultArray);
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="str">加密字符</param>
        /// <param name="key">加密的key</param>
        /// <param name="isBase64">是否返回Base64字符串(默认16进制字符串)</param>
        /// <param name="cipherMode">加密模式(默认 ECB)</param>
        /// <param name="paddingMode">填充模式(默认pk-cs5padding)</param>
        /// <param name="iv">偏移量(默认与秘钥相同)</param>
        /// <param name="encoding">编码(默认UTF-8)</param>
        /// <returns></returns>
        public static string AesDecrypt(string str, string key, bool isBase64 = false, CipherMode cipherMode = CipherMode.ECB, PaddingMode paddingMode = PaddingMode.PKCS7, string iv = null, string encoding = "utf-8")
        {
            if (string.IsNullOrEmpty(str)) return null;
            var toEncryptArray = isBase64 ? Convert.FromBase64String(str) : HexToBytes(str);
            var keyArray = GetKey(Encoding.GetEncoding(encoding).GetBytes(key));
            var rm = new RijndaelManaged
            {
                Key = keyArray,
                Mode = cipherMode,
                Padding = paddingMode
            };
            if (cipherMode != CipherMode.ECB)
            {
                rm.IV = iv == null ? keyArray : Encoding.GetEncoding(encoding).GetBytes(iv);
            }
            var cTransform = rm.CreateDecryptor();
            var resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return Encoding.GetEncoding(encoding).GetString(resultArray);
        }

        /// <summary>
        /// 获取AES加解密KEY
        /// </summary>
        /// <param name="keyBytes"></param>
        /// <returns></returns>
        private static byte[] GetKey(byte[] keyBytes)
        {
            // 如果密钥位数不是16的倍数位，那么就补byte 0
            var baseLen = 16;
            if (keyBytes.Length % baseLen != 0)
            {
                var groups = keyBytes.Length / baseLen + 1;
                var temp = new byte[groups * baseLen];
                // 关键代码 补全
                Array.Copy(keyBytes, 0, temp, 0, keyBytes.Length);
                keyBytes = temp;
            }
            return keyBytes;
        }

        /// <summary>
        /// 16进制字符串转Byte数组
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        private static byte[] HexToBytes(string hex)
        {
            var num = (int)Math.Round((double)hex.Length / 2);
            var buffer = new byte[num];
            var num3 = num - 1;
            for (var i = 0; i <= num3; i++)
            {
                var s = hex.Substring(i * 2, 2);
                buffer[i] = (byte)int.Parse(s, NumberStyles.HexNumber);
            }
            return buffer;
        }

        /// <summary>
        /// Byte数组转16进制字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static string BytesToHex(IList<byte> bytes)
        {
            var builder = new StringBuilder();
            var num2 = bytes.Count - 1;
            for (var i = 0; i <= num2; i++)
            {
                builder.AppendFormat("{0:X2}", bytes[i]);
            }
            return builder.ToString();
        }

    }
}
