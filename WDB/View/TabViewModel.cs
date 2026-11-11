using System.Collections.Generic;
using System.Collections.ObjectModel;
using WDB.Entity;

namespace WDB.View
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class TabViewModel
    {
        public string SqlStr { get; set; }
        public LianJie Lj { get; set; }

        /// <summary>
        /// 全部连接(供查询页顶部的连接下拉选择)
        /// </summary>
        public List<LianJie> LjList { get; set; }

        public Biao B { get; set; }
        public string NoDbKey { get; set; }
        public NoDbData NoDbDataEdit { get; set; }
        public List<NoDbData> NoDbDataList { get; set; }
        public List<NoDbData> NoDbDataBindList { get; set; }

        /// <summary>
        /// 筛选列:勾选显示/隐藏结果表格的列
        /// </summary>
        public ObservableCollection<XuanLieItem> LieList { get; set; } = new ObservableCollection<XuanLieItem>();

        #region 分页(查询页里执行 SELECT 时用)

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
        /// 每页条数可选项
        /// </summary>
        public List<int> PageSizeList { get; } = new List<int> { 50, 100, 200, 500, 1000 };

        #endregion
    }
}
