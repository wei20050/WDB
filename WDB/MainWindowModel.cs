using System.Collections.Generic;
using PropertyChanged;
using WDB.Entity;

namespace WDB
{
    [AddINotifyPropertyChangedInterface]
    public class MainWindowModel
    {
        public List<DbNode> LianJieBindList { get; set; }
        public string Key { get; set; }

    }
}
