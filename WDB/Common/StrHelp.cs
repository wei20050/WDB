using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WDB.Common
{
    public static class StrHelp
    {

        /// <summary>
        /// 字符串补0
        /// </summary>
        /// <param name="str"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public static string Str0(string str, int n)
        {
            if (str.Length > n)
            {
                return str;
            }
            var ret = "";
            for (int i = 0; i < n - str.Length; i++)
            {
                ret += "0";
            }
            ret += str;
            return ret;
        }

        /// <summary>
        /// 获取guid
        /// </summary>
        public static string Id => Guid.NewGuid().ToString("N");

        /// <summary>
        /// 模糊匹配忽略大小写
        /// </summary>
        public static bool ContainsIgnoreCase(this string source, string value)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (value == null) throw new ArgumentNullException(nameof(value));

            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 转时间显示字符串
        /// </summary>
        public static string ToTimeString(double milliseconds)
        {
            if (milliseconds <= 0)
                return "0秒0毫秒";

            const long msPerSecond = 1000;
            const long msPerMinute = msPerSecond * 60;
            const long msPerHour = msPerMinute * 60;
            const long msPerDay = msPerHour * 24;

            // 计算各部分
            long days = (long)milliseconds / msPerDay;
            long remaining = (long)milliseconds % msPerDay;

            long hours = remaining / msPerHour;
            remaining %= msPerHour;

            long minutes = remaining / msPerMinute;
            remaining %= msPerMinute;

            long seconds = remaining / msPerSecond;
            long ms = remaining % msPerSecond;

            // 拼接字符串（自动跳过为0的部分，不冗余显示）
            string result = "";

            if (days > 0) result += $"{days}天";
            if (hours > 0) result += $"{hours}时";
            if (minutes > 0) result += $"{minutes}分";
            if (seconds > 0) result += $"{seconds}秒";
            result += $"{ms}毫秒";

            return result;
        }

        /// <summary>
        /// 多关键词模糊匹配（空格分隔）
        /// 任意一个关键词匹配就返回 true
        /// 自动忽略大小写
        /// </summary>
        public static bool ContainsAnyKeyword(this string source, string searchText)
        {
            // 源文本为空 → 不匹配
            if (string.IsNullOrWhiteSpace(source))
                return false;

            // 搜索内容为空 → 默认匹配
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            // 按空格分割关键词，自动去掉空值
            var keywords = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // 任意一个匹配就返回 true
            return keywords.Any(key => source.ContainsIgnoreCase(key));
        }

        /// <summary>
        /// 简易通配符匹配：_匹配1个任意字符，*匹配0个或多个任意字符
        /// </summary>
        /// <param name="input">待匹配的目标字符串</param>
        /// <param name="pattern">匹配模式串（包含_和*）</param>
        /// <returns>是否匹配成功</returns>
        public static bool Match(string input, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;//空模式串默认匹配成功
            if (string.IsNullOrEmpty(input)) return false;//空输入默认匹配不成功
            int inputIndex = 0;    // 目标字符串指针
            int patternIndex = 0;  // 模式串指针
            int starIndex = -1;    // 记录*的位置
            int matchIndex = 0;    // 记录*匹配到的位置
            int inputLen = input.Length;
            int patternLen = pattern.Length;
            while (inputIndex < inputLen)
            {
                // 情况1：当前字符匹配（普通字符相等 或 模式是_）
                if (patternIndex < patternLen && (pattern[patternIndex] == '_' || char.ToLower(pattern[patternIndex]) == char.ToLower(input[inputIndex])))
                {
                    inputIndex++;
                    patternIndex++;
                }
                // 情况2：遇到*，记录位置并尝试匹配0个字符（先移动模式指针）
                else if (patternIndex < patternLen && pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex;
                    matchIndex = inputIndex;
                    patternIndex++;
                }
                // 情况3：之前有*，回退并尝试匹配更多字符（核心：*匹配任意长度）
                else if (starIndex != -1)
                {
                    patternIndex = starIndex + 1; // 回到*的下一个字符，重新匹配
                    matchIndex++;                 // *多匹配一个字符
                    inputIndex = matchIndex;      // 输入指针跟随matchIndex
                }
                // 情况4：无匹配且无*，直接返回false
                else
                {
                    return false;
                }
            }
            // 处理模式串末尾剩余的*（*可以匹配0个字符，直接跳过）
            while (patternIndex < patternLen && pattern[patternIndex] == '*')
            {
                patternIndex++;
            }
            // 最终判断：模式串是否完全匹配（指针走到末尾）
            return patternIndex == patternLen;
        }

        /// <summary>
        /// 将普通字符串直接编码为 Hex 字符串（默认UTF8编码，大写）
        /// </summary>
        /// <param name="input">待编码的普通字符串（如"Hello 世界"）</param>
        /// <param name="encoding">字符编码（默认UTF8，可传GB2312/Unicode等）</param>
        /// <param name="upperCase">Hex字符串是否大写（默认是）</param>
        /// <returns>Hex格式字符串（如"48656C6C6F20E4B896E7958C"）</returns>
        public static string EncodeStringToHex(string input, Encoding encoding = null, bool upperCase = true)
        {
            // 空值处理
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // 默认使用UTF8编码
            encoding = encoding ?? Encoding.UTF8;

            // 步骤1：字符串→字节数组（屏蔽细节，内部处理）
            byte[] inputBytes = encoding.GetBytes(input);

            // 步骤2：字节数组→Hex字符串（.NET 4.5原生方法，高效）
            string hexStr = BitConverter.ToString(inputBytes).Replace("-", "");

            // 控制大小写
            return upperCase ? hexStr.ToUpperInvariant() : hexStr.ToLowerInvariant();
        }

        /// <summary>
        /// 将 Hex 字符串直接解码为普通字符串（默认UTF8编码）
        /// </summary>
        /// <param name="hexStr">Hex格式字符串（可含空格/横线，兼容大小写）</param>
        /// <param name="encoding">字符编码（需和编码时一致，默认UTF8）</param>
        /// <returns>解码后的原字符串（如"Hello 世界"）</returns>
        /// <exception cref="ArgumentException">Hex格式无效时抛出</exception>
        public static string DecodeHexToString(string hexStr, Encoding encoding = null)
        {
            // 空值处理
            if (string.IsNullOrEmpty(hexStr))
                return string.Empty;

            // 默认使用UTF8编码
            encoding = encoding ?? Encoding.UTF8;

            // 步骤1：清理Hex字符串（过滤空格、横线等无效字符）
            StringBuilder cleanHex = new StringBuilder();
            foreach (char c in hexStr)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'))
                {
                    cleanHex.Append(c);
                }
            }
            string cleanedHex = cleanHex.ToString();

            // 校验：Hex字符串长度必须是偶数（1个字符=2个Hex位）
            if (cleanedHex.Length % 2 != 0)
                throw new ArgumentException("无效的Hex字符串：长度必须为偶数", nameof(hexStr));

            // 步骤2：Hex字符串→字节数组（内部处理，屏蔽细节）
            byte[] outputBytes = new byte[cleanedHex.Length / 2];
            for (int i = 0; i < cleanedHex.Length; i += 2)
            {
                string byteSegment = cleanedHex.Substring(i, 2);
                if (!byte.TryParse(byteSegment, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                {
                    throw new ArgumentException($"Hex字符串包含无效字符：{byteSegment}", nameof(hexStr));
                }
                outputBytes[i / 2] = b;
            }

            // 步骤3：字节数组→普通字符串
            return encoding.GetString(outputBytes);
        }

        /// <summary>
        /// 转 小驼峰 (camelCase)
        /// 示例：User Name → userName，user_name → userName，USER-NAME → userName
        /// </summary>
        public static string ToCamelCase(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return str;

            string pascal = ToPascalCase(str);
            // 首字母小写，其余不变
            return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
        }

        /// <summary>
        /// 转 大驼峰 (PascalCase)
        /// 示例：user name → UserName，user_name → UserName，USER-NAME → UserName
        /// </summary>
        public static string ToPascalCase(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return str;

            bool nextUpper = true;
            StringBuilder result = new StringBuilder();

            foreach (char c in str)
            {
                // 跳过分隔符（空格、下划线、短横线），下一个字符大写
                if (c == ' ' || c == '_' || c == '-')
                {
                    nextUpper = true;
                    continue;
                }

                // 处理大小写
                if (nextUpper)
                {
                    result.Append(char.ToUpperInvariant(c));
                    nextUpper = false;
                }
                else
                {
                    result.Append(char.ToLowerInvariant(c));
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// 转 蛇形小写 (snake_case)
        /// 示例：UserName → user_name，User Name → user_name，USER‑NAME → user_name
        /// </summary>
        public static string ToSnakeCase(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return str;

            StringBuilder result = new StringBuilder();
            foreach (char c in str)
            {
                if (c == ' ' || c == '_' || c == '-')
                {
                    result.Append('_');
                    continue;
                }

                if (char.IsUpper(c))
                {
                    // 不是第一个字符，前面加下划线
                    if (result.Length > 0)
                    {
                        result.Append('_');
                    }
                    result.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    result.Append(char.ToLowerInvariant(c));
                }
            }
            // 清理连续下划线
            return result.ToString().Replace("__", "_");
        }

        /// <summary>
        /// Json格式化
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string JsonFormatter(string str)
        {
            if (str.TrimStart().StartsWith("{"))
            {
                JObject jsonObj = JObject.Parse(str);
                return jsonObj.ToString(Formatting.Indented);
            }
            else
            {
                JArray jsonArr = JArray.Parse(str);
                return jsonArr.ToString(Formatting.Indented);
            }
        }


        /// <summary>
        /// Json压缩
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static string JsonCompress(string json)
        {
            StringBuilder sb = new StringBuilder();
            using (StringReader reader = new StringReader(json))
            {
                int ch = -1;
                int lastch = -1;
                bool isQuoteStart = false;
                while ((ch = reader.Read()) > -1)
                {
                    if ((char)lastch != '\\' && (char)ch == '\"')
                    {
                        if (!isQuoteStart)
                        {
                            isQuoteStart = true;
                        }
                        else
                        {
                            isQuoteStart = false;
                        }
                    }
                    if (!char.IsWhiteSpace((char)ch) || isQuoteStart)
                    {
                        sb.Append((char)ch);
                    }
                    lastch = ch;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 生成随机密码
        /// </summary>
        /// <param name="genlen"></param>
        /// <param name="genNum">生成个数</param>
        /// <param name="usenumbers"></param>
        /// <param name="uselowalphabets"></param>
        /// <param name="usehighalphabets"></param>
        /// <param name="usesymbols"></param>
        /// <returns></returns>
        public static List<string> GeneratePassword(int genlen = 21, int genNum = 1, bool usenumbers = true, bool uselowalphabets = true, bool usehighalphabets = true, bool usesymbols = true)
        {
            var upperCase = new char[]
                    {
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U',
                'V', 'W', 'X', 'Y', 'Z'
                    };

            var lowerCase = new char[]
                {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u',
                'v', 'w', 'x', 'y', 'z'
                };

            var numerals = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

            var symbols = new char[]
                {
                '~', '`', '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '{', '[', '}', ']', '-', '_', '=', '+', ':',
                ';', '|', '/', '?', ',', '<', '.', '>'
                };

            char[] total = (new char[0])
                            .Concat(usehighalphabets ? upperCase : new char[0])
                            .Concat(uselowalphabets ? lowerCase : new char[0])
                            .Concat(usenumbers ? numerals : new char[0])
                            .Concat(usesymbols ? symbols : new char[0])
                            .ToArray();
            var listGen = new List<string>();
            var rnd = new Random();
            for (int i = 0; i < genNum; i++)
            {
                var chars = Enumerable
                    .Repeat<int>(0, genlen)
                    .Select(j => total[rnd.Next(total.Length)])
                    .ToArray();
                listGen.Add(new string(chars));
            }
            return listGen;
        }

        /// <summary>
        /// 密码强度计算
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static int PasswordStrength(string password)
        {
            //空字符串强度值为0
            if (password == "") return 0;
            //字符统计
            int iNum = 0, iLtt = 0, iSym = 0;
            foreach (char c in password)
            {
                if (c >= '0' && c <= '9') iNum++;
                else if (c >= 'a' && c <= 'z') iLtt++;
                else if (c >= 'A' && c <= 'Z') iLtt++;
                else iSym++;
            }
            if (iLtt == 0 && iSym == 0) return 1; //纯数字密码
            if (iNum == 0 && iLtt == 0) return 1; //纯符号密码
            if (iNum == 0 && iSym == 0) return 1; //纯字母密码
            if (password.Length <= 6) return 1; //长度不大于6的密码
            if (iLtt == 0) return 2; //数字和符号构成的密码
            if (iSym == 0) return 2; //数字和字母构成的密码
            if (iNum == 0) return 2; //字母和符号构成的密码
            if (password.Length <= 10) return 2; //长度不大于10的密码
            return 3; //由数字、字母、符号构成的密码 
        }

        /// <summary>
        /// Base64编码
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string Base46_Encode(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Base64解码
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string Base46_Decode(string text)
        {
            byte[] outputb = Convert.FromBase64String(text);
            return Encoding.UTF8.GetString(outputb);
        }

    }
}
