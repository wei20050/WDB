namespace WDB.Entity
{
    /// <summary>
    /// 导出SQL时的表/视图选项
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class DaoChuBiao
    {
        /// <summary>
        /// 是否勾选导出
        /// </summary>
        public bool IsChecked { get; set; } = true;

        /// <summary>
        /// 表/视图名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 备注(表注释)
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// 是否视图
        /// </summary>
        public bool IsView { get; set; }

        /// <summary>
        /// 所在模式(无模式层级的库为空)
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// 对应的表/视图实体
        /// </summary>
        public Biao Biao { get; set; }

        /// <summary>
        /// 该表所在的连接上下文(已带上数据库与模式,导出时直接使用)
        /// </summary>
        public LianJie LianJie { get; set; }

        /// <summary>
        /// 列表显示名(带模式前缀)
        /// </summary>
        public string ShowName => string.IsNullOrEmpty(SchemaName) ? Name : $"{SchemaName}.{Name}";

        /// <summary>
        /// 类型显示名(表/视图)
        /// </summary>
        public string TypeName => IsView ? "视图" : "表";
    }
}
