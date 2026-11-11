using System.Collections.Generic;

namespace WDB.Common
{
    public class GConst
    {
        public const string KEY = "ORMEncryptKey20050KKKKKKKKKKKKKK";

        /// <summary>
        /// SQL常用关键字
        /// </summary>
        public static readonly List<string> Sys_SqlKeywords = new List<string> {
            "ADD",        "ALL",        "ALTER",      "AND",       "ANY",       "AS",        "ASC",
            "BACKUP",     "BETWEEN",    "BY",         "CASE",      "CHECK",     "COLUMN",    "CONSTRAINT",
            "CREATE",     "DATABASE",   "DEFAULT",    "DELETE",    "DESC",      "DISTINCT",
            "DROP",       "ELSE",       "EXCEPT",     "EXISTS",    "FOREIGN",   "FROM",      "FULL",
            "GROUP",      "HAVING",     "IN",         "INDEX",     "INNER",     "INSERT",
            "INTO",       "IS",         "JOIN",       "LEFT",      "LIKE",      "LIMIT",
            "NOT",        "NULL",       "OR",         "ORDER",     "OUTER",     "PRIMARY",
            "REFERENCES", "RIGHT",      "SELECT",     "SET",       "TABLE",     "TOP",
            "TRUNCATE",   "UNION",      "UNIQUE",     "UPDATE",    "VALUES",    "VIEW",
            "WHERE",      "WITH"
            };

        /// <summary>
        /// SQL常用函数
        /// </summary>
        public static readonly List<string> Sys_SqlFuns = new List<string>
        {
            "COUNT",   "SUM",   "AVG",  "MIN",  "MAX",  "UPPER",    "LOWER",    "SUBSTRING",    "CONCAT",   "DATEPART"
        };

        public static readonly Dictionary<string, string> Sys_GencodeKeywords = new Dictionary<string, string>
        {
            ["Model"] = "",
            ["ClassName"] = "类名",
            ["TableName"] = "表名",
            ["Description"] = "表描述",
            ["Properties"] = "列集合",
            ["PropertyName"] = "类中属性名",
            ["Type"] = "类中的属性类型",
            ["DbCloumnName"] = "表字段名",
            ["IsPrimaryKey"] = "是否主键",
            ["IsIdentity"] = "是否自增",
            ["IsNullable"] = "是否为空"
        };
    }
}
