using System.Text;
// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Global

namespace NetFrameWork.Database.SqlFunction
{
    /// <summary>
    /// 基于Mysql的sql函数基类
    /// </summary>
    public class SqlFunBase
    {

        /// <summary>
        /// 返回字符串截取函数
        /// </summary>
        /// <returns></returns>
        public virtual string Substr(string str, int start, int length)
        {
            return $"SUBSTR({str},{start},{length})";
        }

        /// <summary>
        /// 返回当前时间函数
        /// </summary>
        /// <returns></returns>
        public virtual string Now()
        {
            return "NOW()";
        }

        /// <summary>
        /// 最小值
        /// </summary>
        /// <param name="paras"></param>
        /// <returns></returns>
        public virtual string Least(params string[] paras)
        {
            if (paras == null || paras.Length == 0)
            {
                return "LEAST()";
            }
            var sb = new StringBuilder("LEAST(");
            foreach (var para in paras)
            {
                sb.AppendFormat("{0},", para);
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append(")");
            return sb.ToString();
        }

        /// <summary>
        /// 如果是null值设置成默认值
        /// </summary>
        /// <param name="colName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public virtual string IfNull(string colName, string defaultValue)
        {
            return $"IFNULL({colName},'{defaultValue}')";
        }

        /// <summary>
        /// 最大值
        /// </summary>
        /// <returns></returns>
        public virtual string Max(string str)
        {
            return $"MAX({str})";
        }

        /// <summary>
        /// 最小值
        /// </summary>
        /// <returns></returns>
        public virtual string Min(string str)
        {
            return $"MIN({str})";
        }

        /// <summary>
        /// 获取所有表名
        /// </summary>
        /// <returns></returns>
        public virtual string GetTableNames()
        {
            return
                "select TABLE_NAME as name, TABLE_COMMENT as description from information_schema.tables where  TABLE_SCHEMA=database() AND TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME";
        }

        /// <summary>
        /// 获取所有视图
        /// </summary>
        /// <returns></returns>
        public virtual string GetViews()
        {
            return
                "SELECT TABLE_NAME AS name, VIEW_DEFINITION AS view_sql FROM INFORMATION_SCHEMA.VIEWS WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME";
        }

    }
}
