using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace WDB.Common
{
    public class UIHelp
    {

        /// <summary>
        /// 获取当前选中的单元格文本 支持多行选中，每行选中单元格值制表符分隔，多行换行分隔
        /// </summary>
        /// <returns></returns>
        public static string GetSelectedCellsPrecise(DataGrid dg)
        {
            // 1. 无选中单元格直接返回空
            if (dg.SelectedCells.Count == 0)
                return string.Empty;

            // 2. 获取所有列的索引映射（列→索引），保证列顺序一致
            var columnIndexMap = dg.Columns
                .Select((col, index) => new { Column = col, Index = index })
                .ToDictionary(x => x.Column, x => x.Index);

            // 3. 按行分组（选中的单元格必然是有效行，无需过滤）
            var rowGroups = dg.SelectedCells
                .GroupBy(cell => cell.Item) // 按行对象分组
                .ToList();

            // 4. 计算全局列范围：所有选中单元格的最左/最右列索引（核心收缩逻辑）
            var allSelectedColIndexes = dg.SelectedCells
                .Select(cell => columnIndexMap[cell.Column])
                .ToList();
            int minColIndex = allSelectedColIndexes.Min(); // 最左侧选中列
            int maxColIndex = allSelectedColIndexes.Max(); // 最右侧选中列
            int effectiveColCount = maxColIndex - minColIndex + 1; // 有效列数（仅保留选中列的左右边界）

            // 5. 遍历每行，按收缩后的列范围填充值（仅处理选中列边界内的单元格）
            var rowResults = new List<string>();
            foreach (var rowGroup in rowGroups)
            {
                // 初始化当前行有效列数组（仅包含最左到最右列），默认空字符串（兼容所有.NET版本）
                string[] rowValues = new string[effectiveColCount];
                for (int i = 0; i < rowValues.Length; i++)
                {
                    rowValues[i] = "";
                }

                // 为当前行选中的单元格赋值（映射到收缩后的相对列索引）
                foreach (var cellInfo in rowGroup)
                {
                    int originalColIndex = columnIndexMap[cellInfo.Column];
                    // 计算在收缩范围内的相对索引（保证列对齐）
                    int relativeColIndex = originalColIndex - minColIndex;
                    if (relativeColIndex >= 0 && relativeColIndex < effectiveColCount)
                    {
                        // 获取单元格显示文本（和界面一致）
                        string cellText = cellInfo.Column.GetCellContent(cellInfo.Item) is TextBlock tb
                            ? tb.Text
                            : string.Empty;
                        rowValues[relativeColIndex] = cellText;
                    }
                }

                // 每行按制表符分隔（仅保留有效列范围，保证跨行对齐）
                rowResults.Add(string.Join("\t", rowValues));
            }

            // 6. 多行用换行符分隔，返回最终结果
            return string.Join("\n", rowResults);
        }

        /// <summary>
        /// 从 DataGridCell 安全获取归属的 DataGrid
        /// </summary>
        public static DataGrid GetOwnerDataGrid(DataGridCell cell)
        {
            if (cell == null) return null;

            DependencyObject parent = VisualTreeHelper.GetParent(cell);

            while (parent != null && !(parent is DataGrid))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as DataGrid;
        }

        /// <summary>
        /// 获取已选列
        /// </summary>
        /// <returns></returns>
        public static List<string> GetColumns(DataGrid dg)
        {
            var list = new List<string>();
            foreach (var cell in dg.SelectedCells)
            {
                var str = cell.Column?.Header?.ToString();
                if (string.IsNullOrEmpty(str) || list.Any(p => p == str))
                {
                    continue;
                }
                list.Add(str);
            }
            return list;
        }

        /// <summary>
        /// 获取指定DataGrid中，鼠标坐标对应的单元格
        /// </summary>
        /// <param name="dataGrid">目标DataGrid</param>
        /// <param name="mousePosition">鼠标相对于DataGrid的坐标（关键：坐标基准必须是DataGrid）</param>
        /// <returns>鼠标下的单元格（无则返回null）</returns>
        public static DataGridCell GetCellUnderMouse(DataGrid dataGrid, Point mousePosition)
        {
            if (dataGrid == null) return null;
            // 命中测试：获取鼠标位置对应的视觉元素
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(dataGrid, mousePosition);
            if (hitTestResult?.VisualHit == null) return null;

            // 向上遍历视觉树，找到DataGridCell（核心逻辑）
            DependencyObject currentObj = hitTestResult.VisualHit;
            while (currentObj != null && currentObj != dataGrid)
            {
                // 找到单元格则返回
                if (currentObj is DataGridCell cell)
                {
                    return cell;
                }
                // 跳过列头/行头（避免误判）
                if (currentObj is DataGridColumnHeader || currentObj is DataGridRowHeader)
                {
                    break;
                }
                currentObj = VisualTreeHelper.GetParent(currentObj);
            }
            return null;
        }

        /// <summary>
        /// 获取指定DataGrid中，鼠标坐标对应的列头
        /// </summary>
        /// <param name="dataGrid">目标DataGrid</param>
        /// <param name="mousePosition">鼠标相对于DataGrid的坐标（关键：坐标基准必须是DataGrid）</param>
        /// <returns>鼠标下的单元格（无则返回null）</returns>
        public static DataGridColumnHeader GetColumnHeaderUnderMouse(DataGrid dataGrid, Point mousePosition)
        {
            if (dataGrid == null) return null;
            // 命中测试：获取鼠标位置对应的视觉元素
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(dataGrid, mousePosition);
            if (hitTestResult?.VisualHit == null) return null;

            // 向上遍历视觉树，找到DataGridCell（核心逻辑）
            DependencyObject currentObj = hitTestResult.VisualHit;
            while (currentObj != null && currentObj != dataGrid)
            {
                // 找到列头则返回
                if (currentObj is DataGridColumnHeader header)
                {
                    return header;
                }
                // 跳过单元格/行头（避免误判）
                if (currentObj is DataGridCell || currentObj is DataGridRowHeader)
                {
                    break;
                }
                currentObj = VisualTreeHelper.GetParent(currentObj);
            }
            return null;
        }

        /// <summary>
        /// 获取指定DataGrid中，鼠标坐标对应的行头
        /// </summary>
        /// <param name="dataGrid">目标DataGrid</param>
        /// <param name="mousePosition">鼠标相对于DataGrid的坐标（关键：坐标基准必须是DataGrid）</param>
        /// <returns>鼠标下的单元格（无则返回null）</returns>
        public static DataGridRowHeader GetRowHeaderUnderMouse(DataGrid dataGrid, Point mousePosition)
        {
            if (dataGrid == null) return null;
            // 命中测试：获取鼠标位置对应的视觉元素
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(dataGrid, mousePosition);
            if (hitTestResult?.VisualHit == null) return null;

            // 向上遍历视觉树，找到DataGridCell（核心逻辑）
            DependencyObject currentObj = hitTestResult.VisualHit;
            while (currentObj != null && currentObj != dataGrid)
            {
                // 找到行头则返回
                if (currentObj is DataGridRowHeader rowHeader)
                {
                    return rowHeader;
                }
                // 跳过单元格/列头（避免误判）
                if (currentObj is DataGridCell || currentObj is DataGridColumnHeader)
                {
                    break;
                }
                currentObj = VisualTreeHelper.GetParent(currentObj);
            }
            return null;
        }

    }
}
