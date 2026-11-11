using System.Collections.Generic;
using PropertyChanged;
using WDB.Entity;

namespace WDB
{
    [AddINotifyPropertyChangedInterface]
    public class BiaoJieGouNoDbModel
    {
        public string LianjieMing { get; set; }
        public List<NoDbData> InfoList { get; set; }
        public List<NoDbData> InfoBindList { get; set; }
    }
}
