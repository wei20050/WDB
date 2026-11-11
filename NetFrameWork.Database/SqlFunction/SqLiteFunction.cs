using System.Text;

namespace NetFrameWork.Database.SqlFunction
{
    internal class SqLiteFunction : SqlFunBase
    {
        public override string Now()
        {
            return "DATE('NOW') || ' ' || TIME('NOW', 'LOCALTIME')";
        }

        public override string Least(params string[] paras)
        {
            var sb = new StringBuilder("MIN(");
            foreach (var para in paras)
            {
                sb.AppendFormat("{0},", para);
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append(")");
            return sb.ToString();
        }

        public override string GetTableNames()
        {
            return "select name,'' as description from sqlite_master where type='table' and name<>'sqlite_sequence' order by name";
        }

        /// <summary>
        /// 获取所有视图
        /// </summary>
        /// <returns></returns>
        public override string GetViews()
        {
            return "SELECT name, sql AS view_sql FROM sqlite_master WHERE type = 'view'";
        }

    }
}