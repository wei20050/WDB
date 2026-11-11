using System.Collections.ObjectModel;
using WDB.Entity;

namespace WDB.View
{
    /// <summary>
    /// Redis命令窗口:连接 + 逻辑库
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class RedisConsoleViewModel
    {
        /// <summary>
        /// 连接上下文
        /// </summary>
        public LianJie Lj { get; set; }

        /// <summary>
        /// 当前执行的逻辑库(相当于 SELECT)
        /// </summary>
        public long Db { get; set; }

        /// <summary>
        /// 可选逻辑库(Redis默认0-15,执行 SELECT 后会自动补进来)
        /// </summary>
        public ObservableCollection<long> DbList { get; set; } = new ObservableCollection<long>();
    }
}
