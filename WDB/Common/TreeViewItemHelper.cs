using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WDB.Entity;

namespace WDB.Common
{
    /// <summary>
    /// TreeViewItem帮助类
    /// </summary>
    public static class TreeViewItemHelper
    {
        // 缓存：节点唯一标识 → 展开状态
        private static readonly Dictionary<string, bool> _expandStateCache = new Dictionary<string, bool>();

        /// <summary>
        /// 保存TreeView所有节点的展开状态（过滤前调用）
        /// </summary>
        /// <param name="treeView">目标TreeView</param>
        public static void SaveExpandStates(this TreeView treeView)
        {
            _expandStateCache.Clear(); // 清空旧缓存
            // 遍历所有TreeViewItem并保存状态
            foreach (TreeViewItem item in GetFirstLevelTreeViewItems(treeView))
            {
                // 用节点的Name（或唯一ID）作为缓存Key（根据你的模型调整）
                if (item.DataContext is LianJie l && !string.IsNullOrEmpty(l.Id))
                {
                    string key = l.Id;
                    _expandStateCache[key] = item.IsExpanded;
                }
            }
        }

        /// <summary>
        /// 恢复TreeView节点的展开状态（过滤后调用）
        /// </summary>
        /// <param name="treeView">目标TreeView</param>
        public static void RestoreExpandStates(this TreeView treeView)
        {
            // 遍历所有TreeViewItem并恢复状态
            foreach (TreeViewItem item in GetFirstLevelTreeViewItems(treeView))
            {
                if (item.DataContext is LianJie l && !string.IsNullOrEmpty(l.Id))
                {
                    string key = l.Id;
                    if (_expandStateCache.TryGetValue(key, out bool isExpanded))
                    {
                        item.IsExpanded = isExpanded; // 恢复展开状态
                    }
                }
            }
        }

        /// <summary>
        /// 获取TreeView第一级所有TreeViewItem（UI层面）
        /// </summary>
        /// <param name="treeView">目标TreeView</param>
        /// <returns>第一级TreeViewItem列表</returns>
        public static List<TreeViewItem> GetFirstLevelTreeViewItems(this TreeView treeView)
        {
            var firstLevelItems = new List<TreeViewItem>();

            // 遍历TreeView的Items（第一级数据），生成对应的TreeViewItem
            foreach (var dataItem in treeView.Items)
            {
                // 延迟到UI渲染完成后执行
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    // 强制更新布局（确保容器生成）
                    treeView.UpdateLayout();
                    // 尝试获取容器
                    var treeViewItem = treeView.ItemContainerGenerator.ContainerFromItem(dataItem) as TreeViewItem;
                    if (treeViewItem != null)
                    {
                        firstLevelItems.Add(treeViewItem);
                    }
                }, DispatcherPriority.Loaded); // 优先级：Loaded（UI加载完成）
            }

            return firstLevelItems;
        }
    }
}
