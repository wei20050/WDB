using System.Collections.Generic;

namespace WDB.Entity
{
    /// <summary>
    /// 过滤规则(可用于数据库名或表名)
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class FilterConfig
    {
        /// <summary>
        /// 是否启用过滤
        /// </summary>
        public bool IsFilter { get; set; }

        /// <summary>
        /// 包含的列表(白名单,支持 _ 单字符 * 多字符)
        /// </summary>
        public List<string> IncludingList { get; set; }

        /// <summary>
        /// 排除的列表(黑名单,支持 _ 单字符 * 多字符)
        /// </summary>
        public List<string> ExcludeList { get; set; }
    }
}
