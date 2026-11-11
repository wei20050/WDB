using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NetFrameWork.Database.CreateEntity
{
    /// <summary>
    /// PG数据库DAL
    /// </summary>
    public class PgSqlDal : IDal
    {
        /// <summary>
        /// 获取所有表信息
        /// </summary>
        public List<Dictionary<string, string>> GetAllTables(ORM orm)
        {
            var connectionString = orm.MConnectionString;
            var start = connectionString.IndexOf("database=", StringComparison.Ordinal) + 9;
            var end = connectionString.IndexOf("user id=", StringComparison.Ordinal);
            var owner = connectionString.Substring(start, end - start).Replace(";", "");
            var dt = orm.ExecuteSet($@"SELECT TABLE_NAME as TABLE_NAME,TABLE_COMMENT as COMMENTS 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = '{owner}'").Tables[0];
            return (from DataRow dr in dt.Rows
                    select new Dictionary<string, string>
                {
                    {"table_name", dr["TABLE_NAME"].ToString()},
                    {"comments", dr["COMMENTS"].ToString()}
                }).ToList();
        }

        /// <summary>
        /// 获取表的所有字段名及字段类型
        /// </summary>
        public List<Dictionary<string, string>> GetAllColumns(string tableName, ORM orm)
        {
            var connectionString = orm.MConnectionString;
            var start = connectionString.IndexOf("SearchPath=", StringComparison.Ordinal) + 11;
            var end = connectionString.Length - 1;
            var owner = connectionString.Substring(start, end - start).Replace(";", "");
            var dt = orm.ExecuteSet($@"
            SELECT
                a.attname AS COLUMN_NAME,
                pg_catalog.format_type(a.atttypid, a.atttypmod) AS COLUMN_TYPE,
                CASE WHEN a.attnotnull THEN 'NO' ELSE 'YES' END AS IS_NULLABLE,
                COALESCE(pg_get_expr(d.adbin, d.adrelid), '') AS COLUMN_DEFAULT,
                COALESCE(col_description(a.attrelid, a.attnum), '') AS COLUMN_COMMENT,
                CASE WHEN EXISTS (
                    SELECT 1 FROM pg_index i
                    JOIN pg_attribute pa ON pa.attrelid = i.indrelid AND pa.attnum = ANY(i.indkey)
                    WHERE i.indrelid = a.attrelid AND i.indisprimary AND pa.attname = a.attname
                ) THEN 'PRI' ELSE '' END AS COLUMN_KEY
            FROM pg_catalog.pg_attribute a
            LEFT JOIN pg_catalog.pg_attrdef d ON a.attrelid = d.adrelid AND a.attnum = d.adnum
            WHERE 
                a.attrelid = '{owner}.{tableName}'::regclass
                AND a.attnum > 0 
                AND NOT a.attisdropped
            ORDER BY a.attnum").Tables[0];
            var result = new List<Dictionary<string, string>>();
            foreach (DataRow dr in dt.Rows)
            {
                var dic = new Dictionary<string, string>
                {
                    {"columns_name", dr["COLUMN_NAME"].ToString()},
                    {"notnull", dr["IS_NULLABLE"].ToString() == "NO" ? "1" : "0"},
                    {"comments", dr["COLUMN_COMMENT"].ToString()},
                    { "default_value", dr["COLUMN_DEFAULT"]?.ToString() ?? "" }
                };
                var dataType = dr["COLUMN_TYPE"].ToString();
                dic.Add("data_type", dataType);
                dic.Add("constraint_type", dr["COLUMN_KEY"].ToString() == "PRI" ? "P" : "");
                result.Add(dic);
            }
            return result;
        }

        /// <inheritdoc />
        /// <summary>
        /// 类型转换
        /// </summary>
        public string ConvertDataType(Dictionary<string, string> column)
        {
            string dataType;
            switch (column["data_type"])
            {
                case "int":
                    dataType = column["notnull"] == "1" ? "int" : "int?";
                    break;
                case "tinyint":
                    dataType = column["notnull"] == "1" ? "byte" : "byte?";
                    break;
                case "bigint":
                    dataType = column["notnull"] == "1" ? "long" : "long?";
                    break;
                case "decimal":
                    dataType = column["notnull"] == "1" ? "decimal" : "decimal?";
                    break;
                case "nvarchar":
                    dataType = "string";
                    break;
                case "varchar":
                    dataType = "string";
                    break;
                case "text":
                    dataType = "string";
                    break;
                // ReSharper disable once StringLiteralTypo
                case "ntext":
                    dataType = "string";
                    break;
                case "longtext":
                    dataType = "string";
                    break;
                case "timestamp":
                    dataType = column["notnull"] == "1" ? "System.DateTime" : "System.DateTime?";
                    break;
                case "date":
                    dataType = column["notnull"] == "1" ? "System.DateTime" : "System.DateTime?";
                    break;
                case "datetime":
                    dataType = column["notnull"] == "1" ? "System.DateTime" : "System.DateTime?";
                    break;
                case "float":
                    dataType = column["notnull"] == "1" ? "float" : "float?";
                    break;
                default:
                    throw new Exception("Type not found [" + column["data_type"] + "]");
            }
            return dataType;
        }

        /// <summary>
        /// 查询表DDL
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="orm"></param>
        /// <returns></returns>
        public virtual string GetDDL(string tableName, ORM orm)
        {
            var sql = $"SELECT pg_get_tabledef('{tableName}') AS ddl_sql";
            return orm.ExecuteSet(sql).Tables[0].Rows[0]["ddl_sql"].ToString();
        }

    }
}
