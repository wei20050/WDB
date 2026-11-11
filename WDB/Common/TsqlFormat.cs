using System;
using System.Text;
using TSqlFormatter;
using TSqlFormatter.Formatters;

namespace WDB.Common
{
    public static class TsqlFormatHelper
    {
        /// <summary>
        /// Sql格式化
        /// </summary>
        /// <param name="strSql"></param>
        /// <param name="caseWrite"></param>
        /// <returns></returns>
        public static string SqlFormat(this string strSql, bool caseWrite = true)
        {
            string result;
            if (string.IsNullOrEmpty(strSql))
                return strSql;
            try
            {
                var errorsEncountered = false;
                var formaterManager = new SqlFormattingManager();
                var formater = (TSqlStandardFormatter)formaterManager.Formatter;
                formater.Options.UppercaseKeywords = caseWrite;
                result = formaterManager.Format(strSql, ref errorsEncountered);
            }
            catch (Exception)
            {
                result = strSql;
            }
            return result;
        }

        public static string SqlFormatToString(string strSql, bool caseWrite)
        {
            return strSql.SqlFormat(caseWrite);
        }

        /// <summary>
        /// sql压缩
        /// </summary>
        /// <param name="strsql"></param>
        /// <returns></returns>
        public static string CompressToString(this string strSql)
        {
            if (string.IsNullOrWhiteSpace(strSql))
                return string.Empty;

            var result = new StringBuilder(strSql.Length);
            bool inString = false;      // 是否在单引号字符串内
            bool inDoubleQuote = false; // 是否在双引号（标识符）内
            bool inLineComment = false; // 是否在单行注释内
            bool inBlockComment = false;// 是否在多行注释内
            bool lastWasSpace = false;  // 上一个字符是否是空格（用于压缩连续空格）

            for (int i = 0; i < strSql.Length; i++)
            {
                char c = strSql[i];
                char next = (i + 1 < strSql.Length) ? strSql[i + 1] : '\0';

                // 1. 处理多行注释 /* ... */
                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++; // 跳过 '/'
                    }
                    continue;
                }

                // 2. 处理单行注释 -- ...
                if (inLineComment)
                {
                    if (c == '\r' || c == '\n')
                    {
                        inLineComment = false;
                    }
                    else
                    {
                        continue; // 忽略注释内容
                    }
                }

                // 3. 处理字符串字面量（单引号）
                if (inString)
                {
                    result.Append(c);
                    // 处理转义的单引号 '' 或 \'
                    if (c == '\\' && next == '\'')
                    {
                        result.Append(next);
                        i++;
                    }
                    else if (c == '\'')
                    {
                        if (next == '\'') // 连续两个单引号，说明是转义，不是字符串结束
                        {
                            result.Append(next);
                            i++;
                        }
                        else
                        {
                            inString = false;
                        }
                    }
                    continue;
                }

                // 4. 处理双引号标识符（如 `列名` 或 "列名"）
                if (inDoubleQuote)
                {
                    result.Append(c);
                    if (c == '"')
                    {
                        inDoubleQuote = false;
                    }
                    continue;
                }

                // 5. 正常字符区域：判断注释和字符串起始
                if (c == '-' && next == '-')
                {
                    inLineComment = true;
                    i++; // 跳过第二个 '-'
                    continue;
                }
                if (c == '/' && next == '*')
                {
                    inBlockComment = true;
                    i++; // 跳过 '*'
                    continue;
                }
                if (c == '\'')
                {
                    inString = true;
                    result.Append(c);
                    lastWasSpace = false;
                    continue;
                }
                if (c == '"')
                {
                    inDoubleQuote = true;
                    result.Append(c);
                    lastWasSpace = false;
                    continue;
                }

                // 6. 正常字符区域的空格压缩
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                    {
                        result.Append(' ');
                        lastWasSpace = true;
                    }
                }
                else
                {
                    // 7. 清理符号周围空格
                    // 如果当前字符是标点符号，且之前追加了空格，则移除多余空格（但保留必要的分隔）
                    if (lastWasSpace && (c == ',' || c == '(' || c == ')' || c == ';'))
                    {
                        // 去掉末尾的空格
                        if (result.Length > 0 && result[result.Length - 1] == ' ')
                        {
                            result.Length--;
                        }
                    }

                    result.Append(c);
                    lastWasSpace = false;
                }
            }

            // 最后处理可能留下的尾部空格
            string finalSql = result.ToString().Trim();

            // 处理标点符号后的空格（如 "(" 或 "," 之后）
            finalSql = System.Text.RegularExpressions.Regex.Replace(finalSql, @"([,(;])\s+", "$1");

            return finalSql;
        }

    }
}
