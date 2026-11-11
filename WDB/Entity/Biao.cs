using System.Collections.Generic;

namespace WDB.Entity
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class Biao
    {
        public string Name { get; set; }
        public string Comment { get; set; }
        public List<ZiDuan> ZiDuanList { get; set; }
        public bool IsView { get; set; }
        public string TableSql { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }
}
