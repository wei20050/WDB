using NetFrameWork.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Threading;
using WDB.Entity;

namespace WDB.Common
{
    /// <summary>
    /// 把 SQL 文件内容导入到数据库
    /// </summary>
    public class SqlImport
    {
        /// <summary>
        /// 同一个对象(表/视图)最多记录的成功日志条数,超出后只记录失败
        /// </summary>
        public const int PerObjectLogLimit = 10;

        /// <summary>
        /// 导入结果统计
        /// </summary>
        public class Result
        {
            /// <summary>
            /// 语句总数
            /// </summary>
            public int Total { get; set; }

            /// <summary>
            /// 成功条数
            /// </summary>
            public int Success { get; set; }

            /// <summary>
            /// 失败条数
            /// </summary>
            public int Fail { get; set; }

            /// <summary>
            /// 影响行数合计
            /// </summary>
            public long Rows { get; set; }

            /// <summary>
            /// 耗时(毫秒)
            /// </summary>
            public double Milliseconds { get; set; }

            /// <summary>
            /// 是否被取消
            /// </summary>
            public bool Cancelled { get; set; }
        }

        /// <summary>
        /// 把 SQL 脚本按语句切分(识别字符串、标识符、注释与 MySQL 的 DELIMITER 指令)
        /// </summary>
        public static List<string> Split(string sql)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(sql))
            {
                return list;
            }
            var sb = new StringBuilder();
            var delimiter = ";";
            var n = sql.Length;
            var i = 0;
            //当前行是否只有空白(用于识别 DELIMITER 指令)
            var lineBlank = true;
            while (i < n)
            {
                var c = sql[i];
                if (c == '\n')
                {
                    lineBlank = true;
                    sb.Append(c);
                    i++;
                    continue;
                }
                if (c == '\r')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }
                if (lineBlank && (c == 'D' || c == 'd'))
                {
                    var nd = MatchDelimiter(sql, i, out var next);
                    if (nd != null)
                    {
                        AddStatement(list, sb);
                        delimiter = nd;
                        i = next;
                        lineBlank = true;
                        continue;
                    }
                }
                if (lineBlank && !char.IsWhiteSpace(c))
                {
                    lineBlank = false;
                }
                //行注释
                if ((c == '-' && i + 1 < n && sql[i + 1] == '-') || c == '#')
                {
                    var j = sql.IndexOf('\n', i);
                    if (j < 0)
                    {
                        j = n;
                    }
                    sb.Append(sql, i, j - i);
                    i = j;
                    continue;
                }
                //块注释(MySQL 的 /*! ... */ 为条件执行,保留内部语句)
                if (c == '/' && i + 1 < n && sql[i + 1] == '*')
                {
                    var condExec = i + 2 < n && sql[i + 2] == '!';
                    var start = i + (condExec ? 3 : 2);
                    var j = sql.IndexOf("*/", start, StringComparison.Ordinal);
                    if (j < 0)
                    {
                        j = n;
                    }
                    if (condExec)
                    {
                        sb.Append(' ').Append(sql, start, j - start).Append(' ');
                    }
                    i = j + 2;
                    continue;
                }
                //字符串/标识符内的分号不参与切分
                if (c == '\'' || c == '"' || c == '`')
                {
                    i = AppendQuoted(sql, i, sb, c, c == '\'');
                    continue;
                }
                if (c == '[')
                {
                    i = AppendQuoted(sql, i, sb, '[', false, ']');
                    continue;
                }
                if (IsDelimiter(sql, i, delimiter))
                {
                    AddStatement(list, sb);
                    i += delimiter.Length;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            AddStatement(list, sb);
            return list;
        }

