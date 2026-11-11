namespace NetFrameWork.Database.SqlFunction
{
    internal class OracleFunction : SqlFunBase
    {
        public override string Now()
        {
            return "SYSDATE";
        }

        public override string GetTableNames()
        {
            return "SELECT t.TABLE_NAME AS name,COALESCE(c.COMMENTS, '') AS description FROM USER_TABLES t LEFT JOIN USER_TAB_COMMENTS c ON t.TABLE_NAME = c.TABLE_NAME WHERE t.TEMPORARY = 'N' ORDER BY t.TABLE_NAME";
        }
        public override string GetViews()
        {
            return "SELECT 	VIEW_NAME AS name, TEXT AS view_sql FROM USER_VIEWS ORDER BY VIEW_NAME";
        }
    }
}