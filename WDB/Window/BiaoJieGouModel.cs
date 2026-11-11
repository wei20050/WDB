using System.Collections.Generic;
using PropertyChanged;
using WDB.Entity;

namespace WDB
{
    [AddINotifyPropertyChangedInterface]
    public class BiaoJieGouModel
    {
        public string LianjieMing { get; set; }
        public string BiaoMing { get; set; }
        public string BiaoZhuShi { get; set; }
        public string SqlStr { get; set; }
        public List<ZiDuan> ZiDuanList { get; set; }
    }
}