        /// <summary>
        /// 识别 DELIMITER 指令,返回新的分隔符(不是指令返回 null)
        /// </summary>
        private static string MatchDelimiter(string sql, int i, out int next)
        {
            next = i;
            const string key = "delimiter";
            if (i + key.Length > sql.Length)
            {
                return null;
            }
            if (string.Compare(sql, i, key, 0, key.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return null;
            }
            var p = i + key.Length;
            if (p >= sql.Length || (sql[p] != ' ' && sql[p] != '\t'))
            {
                return null;
            }
            while (p < sql.Length && (sql[p] == ' ' || sql[p] == '\t'))
            {
                p++;
            }
            var end = p;
            while (end < sql.Length && sql[end] != '\r' && sql[end] != '\n')
            {
                end++;
            }
            next = end;
            var d = sql.Substring(p, end - p).Trim();
            return string.IsNullOrEmpty(d) ? ";" : d;
        }

        /// <summary>
        /// 原样拷贝引号内的内容并返回结束位置
        /// </summary>
        private static int AppendQuoted(string sql, int i, StringBuilder sb, char quote, bool backslashEscape, char? endChar = null)
        {
            var end = endChar ?? quote;
            sb.Append(sql[i]);
            i++;
            while (i < sql.Length)
            {
                var c = sql[i];
                if (backslashEscape && c == '\\' && i + 1 < sql.Length)
                {
                    sb.Append(c).Append(sql[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == end)
                {
                    //双写引号为转义
                    if (i + 1 < sql.Length && sql[i + 1] == end)
                    {
                        sb.Append(c).Append(sql[i + 1]);
                        i += 2;
                        continue;
                    }
                    sb.Append(c);
                    return i + 1;
                }
                sb.Append(c);
                i++;
            }
            return i;
        }

        private static bool IsDelimiter(string sql, int i, string delimiter)
        {
            return string.CompareOrdinal(sql, i, delimiter, 0, delimiter.Length) == 0;
        }

        /// <summary>
        /// 收集一条语句(空语句与纯注释不入库)
        /// </summary>
        private static void AddStatement(List<string> list, StringBuilder sb)
        {
            var s = sb.ToString().Trim();
            sb.Clear();
            if (string.IsNullOrWhiteSpace(s) || IsOnlyComment(s))
            {
                return;
            }
            list.Add(s);
        }

        private static bool IsOnlyComment(string s)
        {
            foreach (var line in s.Split('\n'))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("--") || t.StartsWith("#"))
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// 逐条执行 SQL 并输出日志
        /// </summary>
        /// <param name="l">连接上下文(已定位到目标库/模式)</param>
        /// <param name="sqlList">语句列表</param>
        /// <param name="continueOnError">失败后是否继续</param>
        /// <param name="token">取消标记</param>
        /// <param name="progress">进度回调(百分比,状态)</param>
        /// <param name="log">日志回调</param>
        public static Result Run(LianJie l, List<string> sqlList, bool continueOnError,
            CancellationToken token, Action<int, string> progress, Action<SqlImportLogItem> log)
        {
            var result = new Result();
            if (l == null || sqlList == null)
            {
                return result;
            }
            result.Total = sqlList.Count;
            var orm = Db.GetOrm(l);
            var conn = orm.GetConnection();
            //同一对象的成功日志计数(超过上限后只记录失败)
            var logCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var sw = Stopwatch.StartNew();
            try
            {
                conn.Open();
                if (l.DataBaseType == ORM.DbTypeEnum.Pgsql)
                {
                    //pg 时区
                    _ = orm.GetCommand("SET TIME ZONE 'Asia/Shanghai';", conn).ExecuteNonQuery();
                }
                for (var i = 0; i < sqlList.Count; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        result.Cancelled = true;
                        break;
                    }
                    var sql = sqlList[i];
                    //用 i+1 计算,保证最后一条语句进度到 100
                    progress?.Invoke((i + 1) * 100 / sqlList.Count, $"正在执行第 {i + 1}/{sqlList.Count} 条");
                    var st = DateTime.Now;
                    var head = Short(sql, i + 1, sqlList.Count);
                    //同一张表只记录前 N 条成功日志,后续仅记录失败
                    var key = GetTargetName(sql);
                    var seq = 0;
                    if (key != null)
                    {
                        logCount.TryGetValue(key, out seq);
                        seq++;
                        logCount[key] = seq;
                    }
                    try
                    {
                        using (var cmd = orm.GetCommand(sql, conn))
                        {
                            //导入可能耗时较长,不设超时
                            cmd.CommandTimeout = 0;
                            var rows = cmd.ExecuteNonQuery();
                            //部分库对非DML语句返回 -1,按 0 行显示
                            if (rows < 0)
                            {
                                rows = 0;
                            }
                            result.Success++;
                            result.Rows += rows;
                            if (key == null || seq <= PerObjectLogLimit)
                            {
                                log?.Invoke(new SqlImportLogItem
                                {
                                    Time = DateTime.Now.ToString("HH:mm:ss"),
                                    Text = head,
                                    Sql = sql,
                                    Detail = $"成功,影响 {rows} 行,耗时 {(DateTime.Now - st).TotalMilliseconds:F0} ms"
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Fail++;
                        Log.Error($"{ex.Message}{Environment.NewLine}{sql}");
                        log?.Invoke(new SqlImportLogItem
                        {
                            Time = DateTime.Now.ToString("HH:mm:ss"),
                            Text = head,
                            Sql = sql,
                            Detail = $"失败:{ex.Message}",
                            IsError = true
                        });
                        if (!continueOnError)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                sw.Stop();
                result.Milliseconds = sw.Elapsed.TotalMilliseconds;
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
            return result;
        }

        /// <summary>
        /// 取 DML 语句的操作对象名作为日志分组依据(非 DML 返回 null)
        /// </summary>
        private static string GetTargetName(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return null;
            }
            foreach (var line in sql.Split('\n'))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("--") || t.StartsWith("#"))
                {
                    //跳过语句前的注释行
                    continue;
                }
                var parts = t.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var p = 0;
                switch (parts[0].ToUpperInvariant())
                {
                    case "INSERT":
                    case "REPLACE":
                        p = 1;
                        if (p < parts.Length && parts[p].Equals("INTO", StringComparison.OrdinalIgnoreCase))
                        {
                            p++;
                        }
                        break;
                    case "UPDATE":
                        p = 1;
                        break;
                    case "DELETE":
                        p = 1;
                        if (p < parts.Length && parts[p].Equals("FROM", StringComparison.OrdinalIgnoreCase))
                        {
                            p++;
                        }
                        break;
                    default:
                        return null;
                }
                if (p >= parts.Length)
                {
                    return null;
                }
                var name = parts[p].Trim('`', '"', '\'', '[', ']');
                var idx = name.IndexOf('(');
                if (idx > 0)
                {
                    name = name.Substring(0, idx);
                }
                return string.IsNullOrEmpty(name) ? null : name;
            }
            return null;
        }

        /// <summary>
        /// 语句摘要(压缩空白并截断)
        /// </summary>
        private static string Short(string sql, int index, int total)
        {
            var sb = new StringBuilder(sql.Length);
            var space = false;
            foreach (var c in sql)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!space)
                    {
                        sb.Append(' ');
                        space = true;
                    }
                    continue;
                }
                sb.Append(c);
                space = false;
            }
            var s = sb.ToString().Trim();
            if (s.Length > 160)
            {
                s = s.Substring(0, 160) + " ...";
            }
            return $"({index}/{total}) {s}";
        }
    }
}
