using System.Collections.Generic;
using PropertyChanged;
using WDB.Entity;

namespace WDB
{
    [AddINotifyPropertyChangedInterface]
    public class SqlLogWindowModel
    {
        public string Str { get; set; }
        public List<SqlLog> SqlLogList { get; set; }
        public List<SqlLog> SqlLogBindList { get; set; }
    }
}
