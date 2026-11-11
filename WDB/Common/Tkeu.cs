using System;
using System.Text;

namespace WDB.Common
{
    public class Tkeu
    {
        private static readonly char[] B62_CHARS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
        private static readonly int[] B62_MAP = BuildB62Map();
        private static int[] BuildB62Map()
        {
            int[] map = new int[128];
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = -1;
            }
            for (int i = 0; i < B62_CHARS.Length; i++)
            {
                map[B62_CHARS[i]] = i;
            }
            return map;
        }

        public static string Tke(string t, string k, int e)
        {
            if (string.IsNullOrEmpty(t)) return string.Empty;
            if (string.IsNullOrEmpty(k)) throw new ArgumentNullException(nameof(k), "密钥不能为空");

            int keyLen = k.Length;
            int textLen = t.Length;
            StringBuilder result = new StringBuilder(textLen * 2);
            int keySum = 0;
            for (int i = 0; i < keyLen; i++)
            {
                keySum += k[i];
            }
            int i12 = (keySum + keyLen) % 88 * 888 + 88888;
            int i13 = (keySum + keyLen) % 88 * 888 + 8888;

            if (e == 0)
            {
                char[] tempBuffer = new char[16];
                for (int i = 0; i < textLen; i++)
                {
                    int charCode = t[i];
                    int num = charCode <= 128 ? (charCode * 88) + i12 : charCode + i13;
                    int tempIdx = 0;
                    while (num > 0)
                    {
                        tempBuffer[tempIdx++] = B62_CHARS[num % 62];
                        num /= 62;
                    }
                    for (int j = tempIdx - 1; j >= 0; j--)
                    {
                        result.Append(tempBuffer[j]);
                    }
                }
            }
            else
            {
                int maxLoop = textLen / 3;
                for (int i = 0; i < maxLoop; i++)
                {
                    int pos = i * 3;
                    if (pos + 2 >= textLen) break;
                    int c1 = B62_MAP[t[pos]];
                    int c2 = B62_MAP[t[pos + 1]];
                    int c3 = B62_MAP[t[pos + 2]];
                    if (c1 == -1 || c2 == -1 || c3 == -1)
                    {
                        result.Append('\0');
                        continue;
                    }
                    int num = c1 * 3844 + c2 * 62 + c3;
                    if (num >= i12)
                    {
                        num = (num - i12) / 88;
                    }
                    else
                    {
                        num -= i13;
                    }
                    result.Append((char)num);
                }
            }

            return result.ToString();
        }
    }
}