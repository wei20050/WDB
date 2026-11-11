using System.Collections.Generic;
using System.Collections.ObjectModel;
using WDB.Entity;

namespace WDB.View
{
    /// <summary>
    /// "查看数据"页:表格 + 分页 + 筛选列
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class TableDataViewModel
    {
        /// <summary>
        /// 连接上下文
        /// </summary>
        public LianJie Lj { get; set; }

        /// <summary>
        /// 当前表/视图
        /// </summary>
        public Biao B { get; set; }

        /// <summary>
        /// 所在模式(导出SQL时定位对象用)
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// 每页条数(默认100)
        /// </summary>
        public int PageSize { get; set; } = 100;

        /// <summary>
        /// 当前页码(从1开始)
        /// </summary>
        public int PageIndex { get; set; } = 1;

        /// <summary>
        /// 是否还有下一页
        /// </summary>
        public bool HasNext { get; set; }

        /// <summary>
        /// 是否可以上一页
        /// </summary>
        public bool CanPrev { get; set; }

        /// <summary>
        /// 分页信息文本
        /// </summary>
        public string PageInfo { get; set; }

        /// <summary>
        /// 筛选列(勾选显示/隐藏列)
        /// </summary>
        public ObservableCollection<XuanLieItem> LieList { get; set; } = new ObservableCollection<XuanLieItem>();

        /// <summary>
        /// 每页条数可选项
        /// </summary>
        public List<int> PageSizeList { get; } = new List<int> { 50, 100, 200, 500, 1000 };
    }

    /// <summary>
    /// 筛选列里的单个列(是否显示)
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class XuanLieItem
    {
        /// <summary>
        /// 列名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否显示
        /// </summary>
        public bool IsVisible { get; set; } = true;
    }
}
