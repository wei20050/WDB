using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NetFrameWork.Database.CreateEntity
{

    /// <summary>
    /// Oracle数据库DAL
    /// </summary>
    public class OracleDal : IDal
    {
        /// <summary>
        /// 获取所有表信息
        /// </summary>
        public List<Dictionary<string, string>> GetAllTables(ORM orm)
        {
            var dt = orm.ExecuteSet(@"
                select a.TABLE_NAME,b.COMMENTS 
                from user_tables a,user_tab_comments b 
                WHERE a.TABLE_NAME=b.TABLE_NAME").Tables[0];
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
            var dt = orm.ExecuteSet($@"
            select 
                TABLE_NAME,
                COLUMN_NAME,
                COLUMN_ID,
                DATA_TYPE,
                DATA_LENGTH,
                DATA_PRECISION,
                NULLABLE,
                DATA_DEFAULT,
                COMMENTS,
                CONSTRAINT_TYPE
            from (
                select 
                    a.TABLE_NAME,
                    a.COLUMN_NAME,
                    a.COLUMN_ID,
                    a.DATA_TYPE,
                    a.DATA_LENGTH,
                    a.DATA_PRECISION,
                    a.DATA_SCALE,
                    a.NULLABLE,
                    a.DATA_DEFAULT,
                    b.COMMENTS,
                    c.CONSTRAINT_TYPE,
                    row_number() over(partition by a.TABLE_NAME, a.COLUMN_NAME order by a.COLUMN_ID) as rn
                from user_tab_columns a
                inner join user_col_comments b 
                    on a.TABLE_NAME = b.TABLE_NAME and a.COLUMN_NAME = b.COLUMN_NAME
                left join user_cons_columns d 
                    on a.TABLE_NAME = d.TABLE_NAME and a.COLUMN_NAME = d.COLUMN_NAME
                left join user_constraints c 
                    on d.TABLE_NAME = c.TABLE_NAME and d.CONSTRAINT_NAME = c.CONSTRAINT_NAME and c.CONSTRAINT_TYPE = 'P'
                where a.TABLE_NAME = '{tableName}'
            ) t
            where t.rn = 1
            order by COLUMN_ID").Tables[0];
            var result = new List<Dictionary<string, string>>();
            foreach (DataRow dr in dt.Rows)
            {
                var dic = new Dictionary<string, string>
                {
                    {"columns_name", dr["COLUMN_NAME"].ToString()},
                    {"notnull", dr["NULLABLE"].ToString() == "N" ? "1" : "0"},
                    {"comments", dr["COMMENTS"].ToString()},
                    {"data_type", dr["DATA_TYPE"].ToString()},
                    {"data_precision", dr["DATA_PRECISION"].ToString()},
                    { "default_value", dr["DATA_DEFAULT"]?.ToString() ?? "" }
                };

                if (dr["CONSTRAINT_TYPE"].ToString() == "P")
                {
                    dic.Add("constraint_type", dr["CONSTRAINT_TYPE"].ToString());
                }
                if (!dic.ContainsKey("constraint_type"))
                {
                    dic.Add("constraint_type", "");
                }
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
                case "NUMBER":
                    if (column["data_scale"].Trim() == "0")
                    {
                        if (column["data_precision"].Trim() != "" && int.Parse(column["data_precision"].Trim()) > 9)
                        {
                            dataType = column["notnull"] == "1" ? "long" : "long?";
                        }
                        else
                        {
                            dataType = column["notnull"] == "1" ? "int" : "int?";
                        }
                    }
                    else
                    {
                        dataType = column["notnull"] == "1" ? "decimal" : "decimal?";
                    }
                    break;
                case "LONG":
                    dataType = column["notnull"] == "1" ? "long" : "long?";
                    break;
                case "VARCHAR2":
                    dataType = "string";
                    break;
                case "NVARCHAR2":
                    dataType = "string";
                    break;
                case "CHAR":
                    dataType = "string";
                    break;
                case "DATE":
                    dataType = column["notnull"] == "1" ? "System.DateTime" : "System.DateTime?";
                    break;
                case "CLOB":
                    dataType = "string";
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
            var sql = $"SELECT DBMS_METADATA.GET_DDL('TABLE','{tableName}') AS ddl_sql FROM DUAL";
            return orm.ExecuteSet(sql).Tables[0].Rows[0]["ddl_sql"].ToString();
        }

    }
}
