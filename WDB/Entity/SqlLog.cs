using System;

namespace WDB.Entity
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class SqlLog
    {
        public string Info { get; set; }
        public DateTime Time { get; set; }
        public string Sql { get; set; }
        public int Count { get; set; }
        public string Ms { get; set; }
        public bool Status { get; set; }
    }
}
