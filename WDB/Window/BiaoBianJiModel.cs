using System.Collections.Generic;
using PropertyChanged;
using WDB.Entity;

namespace WDB
{
    [AddINotifyPropertyChangedInterface]
    public class BiaoBianJiModel
    {
        public string LianjieMing { get; set; }
        public string BiaoMing { get; set; }
        public string BiaoZhuShi { get; set; }

        /// <summary>
        /// 新建模式(表名可编辑)
        /// </summary>
        public bool IsNew { get; set; }

        public List<ZiDuan> ZiDuanList { get; set; }

        /// <summary>
        /// 当前数据库类型的常用字段类型
        /// </summary>
        public List<string> TypeList { get; set; }
    }
}
