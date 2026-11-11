using System.Collections.Generic;

namespace NetFrameWork.Database.CreateEntity
{
    /// <summary>
    /// 数据库操作接口
    /// </summary>
    public interface IDal
    {
        /// <summary>
        /// 获取数据库名
        /// </summary>
        List<Dictionary<string, string>> GetAllTables(ORM orm);
        /// <summary>
        /// 获取表的所有字段名及字段类型
        /// </summary>
        List<Dictionary<string, string>> GetAllColumns(string tableName, ORM orm);
        /// <summary>
        /// 类型转换
        /// </summary>
        string ConvertDataType(Dictionary<string, string> column);
        /// <summary>
        /// 查询表DDL
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="orm"></param>
        /// <returns></returns>
        string GetDDL(string tableName, ORM orm);
    }
}
