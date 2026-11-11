namespace WDB.Entity
{
    /// <summary>
    /// 待执行的SQL语句
    /// </summary>
    public class SqlItem
    {
        /// <summary>
        /// SQL语句
        /// </summary>
        public string Sql { get; set; }

        /// <summary>
        /// 执行失败时是否忽略(用于"存在则删除"之类的辅助语句)
        /// </summary>
        public bool IgnoreError { get; set; }

        public override string ToString()
        {
            return Sql;
        }
    }
}
