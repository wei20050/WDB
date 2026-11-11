using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

namespace WDB.View
{
    /// <summary>
    /// 筛选列:维护"列显示/隐藏"的勾选项,并应用到结果表格(SQL 与 MongoDB 的新建查询页共用)
    /// </summary>
    public static class LieColumnHelper
    {
        /// <summary>
        /// 按结果集列补齐勾选项(已存在的保留原勾选状态)
        /// </summary>
        public static void EnsureItems(ObservableCollection<XuanLieItem> items, IEnumerable<DataTable> tables)
        {
            if (items == null || tables == null)
            {
                return;
            }
            foreach (var dt in tables)
            {
                if (dt == null)
                {
                    continue;
                }
                foreach (DataColumn col in dt.Columns)
                {
                    if (items.Any(p => p.Name == col.ColumnName))
                    {
                        continue;
                    }
                    items.Add(new XuanLieItem { Name = col.ColumnName, IsVisible = true });
                }
            }
        }

        /// <summary>
        /// 把勾选状态应用到一组结果表格
        /// </summary>
        public static void Apply(ObservableCollection<XuanLieItem> items, IEnumerable<DataTableEditDataGridView> grids)
        {
            if (items == null || grids == null)
            {
                return;
            }
            foreach (var grid in grids)
            {
                if (grid == null)
                {
                    continue;
                }
                foreach (var item in items)
                {
                    grid.SetColumnVisible(item.Name, item.IsVisible);
                }
            }
        }

        /// <summary>
        /// 从结果页签里取出所有表格(文本结果页签会被忽略)
        /// </summary>
        public static List<DataTableEditDataGridView> GetGrids(IEnumerable<TabModel> tabs)
        {
            return tabs == null
                ? new List<DataTableEditDataGridView>()
                : tabs.Select(p => p.Content).OfType<DataTableEditDataGridView>().ToList();
        }
    }
}
