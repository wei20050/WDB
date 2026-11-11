namespace WDB.Entity
{
    /// <summary>
    /// 导入SQL的日志条目
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class SqlImportLogItem
    {
        /// <summary>
        /// 时间
        /// </summary>
        public string Time { get; set; }

        /// <summary>
        /// 语句摘要(截断后的单行显示文本)
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 完整语句(供双击查看)
        /// </summary>
        public string Sql { get; set; }

        /// <summary>
        /// 结果说明(成功/失败原因、影响行数、耗时)
        /// </summary>
        public string Detail { get; set; }

        /// <summary>
        /// 是否失败
        /// </summary>
        public bool IsError { get; set; }
    }
}
