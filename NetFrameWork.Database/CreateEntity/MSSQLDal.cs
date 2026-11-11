using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NetFrameWork.Database.CreateEntity
{
    /// <summary>
    /// MSSQL数据库DAL
    /// </summary>
    public class MssqlDal : IDal
    {
        /// <summary>
        /// 获取所有表信息
        /// </summary>
        public List<Dictionary<string, string>> GetAllTables(ORM orm)
        {
            var dt = orm.ExecuteSet(@"
                SELECT tbs.name as TABLE_NAME,ds.value as COMMENTS 
                FROM sys.tables tbs
                left join sys.extended_properties ds on ds.major_id=tbs.object_id 
                Where ds.minor_id=0 AND ds.class = 1").Tables[0];
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
            var dt = orm.ExecuteSet($@"SELECT 
    c.column_id,
    c.name AS column_name,
    ts.name AS data_type,
    c.max_length,
    c.precision,
    c.is_nullable,
    ISNULL(ds.value, '') AS comments,
    ISNULL(
        REPLACE(
            REPLACE(
                ISNULL(sm.definition, ''), 
                '(', ''), 
            ')', ''
        ), 
    '') AS default_value,
    CASE WHEN pk.index_column_id IS NOT NULL THEN 'PRI' ELSE '' END AS column_key
FROM sys.columns c
LEFT JOIN sys.extended_properties ds 
    ON ds.major_id = c.object_id AND ds.minor_id = c.column_id
LEFT JOIN sys.types ts 
    ON c.system_type_id = ts.system_type_id AND ts.user_type_id = c.user_type_id
LEFT JOIN sys.tables tbs 
    ON tbs.object_id = c.object_id
LEFT JOIN sys.default_constraints sm 
    ON c.default_object_id = sm.object_id
LEFT JOIN (
    SELECT ic.object_id, ic.column_id, ic.index_column_id
    FROM sys.indexes i
    JOIN sys.index_columns ic 
        ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.is_primary_key = 1
) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
WHERE 
    tbs.name = '{tableName}' 
ORDER BY 
    c.column_id").Tables[0];
            var result = new List<Dictionary<string, string>>();
            foreach (DataRow dr in dt.Rows)
            {
                var dic = new Dictionary<string, string>
                {
                    {"columns_name", dr["column_name"].ToString()},
                    {"notnull", dr["is_nullable"].ToString() == "False" ? "1" : "0"},
                    {"comments", dr["comments"].ToString()},
                    { "default_value", dr["default_value"]?.ToString() ?? "" }
                };
                var dataType = dr["data_type"].ToString();
                dic.Add("data_type", dataType);
                dic.Add("data_precision", dr["precision"].ToString());

                dic.Add("constraint_type", dr["column_key"].ToString() == "PRI" ? "P" : "");
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
            switch (column["data_type"])
            {
                case "int":
                    dataType = column["notnull"] == "1" ? "int" : "int?";
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
                case "datetime":
                    dataType = column["notnull"] == "1" ? "System.DateTime" : "System.DateTime?";
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
        public string GetDDL(string tableName, ORM orm)
        {
            var sql = @$"SELECT
    'CREATE TABLE [' + t.name + '] (' + 
    STUFF((
        SELECT ', [' + c.name + '] ' +
            ty.name +
            CASE WHEN ty.name IN ('varchar', 'nvarchar', 'char', 'nchar') 
                 THEN '(' + IIF(c.max_length = -1, 'MAX', CAST(c.max_length AS VARCHAR)) + ')' 
                 ELSE '' 
            END +
            CASE WHEN c.is_nullable = 0 THEN ' NOT NULL' ELSE ' NULL' END
        FROM sys.columns c
        JOIN sys.types ty ON c.system_type_id = ty.system_type_id
        WHERE c.object_id = t.object_id
        ORDER BY c.column_id
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') + ')' AS ddl_sql
FROM sys.tables t
WHERE t.name = '{tableName}'
AND SCHEMA_NAME(t.schema_id) = 'dbo';";
            return orm.ExecuteSet(sql).Tables[0].Rows[0]["ddl_sql"].ToString();
        }

    }
}
