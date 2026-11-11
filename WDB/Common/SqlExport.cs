using NetFrameWork.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using WDB.Entity;

namespace WDB.Common
{
    /// <summary>
    /// 按数据库类型把表结构/数据导出为 SQL 文件
    /// </summary>
    public class SqlExport
    {
        /// <summary>
        /// 单条 INSERT 合并的行数
        /// </summary>
        private const int BatchSize = 100;

        /// <summary>
        /// 单行超过该长度(主要为大字段)时单独成一条 INSERT
        /// </summary>
        private const int BigRowLength = 256 * 1024;

        /// <summary>
        /// 导出为 SQL 文件
        /// </summary>
        /// <param name="items">勾选的表/视图(各自携带所在库与模式的连接上下文)</param>
        /// <param name="withData">true:结构+数据 false:仅结构</param>
        /// <param name="withDrop">true:建表前带 DROP 语句</param>
        /// <param name="filePath">目标文件</param>
        /// <param name="token">取消标记</param>
        /// <param name="progress">进度回调(百分比,状态文本)</param>
        /// <param name="error">失败原因</param>
        public static bool Export(List<DaoChuBiao> items, bool withData, bool withDrop, string filePath,
            CancellationToken token, Action<int, string> progress, out string error)
        {
            error = null;
            if (items == null || items.Count == 0)
            {
                error = "没有需要导出的对象";
                return false;
            }
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                //不带 BOM,避免部分数据库客户端把 BOM 当成语句的一部分
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    WriteHead(sw, items[0].LianJie, items.Count, withData, withDrop);
                    for (var i = 0; i < items.Count; i++)
                    {
                        if (token.IsCancellationRequested)
                        {
                            error = "已取消导出";
                            return false;
                        }
                        var item = items[i];
                        //用 i+1 计算,保证最后一个对象进度到 100
                        progress?.Invoke((i + 1) * 100 / items.Count, $"正在导出 {item.ShowName} ({i + 1}/{items.Count})");
                        WriteObject(sw, item, withData, withDrop);
                    }
                    progress?.Invoke(100, "正在写入文件...");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 文件头注释
        /// </summary>
        private static void WriteHead(StreamWriter sw, LianJie l, int count, bool withData, bool withDrop)
        {
            sw.WriteLine("-- ----------------------------");
            sw.WriteLine("-- WDB 导出 SQL");
            if (l != null)
            {
                sw.WriteLine($"-- 连接名称: {l.Name}");
                sw.WriteLine($"-- 数据库类型: {l.DataBaseType}");
                sw.WriteLine($"-- 服务器: {l.Host}:{l.Port}");
                if (!string.IsNullOrEmpty(l.DataBaseName))
                {
                    sw.WriteLine($"-- 数据库: {l.DataBaseName}");
                }
                if (Db.HasSchemaLayer(l.DataBaseType) && !string.IsNullOrEmpty(l.ServiceName))
                {
                    sw.WriteLine($"-- 模式: {l.ServiceName}");
                }
            }
            sw.WriteLine($"-- 导出对象数: {count}");
            sw.WriteLine($"-- 导出内容: {(withData ? "结构 + 数据" : "仅结构")}{(withDrop ? " + 删除表语句" : string.Empty)}");
            sw.WriteLine($"-- 导出时间: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
            sw.WriteLine("-- ----------------------------");
            if (l != null && l.DataBaseType == ORM.DbTypeEnum.MySql)
            {
                sw.WriteLine("SET NAMES utf8mb4;");
            }
            sw.WriteLine();
        }

        /// <summary>
        /// 导出单个表/视图
        /// </summary>
        private static void WriteObject(StreamWriter sw, DaoChuBiao item, bool withData, bool withDrop)
        {
            sw.WriteLine();
            sw.WriteLine("-- ----------------------------");
            sw.WriteLine($"-- {item.TypeName}结构: {item.ShowName}");
            sw.WriteLine("-- ----------------------------");
            var ddl = item.IsView
                ? Db.GetViewDdlSilent(item.Biao, item.LianJie)
                : Db.GetDDLSilent(item.Biao, item.LianJie);
            if (string.IsNullOrWhiteSpace(ddl))
            {
                sw.WriteLine($"-- 未获取到 {item.ShowName} 的{(item.IsView ? "视图" : "建表")}语句");
            }
            else
            {
                //拿到建表语句才输出删除语句,避免删掉无法重建的表
                if (withDrop && item.LianJie != null)
                {
                    sw.WriteLine(GetDropSql(item));
                }
                sw.WriteLine(ddl.Trim().TrimEnd(';') + ";");
            }
            if (!withData || item.LianJie == null)
            {
                return;
            }
            try
            {
                WriteData(sw, item);
            }
            catch (Exception ex)
            {
                //单表数据失败不影响其它表,写注释继续
                Log.Error(ex.Message);
                sw.WriteLine($"-- 导出 {item.ShowName} 数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出表数据(INSERT 语句)
        /// </summary>
        private static void WriteData(StreamWriter sw, DaoChuBiao item)
        {
            var l = item.LianJie;
            var orm = Db.GetOrm(l);
            var conn = orm.GetConnection();
            var rows = 0L;
            try
            {
                conn.Open();
                if (l.DataBaseType == ORM.DbTypeEnum.Pgsql)
                {
                    //pg 时区
                    _ = orm.GetCommand("SET TIME ZONE 'Asia/Shanghai';", conn).ExecuteNonQuery();
                }
                var fullName = GetFullName(orm, l, item.Name, item.SchemaName);
                var identity = l.DataBaseType == ORM.DbTypeEnum.SqlServer && HasIdentity(orm, conn, item);
                using (var cmd = orm.GetCommand($"SELECT * FROM {fullName}", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    var cols = new List<string>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        cols.Add(orm.GetEscapeName(reader.GetName(i)));
                    }
                    if (cols.Count == 0)
                    {
                        return;
                    }
                    sw.WriteLine();
                    sw.WriteLine($"-- {item.ShowName} 数据");
                    if (identity)
                    {
                        sw.WriteLine($"SET IDENTITY_INSERT {fullName} ON;");
                    }
                    var prefix = $"INSERT INTO {fullName} ({string.Join(", ", cols)}) VALUES ";
                    //Oracle/达梦不支持一条 INSERT 写多行 VALUES
                    var singleRow = l.DataBaseType == ORM.DbTypeEnum.Oracle || l.DataBaseType == ORM.DbTypeEnum.Dameng;
                    var batch = singleRow ? 1 : BatchSize;
                    var values = new List<string>(batch);
                    while (reader.Read())
                    {
                        var vs = new string[cols.Count];
                        for (var i = 0; i < cols.Count; i++)
                        {
                            vs[i] = GetValue(reader.GetValue(i), l.DataBaseType);
                        }
                        var row = "(" + string.Join(", ", vs) + ")";
                        rows++;
                        //单行过大(如 longblob)时单独成一条 INSERT,避免与其它行合并后超出数据库包大小限制
                        var tooBig = row.Length > BigRowLength;
                        if (tooBig && values.Count > 0)
                        {
                            sw.WriteLine(prefix + string.Join(", ", values) + ";");
                            values.Clear();
                        }
                        if (tooBig)
                        {
                            sw.WriteLine(prefix + row + ";");
                            continue;
                        }
                        values.Add(row);
                        if (values.Count >= batch)
                        {
                            sw.WriteLine(prefix + string.Join(", ", values) + ";");
                            values.Clear();
                        }
                    }
                    if (values.Count > 0)
                    {
                        sw.WriteLine(prefix + string.Join(", ", values) + ";");
                    }
                    if (identity)
                    {
                        sw.WriteLine($"SET IDENTITY_INSERT {fullName} OFF;");
                    }
                }
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
            sw.WriteLine($"-- {item.ShowName} 数据行数: {rows}");
        }

        /// <summary>
        /// 对象的完整名称(有模式层级时带模式前缀)
        /// </summary>
        private static string GetFullName(ORM orm, LianJie l, string table, string schema)
        {
            var name = orm.GetEscapeName(table);
            if (Db.HasSchemaLayer(l.DataBaseType) && !string.IsNullOrEmpty(schema))
            {
                return orm.GetEscapeName(schema) + "." + name;
            }
            return name;
        }

        /// <summary>
        /// 生成删除表/视图的语句(按库类型决定是否支持 IF EXISTS)
        /// </summary>
        private static string GetDropSql(DaoChuBiao item)
        {
            var l = item.LianJie;
            var orm = Db.GetOrm(l);
            var fullName = GetFullName(orm, l, item.Name, item.SchemaName);
            var keyword = item.IsView ? "VIEW" : "TABLE";
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.Oracle:
                case ORM.DbTypeEnum.Dameng:
                    //不支持 IF EXISTS:对象不存在时会报"对象不存在",导入时可忽略该条错误
                    return $"DROP {keyword} {fullName};";
                default:
                    //MySql/Sqlite/Pg 系列/SqlServer(2016+) 均支持 IF EXISTS
                    return $"DROP {keyword} IF EXISTS {fullName};";
            }
        }

        /// <summary>
        /// SqlServer:是否含自增列(有自增列时插入需要显式开启 IDENTITY_INSERT)
        /// </summary>
        private static bool HasIdentity(ORM orm, DbConnection conn, DaoChuBiao item)
        {
            try
            {
                var schema = string.IsNullOrEmpty(item.SchemaName) ? "dbo" : item.SchemaName;
                var sql = $"SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID(N'{schema}.{item.Name}') AND is_identity = 1";
                var obj = orm.GetCommand(sql, conn).ExecuteScalar();
                return obj != null && Convert.ToInt32(obj) > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 值转 SQL 字面量
        /// </summary>
        private static string GetValue(object v, ORM.DbTypeEnum type)
        {
            if (v == null || v == DBNull.Value)
            {
                return "NULL";
            }
            if (v is byte[] bytes)
            {
                return ToHex(bytes, type);
            }
            if (v is bool b)
            {
                return IsPgLike(type) ? (b ? "true" : "false") : (b ? "1" : "0");
            }
            if (v is DateTime dt)
            {
                return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'";
            }
            if (v is TimeSpan ts)
            {
                return "'" + ts.ToString() + "'";
            }
            if (v is Guid g)
            {
                return "'" + g.ToString() + "'";
            }
            if (v is string s)
            {
                return Quote(s, type);
            }
            if (v is float f)
            {
                return f.ToString("R", CultureInfo.InvariantCulture);
            }
            if (v is double d)
            {
                return d.ToString("R", CultureInfo.InvariantCulture);
            }
            if (v is decimal m)
            {
                return m.ToString(CultureInfo.InvariantCulture);
            }
            //其余数值类型(byte/short/int/long 等)直接用不变区域输出
            if (v is IFormattable fm)
            {
                return fm.ToString(null, CultureInfo.InvariantCulture);
            }
            return Quote(v.ToString(), type);
        }

        /// <summary>
        /// 字符串加引号并按库类型转义
        /// </summary>
        private static string Quote(string s, ORM.DbTypeEnum type)
        {
            if (s == null)
            {
                return "NULL";
            }
            if (type == ORM.DbTypeEnum.MySql)
            {
                //MySQL 中反斜杠是转义符,需要二次转义
                return "'" + s.Replace("\\", "\\\\").Replace("'", "''")
                    .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\0", "\\0") + "'";
            }
            if (IsPgLike(type) && s.IndexOf('\\') >= 0)
            {
                //含反斜杠时用 E'' 转义字符串
                return "E'" + s.Replace("\\", "\\\\").Replace("'", "''") + "'";
            }
            //换行保留在字符串里是合法SQL,这里统一成 \n 以保持每行一条语句
            return "'" + s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("'", "''") + "'";
        }

        /// <summary>
        /// 二进制转各库的十六进制字面量
        /// </summary>
        private static string ToHex(byte[] bytes, ORM.DbTypeEnum type)
        {
            if (bytes.Length == 0)
            {
                //0x / HEXTORAW('') 这类空字面量数据库不认,空二进制统一用空串表示
                return type == ORM.DbTypeEnum.MySql || type == ORM.DbTypeEnum.SqlIte ? "X''" : "''";
            }
            //大字段逐字节拼 hex,避免 BitConverter 生成带分隔符的中间字符串
            const string hexChars = "0123456789ABCDEF";
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                sb.Append(hexChars[b >> 4]).Append(hexChars[b & 0xF]);
            }
            var hex = sb.ToString();
            switch (type)
            {
                case ORM.DbTypeEnum.MySql:
                case ORM.DbTypeEnum.SqlServer:
                    return "0x" + hex;
                case ORM.DbTypeEnum.SqlIte:
                    return "X'" + hex + "'";
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    return "'\\x" + hex + "'";
                case ORM.DbTypeEnum.Oracle:
                case ORM.DbTypeEnum.Dameng:
                    return "HEXTORAW('" + hex + "')";
                default:
                    return "'" + hex + "'";
            }
        }

        /// <summary>
        /// 是否 Pg 系列(Pgsql/Kingbase/GaussDB)
        /// </summary>
        private static bool IsPgLike(ORM.DbTypeEnum type)
        {
            return type == ORM.DbTypeEnum.Pgsql || type == ORM.DbTypeEnum.Kingbase || type == ORM.DbTypeEnum.GaussDB;
        }
    }
}
