using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NetFrameWork.Database.CreateEntity
{

    /// <summary>
    /// SQLite数据库DAL
    /// </summary>
    public class SqLiteDal : IDal
    {

        /// <summary>
        /// 获取数据库名
        /// </summary>
        public List<Dictionary<string, string>> GetAllTables(ORM orm)
        {
            // ReSharper disable once StringLiteralTypo
            var dt = orm.ExecuteSet("select tbl_name from sqlite_master where type='table'").Tables[0];
            return (from DataRow dr in dt.Rows select new Dictionary<string, string> { { "table_name", dr["tbl_name"].ToString() }, { "comments", "" } }).ToList();
        }

        /// <summary>
        /// 获取表的所有字段名及字段类型
        /// </summary>
        public List<Dictionary<string, string>> GetAllColumns(string tableName, ORM orm)
        {
            var dt = orm.ExecuteSet("PRAGMA table_info('" + tableName + "')").Tables[0];
            var result = new List<Dictionary<string, string>>();
            foreach (DataRow dr in dt.Rows)
            {
                var dic = new Dictionary<string, string>
                {
                    {"columns_name", dr["name"].ToString()},
                    {"notnull", dr["notnull"].ToString() == "NO" ? "1" : "0"},
                    {"comments", ""},
                    { "default_value", dr["dflt_value"]?.ToString() ?? "" }
                };
                var dataType = dr["type"].ToString();
                dic.Add("data_type", dataType);
                dic.Add("data_precision", "");

                dic.Add("constraint_type", dr["pk"].ToString() != "0" ? "P" : "");
                result.Add(dic);
            }
            return result;
        }

        /// <summary>
        /// 类型转换
        /// </summary>
        public string ConvertDataType(Dictionary<string, string> column)
        {
            string dataType;
            string type = column["data_type"].ToLower().Trim();
            switch (type)
            {
                case "integer":
                    dataType = column["notnull"] == "1" ? "int" : "int?";
                    break;
                case "bit":
                    dataType = column["notnull"] == "1" ? "int" : "int?";
                    break;
                case "bigint":
                    dataType = column["notnull"] == "1" ? "long" : "long?";
                    break;
                case "decimal":
                    dataType = column["notnull"] == "1" ? "decimal" : "decimal?";
                    break;
                case "tinyint":
                    dataType = column["notnull"] == "1" ? "int" : "int?";
                    break;
                case "smallint":
                    dataType = column["notnull"] == "1" ? "int" : "int?";
                    break;
                case "real":
                    dataType = column["notnull"] == "1" ? "double" : "double?";
                    break;
                case "float":
                    dataType = column["notnull"] == "1" ? "float" : "float?";
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
                case "time":
                    dataType = column["notnull"] == "1" ? "System.DateTime" : "System.DateTime?";
                    break;
                case "date":
                    dataType = column["notnull"] == "1" ? "System.DateTime" : "System.DateTime?";
                    break;
                case "datetime":
                    dataType = column["notnull"] == "1" ? "System.DateTime" : "System.DateTime?";
                    break;
                default:
                    throw new Exception("Type not found [" + type + "]");
            }
            return dataType;
        }

        /// <summary>
        /// 查询表DDL
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="orm"></param>
        /// <returns></returns>
        public string GetDDL(string tableName, ORM orm)
        {
            var sql = $"SELECT sql AS ddl_sql FROM sqlite_master WHERE name='{tableName}'";
            return orm.ExecuteSet(sql).Tables[0].Rows[0]["ddl_sql"].ToString();
        }

    }
}
