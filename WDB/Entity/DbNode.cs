using System.Collections.Generic;

namespace WDB.Entity
{
    /// <summary>
    /// 连接树节点类型
    /// </summary>
    public enum DbNodeType
    {
        /// <summary>
        /// 连接(服务器)
        /// </summary>
        Connection,
        /// <summary>
        /// 数据库
        /// </summary>
        Database,
        /// <summary>
        /// 模式(schema)
        /// </summary>
        Schema,
        /// <summary>
        /// 表
        /// </summary>
        Table,
        /// <summary>
        /// 视图
        /// </summary>
        View,
        /// <summary>
        /// 占位节点(用于触发懒加载)
        /// </summary>
        Loading,
        /// <summary>
        /// Redis 库(0-15)
        /// </summary>
        RedisDb,
        /// <summary>
        /// MongoDB 数据库
        /// </summary>
        MongoDb
    }

    /// <summary>
    /// 连接树节点(统一承载 连接/数据库/模式/表/视图)
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class DbNode
    {
        /// <summary>
        /// 节点类型
        /// </summary>
        public DbNodeType NodeType { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 备注(表注释/库大小等)
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// 图标 pack uri
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// 是否加粗显示(连接/数据库/模式为容器节点)
        /// </summary>
        public bool IsBold { get; set; }

        /// <summary>
        /// 是否错误提示节点(加载失败文案以红字显示)
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// 悬浮提示(仅连接节点使用)
        /// </summary>
        public string ToolTipText { get; set; }

        /// <summary>
        /// 原始子节点集合(过滤前)
        /// </summary>
        public List<DbNode> AllChildren { get; set; }

        /// <summary>
        /// 绑定到树的子节点(过滤后)
        /// </summary>
        public List<DbNode> Children { get; set; }

        /// <summary>
        /// 是否已展开(绑定到 TreeViewItem.IsExpanded,重建树时保留状态)
        /// </summary>
        public bool IsExpanded { get; set; }

        /// <summary>
        /// 子节点是否已加载
        /// </summary>
        public bool IsLoaded { get; set; }

        /// <summary>
        /// 是否正在加载(防止重复加载)
        /// </summary>
        public bool IsLoading { get; set; }

        /// <summary>
        /// 上次加载是否失败:失败后重复展开/双击只更新提示,不再自动重试(右键"刷新"可重试)
        /// </summary>
        public bool LoadFailed { get; set; }

        /// <summary>
        /// 父节点
        /// </summary>
        public DbNode Parent { get; set; }

        /// <summary>
        /// 所属连接(表/视图/模式节点为其所在库的有效连接上下文)
        /// </summary>
        public LianJie LianJie { get; set; }

        /// <summary>
        /// 所属的根连接配置(用于读写过滤规则等连接级配置,不会被库/模式上下文覆盖)
        /// </summary>
        public LianJie RootLianJie { get; set; }

        /// <summary>
        /// 所在数据库(用于定位上下文)
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// 所在模式(用于定位上下文)
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// 表/视图/Redis库 节点对应的实体
        /// </summary>
        public Biao Biao { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
