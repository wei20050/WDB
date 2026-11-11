using System;
using System.Collections.Generic;

namespace TSqlFormatter
{
    static class StandardKeywordRemapping
    {
        public static Dictionary<string, string> Instance { get; private set; }
        static StandardKeywordRemapping()
        {
            Instance = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Instance.Add("PROC", "PROCEDURE");
            Instance.Add("LEFT OUTER JOIN", "LEFT JOIN");
            Instance.Add("RIGHT OUTER JOIN", "RIGHT JOIN");
            Instance.Add("FULL OUTER JOIN", "FULL JOIN");
            Instance.Add("JOIN", "INNER JOIN");
            Instance.Add("TRAN", "TRANSACTION");
            Instance.Add("BEGIN TRAN", "BEGIN TRANSACTION");
            Instance.Add("COMMIT TRAN", "COMMIT TRANSACTION");
            Instance.Add("ROLLBACK TRAN", "ROLLBACK TRANSACTION");
            Instance.Add("BINARY VARYING", "VARBINARY");
            Instance.Add("CHAR VARYING", "VARCHAR");
            Instance.Add("CHARACTER", "CHAR");
            Instance.Add("CHARACTER VARYING", "VARCHAR");
            Instance.Add("DEC", "DECIMAL");
            Instance.Add("DOUBLE PRECISION", "FLOAT");
            Instance.Add("INTEGER", "INT");
            Instance.Add("NATIONAL CHARACTER", "NCHAR");
            Instance.Add("NATIONAL CHAR", "NCHAR");
            Instance.Add("NATIONAL CHARACTER VARYING", "NVARCHAR");
            Instance.Add("NATIONAL CHAR VARYING", "NVARCHAR");
            Instance.Add("NATIONAL TEXT", "NTEXT");
            Instance.Add("OUT", "OUTPUT");
        }
    }
}
