namespace NetFrameWork.Database.SqlFunction
{
    internal class PgsqlFunction : SqlFunBase
    {
        public override string Now()
        {
            return "NOW()";
        }

        public override string GetTableNames()
        {
            return "SELECT c.relname AS name, COALESCE(d.description, '') AS description FROM pg_class c LEFT JOIN pg_description d ON c.oid = d.objoid AND d.objsubid = 0 WHERE c.relkind = 'r' AND c.relnamespace = ( SELECT oid FROM pg_namespace WHERE nspname = current_schema() ) ORDER BY c.relname";
        }

        public override string GetViews()
        {
            return "SELECT table_name AS name, view_definition AS view_sql FROM information_schema.views WHERE table_schema = current_schema() ORDER BY table_name";
        }
    }
}
