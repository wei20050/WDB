// ReSharper disable StringLiteralTypo
namespace NetFrameWork.Database.SqlFunction
{
    internal class MsSqlFunction : SqlFunBase
    {
        public override string Substr(string str, int start, int length)
        {
            return $"SUBSTRING({str},{start},{length}))";
        }

        public override string Now()
        {
            return "GETDATE()";
        }

        public override string GetTableNames()
        {
            return "SELECT t.name AS name, ISNULL(ep.value, '') AS description FROM sys.tables t LEFT JOIN sys.extended_properties ep ON ep.major_id = t.object_id ORDER BY t.name";
        }

        public override string GetViews()
        {
            return "SELECT o.name,m.definition AS view_sql FROM sys.sql_modules m JOIN sys.objects o ON m.object_id = o.object_id WHERE o.type = 'V' ORDER BY o.name";
        }

    }
}
