using System;
using System.Collections.Generic;
using System.Text;

namespace NetFrameWork.Database.CreateEntity
{
    /// <summary>
    /// 人大金仓数据库DAL
    /// </summary>
    public class KdbDal : PgSqlDal
    {
        /// <summary>
        /// 查询表DDL
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="orm"></param>
        /// <returns></returns>
        public override string GetDDL(string tableName, ORM orm)
        {
            // 1. 获取你已经写好的列信息
            var columns = GetAllColumns(tableName, orm);

            // 数据库模式名（从连接串获取，和你原来逻辑一样）
            var connectionString = orm.MConnectionString;
            var start = connectionString.IndexOf("database=", StringComparison.Ordinal) + 9;
            var end = connectionString.IndexOf("user id=", StringComparison.OrdinalIgnoreCase);
            var owner = connectionString.Substring(start, end - start).Replace(";", "").Trim();

            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE {owner}.{tableName} (");

            // 存储主键字段名
            var pkColumns = new List<string>();

            // 2. 拼接所有字段
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                var colName = col["columns_name"];
                var dataType = col["data_type"];
                var notNull = col["notnull"] == "1" ? "NOT NULL" : "";
                var isPK = col["constraint_type"] == "P";

                if (isPK)
                    pkColumns.Add(colName);

                // 字段类型拼接（带长度）
                var typeStr = dataType;

                // 拼接字段行
                var line = $"    {colName} {typeStr} {notNull}".Trim();
                if (i < columns.Count - 1)
                    line += ",";

                sb.AppendLine(line);
            }

            // 3. 拼接主键约束
            if (pkColumns.Count > 0)
            {
                var pkStr = string.Join(", ", pkColumns);
                sb.AppendLine($",CONSTRAINT PK_{tableName} PRIMARY KEY ({pkStr})");
            }

            sb.AppendLine(");");

            return sb.ToString();
        }
    }
}
