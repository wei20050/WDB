namespace WDB.Entity
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class ZiDuan
    {
        public string Name { get; set; }
        public string Comment { get; set; }
        public string Type { get; set; }
        public bool IsPK { get; set; }
        public string DefaultValue { get; set; }
        public bool Notnull { get; set; }

        /// <summary>
        /// 原始字段名(生成 ALTER 语句时用于识别重命名)
        /// </summary>
        public string OldName { get; set; }
    }
}
