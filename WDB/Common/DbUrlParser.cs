using NetFrameWork.Database;
using System;
using System.Collections.Generic;
using System.Text;
using WDB.Entity;

namespace WDB.Common
{
    /// <summary>
    /// 连接串解析器:把 URL / JDBC 连接串解析成已预填字段的 LianJie,
    /// 供"新建连接 → URL / JDBC 自动识别"入口直接打开对应的新建窗口。
    /// 只做字符串解析,不连库、不校验可达性。
    /// 解析不到的值一律留 null(不是空串),由各新建窗口自己填默认值。
    /// </summary>
    public static class DbUrlParser
    {
        /// <summary>
        /// scheme → 类型名(关系库为 ORM.DbTypeEnum 的名称,NoDb 为 Redis/MongoDB)
        /// </summary>
        private static readonly Dictionary<string, string> SchemeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "mysql", "MySql" },
            { "mariadb", "MySql" },
            { "sqlserver", "SqlServer" },
            { "mssql", "SqlServer" },
            { "jtds:sqlserver", "SqlServer" },
            { "oracle", "Oracle" },
            { "dm", "Dameng" },
            { "dameng", "Dameng" },
            { "kingbase", "Kingbase" },
            { "kingbase8", "Kingbase" },
            { "postgresql", "Pgsql" },
            { "postgres", "Pgsql" },
            { "pgsql", "Pgsql" },
            { "opengauss", "GaussDB" },
            { "gaussdb", "GaussDB" },
            { "sqlite", "SqlIte" },
            { "redis", "Redis" },
            { "rediss", "Redis" },
            { "mongodb", "MongoDB" },
            { "mongodb+srv", "MongoDB" },
        };

        /// <summary>
        /// 解析连接串。失败时 lianJie 为 null 并给出 error 文案。
        /// </summary>
        public static bool TryParse(string input, out LianJie lianJie, out string error)
        {
            lianJie = null;
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "请输入数据库 URL、JDBC 连接串或配置片段";
                return false;
            }

            // 先按"配置片段"取键值对(url / username / password),支持 YAML 与 properties 两种风格;
            // 取不到 url 就退回"整串解析"模式
            var kv = ExtractKeyValues(input);
            string userName = null;
            string passWord = null;
            string text;

            if (kv.TryGetValue("url", out var configUrl))
            {
                text = (configUrl ?? string.Empty).Trim();
                if (text.Length == 0)
                {
                    error = "配置片段中的 url 为空,请检查";
                    return false;
                }
                kv.TryGetValue("username", out userName);
                kv.TryGetValue("password", out passWord);
            }
            else
            {
                text = input;
            }

            text = Normalize(text);
            if (text.Length == 0)
            {
                error = "请输入数据库 URL、JDBC 连接串或配置片段";
                return false;
            }

            // 统一剥掉 jdbc: 前缀,剩下的按 scheme 识别
            if (text.StartsWith("jdbc:", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(5);
            }

            SplitScheme(text, out var scheme, out var body);
            if (string.IsNullOrEmpty(scheme) || !SchemeMap.TryGetValue(scheme, out var typeName))
            {
                error = "无法识别的连接串:未匹配到支持的数据库类型";
                return false;
            }

            var lj = new LianJie { IsFilter = true };
            if (typeName == "Redis" || typeName == "MongoDB")
            {
                lj.NoDbType = typeName;
            }
            else
            {
                lj.DataBaseType = (ORM.DbTypeEnum)Enum.Parse(typeof(ORM.DbTypeEnum), typeName);
            }

            if (typeName == "Oracle")
            {
                ParseOracle(body, lj);
            }
            else if (typeName == "SqlIte")
            {
                ParseSqlite(body, lj);
            }
            else
            {
                ParseCommon(body, lj, typeName);
            }

            // 配置片段里显式写出的账号密码优先(URL 本身没带账号时尤其需要)
            if (!string.IsNullOrEmpty(userName))
            {
                lj.UserName = userName;
            }
            if (!string.IsNullOrEmpty(passWord))
            {
                lj.PassWord = passWord;
            }

            if (string.IsNullOrWhiteSpace(lj.Host))
            {
                error = $"已识别为 {GetDisplayName(lj)},但未解析出主机地址,请检查连接串";
                lianJie = null;
                return false;
            }

            lianJie = lj;
            return true;
        }

        /// <summary>
        /// 取库图标资源名(不含扩展名),与 Resources/connect 下的图标文件名一致
        /// </summary>
        public static string GetIconName(LianJie lj)
        {
            if (lj == null)
            {
                return null;
            }
            return string.IsNullOrEmpty(lj.NoDbType)
                ? lj.DataBaseType.ToString().ToLowerInvariant()
                : lj.NoDbType.ToLowerInvariant();
        }

        /// <summary>
        /// 取库类型显示名(用于界面预览)
        /// </summary>
        public static string GetDisplayName(LianJie lj)
        {
            if (lj == null)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(lj.NoDbType))
            {
                return lj.NoDbType;
            }
            switch (lj.DataBaseType)
            {
                case ORM.DbTypeEnum.MySql:
                    return "MySQL";
                case ORM.DbTypeEnum.SqlServer:
                    return "SQL Server";
                case ORM.DbTypeEnum.SqlIte:
                    return "SQLite";
                case ORM.DbTypeEnum.Pgsql:
                    return "PostgreSQL";
                case ORM.DbTypeEnum.Dameng:
                    return "达梦";
                case ORM.DbTypeEnum.Kingbase:
                    return "人大金仓";
                default:
                    return lj.DataBaseType.ToString();
            }
        }

        #region 内部解析

        /// <summary>
        /// 清掉复制粘贴带入的换行/制表符与首尾空白(保留串内空格,密码可能含空格)
        /// </summary>
        private static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (c == '\r' || c == '\n' || c == '\t')
                {
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// 从配置片段中提取 url / username / password。
        /// 兼容 YAML(url: xxx)与 properties(url=xxx)两种风格,键名允许带前缀(spring.datasource.url)
        /// 与 - / _ 分隔;取不到 url 时返回空字典,调用方退回"整串解析"模式。
        /// </summary>
        private static Dictionary<string, string> ExtractKeyValues(string input)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(input))
            {
                return result;
            }

            foreach (var rawLine in input.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#' || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                // 取 ':' 与 '=' 中最先出现的作为键值分隔符(键名不会含这两个字符,
                // 而 url 的值里两者都可能出现,所以只能取最早的)
                var ci = line.IndexOf(':');
                var ei = line.IndexOf('=');
                int sep;
                if (ci < 0)
                {
                    sep = ei;
                }
                else if (ei < 0)
                {
                    sep = ci;
                }
                else
                {
                    sep = Math.Min(ci, ei);
                }

                if (sep <= 0)
                {
                    continue;
                }

                var key = NormalizeKey(line.Substring(0, sep));
                if (key == null || result.ContainsKey(key))
                {
                    continue;
                }

                result[key] = Unquote(StripComment(line.Substring(sep + 1)));
            }

            return result;
        }

        /// <summary>
        /// 键名归一化:小写、去掉 - _ 与空白,多点名(spring.datasource.url)只取最后一段;
        /// 只认 url / username / password 三类,其余返回 null
        /// </summary>
        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var k = key.Trim().ToLowerInvariant()
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Replace(" ", string.Empty);

            var dot = k.LastIndexOf('.');
            if (dot >= 0)
            {
                k = k.Substring(dot + 1);
            }

            switch (k)
            {
                case "url":
                case "jdbcurl":
                case "datasourceurl":
                case "connectionstring":
                    return "url";

                case "username":
                case "user":
                case "userid":
                case "uid":
                    return "username";

                case "password":
                case "pwd":
                case "passwd":
                    return "password";

                default:
                    return null;
            }
        }

        /// <summary>
        /// 去掉 YAML 行尾注释(# 前必须有空白,避免误伤值里的 #)
        /// </summary>
        private static string StripComment(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            for (var i = 1; i < value.Length; i++)
            {
                if (value[i] == '#' && char.IsWhiteSpace(value[i - 1]))
                {
                    return value.Substring(0, i);
                }
            }
            return value;
        }

        /// <summary>
        /// 去掉值两端成对的引号(YAML 里 url 常写成 "jdbc:..." 或 'jdbc:...')
        /// </summary>
        private static string Unquote(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            var v = value.Trim();
            if (v.Length >= 2 &&
                ((v[0] == '"' && v[v.Length - 1] == '"') || (v[0] == '\'' && v[v.Length - 1] == '\'')))
            {
                v = v.Substring(1, v.Length - 2);
            }
            return v;
        }

        /// <summary>
        /// 拆出 scheme 与主体。兼容 jtds:sqlserver 这种两段式驱动名。
        /// </summary>
        private static void SplitScheme(string text, out string scheme, out string body)
        {
            scheme = string.Empty;
            body = text;

            var i = text.IndexOf(':');
            if (i <= 0)
            {
                return;
            }

            var first = text.Substring(0, i);
            if (first.Equals("jtds", StringComparison.OrdinalIgnoreCase))
            {
                var j = text.IndexOf(':', i + 1);
                if (j > 0 && text.Substring(i + 1, j - i - 1).Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
                {
                    scheme = "jtds:sqlserver";
                    body = text.Substring(j + 1);
                    return;
                }
            }

            scheme = first;
            body = text.Substring(i + 1);
        }

        /// <summary>
        /// 通用形式:[//][user[:password]@]host[:port][/database][?k=v&amp;...]
        /// </summary>
        private static void ParseCommon(string body, LianJie lj, string typeName)
        {
            var rest = body ?? string.Empty;

            // 查询参数(先切掉,避免其中的 / 与 @ 干扰后面的切分)
            string paramText = null;
            var qi = rest.IndexOf('?');
            if (qi >= 0)
            {
                paramText = rest.Substring(qi + 1);
                rest = rest.Substring(0, qi);
            }

            if (rest.StartsWith("//", StringComparison.Ordinal))
            {
                rest = rest.Substring(2);
            }

            // SqlServer 的 JDBC 串用分号传参://host:1433;databaseName=x;user=y;password=z
            // 只在 SqlServer 上按分号切,避免误伤其他库里密码中的分号
            if (paramText == null && typeName == "SqlServer")
            {
                var si = rest.IndexOf(';');
                if (si >= 0)
                {
                    paramText = rest.Substring(si + 1).Replace(';', '&');
                    rest = rest.Substring(0, si);
                }
            }

            // 路径(库名)
            string path = null;
            var pi = rest.IndexOf('/');
            if (pi >= 0)
            {
                path = rest.Substring(pi + 1);
                rest = rest.Substring(0, pi);
            }

            // 账号信息(按最后一个 @ 切,密码里未转义的 @ 才不会被截断)
            var ai = rest.LastIndexOf('@');
            if (ai >= 0)
            {
                var userInfo = rest.Substring(0, ai);
                rest = rest.Substring(ai + 1);

                var ci = userInfo.IndexOf(':');
                if (ci >= 0)
                {
                    if (ci > 0)
                    {
                        lj.UserName = Decode(userInfo.Substring(0, ci));
                    }
                    lj.PassWord = Decode(userInfo.Substring(ci + 1));
                }
                else if (userInfo.Length > 0)
                {
                    lj.UserName = Decode(userInfo);
                }
            }

            // host:port(兼容 [::1]:3306 形式的 IPv6 字面量)
            rest = rest.Trim();
            if (rest.Length > 0)
            {
                string host = rest;
                string port = null;

                if (rest[0] == '[')
                {
                    var ei = rest.IndexOf(']');
                    if (ei > 0)
                    {
                        host = rest.Substring(1, ei - 1);
                        var after = rest.Substring(ei + 1);
                        if (after.StartsWith(":", StringComparison.Ordinal))
                        {
                            port = after.Substring(1);
                        }
                    }
                }
                else
                {
                    var ci = rest.LastIndexOf(':');
                    if (ci >= 0)
                    {
                        host = rest.Substring(0, ci);
                        port = rest.Substring(ci + 1);
                    }
                }

                if (!string.IsNullOrWhiteSpace(host))
                {
                    lj.Host = host.Trim();
                }
                if (!string.IsNullOrWhiteSpace(port))
                {
                    lj.Port = port.Trim();
                }
            }

            // 库名
            if (!string.IsNullOrWhiteSpace(path))
            {
                var db = Decode(path.Trim().Trim('/'));
                if (db.Length > 0)
                {
                    lj.DataBaseName = db;
                }
            }

            ApplyParams(paramText, lj);
        }

        /// <summary>
        /// Oracle:jdbc:oracle:thin:@//host:1521/orcl、@host:1521:orcl、@host:1521/orcl
        /// </summary>
        private static void ParseOracle(string body, LianJie lj)
        {
            var rest = body ?? string.Empty;

            var ai = rest.LastIndexOf('@');
            if (ai >= 0)
            {
                // @ 之前可能是 thin: 或 thin:scott/tiger
                var before = rest.Substring(0, ai);
                var colon = before.IndexOf(':');
                var credential = colon >= 0 ? before.Substring(colon + 1) : string.Empty;
                var sp = credential.IndexOf('/');
                if (sp > 0)
                {
                    lj.UserName = Decode(credential.Substring(0, sp));
                    lj.PassWord = Decode(credential.Substring(sp + 1));
                }
                rest = rest.Substring(ai + 1);
            }

            if (rest.StartsWith("//", StringComparison.Ordinal))
            {
                rest = rest.Substring(2);
            }

            var qi = rest.IndexOf('?');
            if (qi >= 0)
            {
                rest = rest.Substring(0, qi);
            }

            rest = rest.Trim();
            if (rest.Length == 0)
            {
                return;
            }

            // 服务名/SID:优先 /service,否则按 host:port:SID 三段式取
            string service = null;
            var slash = rest.IndexOf('/');
            if (slash >= 0)
            {
                service = rest.Substring(slash + 1);
                rest = rest.Substring(0, slash);
            }
            else
            {
                var parts = rest.Split(':');
                if (parts.Length >= 3)
                {
                    service = parts[2];
                    rest = parts[0] + ":" + parts[1];
                }
            }

            var ci = rest.LastIndexOf(':');
            if (ci >= 0)
            {
                var host = rest.Substring(0, ci);
                var port = rest.Substring(ci + 1);
                if (!string.IsNullOrWhiteSpace(host))
                {
                    lj.Host = host.Trim();
                }
                if (!string.IsNullOrWhiteSpace(port))
                {
                    lj.Port = port.Trim();
                }
            }
            else if (rest.Length > 0)
            {
                lj.Host = rest;
            }

            if (!string.IsNullOrWhiteSpace(service))
            {
                var svc = Decode(service.Trim().Trim('/'));
                if (svc.Length > 0)
                {
                    lj.ServiceName = svc;
                }
            }
        }

        /// <summary>
        /// SQLite:文件路径放 Host(与 XinJianLocal 的用法一致)
        /// </summary>
        private static void ParseSqlite(string body, LianJie lj)
        {
            var rest = (body ?? string.Empty).Trim();

            var qi = rest.IndexOf('?');
            if (qi >= 0)
            {
                rest = rest.Substring(0, qi);
            }

            if (rest.StartsWith("//", StringComparison.Ordinal))
            {
                rest = rest.Substring(2);
            }

            // sqlite:///D:/a.db 会剩 /D:/a.db,去掉盘符前多余的斜杠(不误伤 /home/... 这类 unix 路径)
            if (rest.Length > 2 && rest[0] == '/' && char.IsLetter(rest[1]) && rest[2] == ':')
            {
                rest = rest.Substring(1);
            }

            if (!string.IsNullOrWhiteSpace(rest))
            {
                lj.Host = Decode(rest.Trim());
            }
        }

        /// <summary>
        /// 解析 ? 或 ; 形式的附加参数,回填账号/库名/MongoDB 认证参数
        /// </summary>
        private static void ApplyParams(string paramText, LianJie lj)
        {
            if (string.IsNullOrWhiteSpace(paramText))
            {
                return;
            }

            var extras = new List<string>();
            foreach (var pair in paramText.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                var key = (eq >= 0 ? pair.Substring(0, eq) : pair).Trim();
                var value = eq >= 0 ? pair.Substring(eq + 1).Trim() : string.Empty;
                if (key.Length == 0)
                {
                    continue;
                }

                switch (key.ToLowerInvariant())
                {
                    case "user":
                    case "username":
                    case "uid":
                    case "userid":
                        if (string.IsNullOrEmpty(lj.UserName))
                        {
                            lj.UserName = Decode(value);
                        }
                        break;

                    case "password":
                    case "pwd":
                    case "passwd":
                        if (string.IsNullOrEmpty(lj.PassWord))
                        {
                            lj.PassWord = Decode(value);
                        }
                        break;

                    case "database":
                    case "databasename":
                    case "dbname":
                    case "db":
                    case "initialcatalog":
                        if (string.IsNullOrEmpty(lj.DataBaseName))
                        {
                            lj.DataBaseName = Decode(value);
                        }
                        break;

                    case "servicename":
                        if (string.IsNullOrEmpty(lj.ServiceName))
                        {
                            lj.ServiceName = Decode(value);
                        }
                        break;

                    case "currentschema":
                    case "schema":
                        if (string.IsNullOrEmpty(lj.ServiceName))
                        {
                            lj.ServiceName = Decode(value);
                        }
                        break;

                    case "authsource":
                        lj.AuthSource = Decode(value);
                        break;

                    case "authmechanism":
                        lj.AuthMechanism = Decode(value);
                        break;

                    default:
                        extras.Add(pair);
                        break;
                }
            }

            // 剩余参数只有 MongoDB 用得上(附加连接参数)
            if (extras.Count > 0 && lj.NoDbType == "MongoDB")
            {
                lj.UrlParams = string.Join("&", extras);
            }
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            try
            {
                return Uri.UnescapeDataString(value);
            }
            catch
            {
                return value;
            }
        }

        #endregion
    }
}
