using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    public partial class BiaoGuolv
    {
        public FilterConfig _cfg;

        private ObservableCollection<EditableListItem> _listBItems;
        private ObservableCollection<EditableListItem> _listPItems;

        /// <summary>
        /// 过滤规则编辑
        /// </summary>
        /// <param name="cfg">过滤配置(直接修改该对象)</param>
        /// <param name="targetName">过滤对象名称,如"数据库"或"表名"</param>
        /// <param name="scopeName">作用范围名称,如连接名或数据库名</param>
        public BiaoGuolv(FilterConfig cfg, string targetName, string scopeName)
        {
            InitializeComponent();
            _cfg = cfg;
            //通配符说明已挪到窗口内"启用过滤"上方,标题只留名称(标题栏放长句会被截断)
            Title = $"{targetName}过滤";
            GroupBoxMain.Header = $"配置{targetName}过滤规则 [{scopeName}]";
            cbQiyong.IsChecked = cfg.IsFilter;
            _listBItems = new ObservableCollection<EditableListItem>();
            _listPItems = new ObservableCollection<EditableListItem>();
            if (cfg.IncludingList != null)
            {
                foreach (var item in cfg.IncludingList)
                {
                    _listBItems.Add(new EditableListItem
                    {
                        Content = item
                    });
                }
            }
            if (cfg.ExcludeList != null)
            {
                foreach (var item in cfg.ExcludeList)
                {
                    _listPItems.Add(new EditableListItem
                    {
                        Content = item
                    });
                }
            }
            lbBaokuo.ItemsSource = _listBItems;
            lbPaichu.ItemsSource = _listPItems;
        }

        private void BtnTianjiaB(object sender, RoutedEventArgs e)
        {
            _listBItems.Add(new EditableListItem { IsEditing = true});
        }

        private void BtnQingkongB(object sender, RoutedEventArgs e)
        {
            if (Msg.Question($"确定要清空条目吗？"))
            {
                _listBItems.Clear();
            }
        }

        private void BtnTianjiaP(object sender, RoutedEventArgs e)
        {
            _listPItems.Add(new EditableListItem { IsEditing = true });
        }

        private void BtnQingkongP(object sender, RoutedEventArgs e)
        {
            if (Msg.Question($"确定要清空条目吗？"))
            {
                _listPItems.Clear();
            }
        }

        private void BtnQueding(object sender, RoutedEventArgs e)
        {
            _cfg.IsFilter = cbQiyong.IsChecked == true;
            var lb = new List<string>();
            foreach (var item in _listBItems)
            {
                lb.Add(item.Content);
            }
            _cfg.IncludingList = lb;
            var lp = new List<string>();
            foreach (var item in _listPItems)
            {
                lp.Add(item.Content);
            }
            _cfg.ExcludeList = lp;
            DialogResult = true;
            Close();
        }

        private void BtnQuxiao(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // 编辑按钮点击：切换到编辑模式
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            // 获取当前点击的按钮对应的项
            var btn = sender as Button;
            var item = btn.DataContext as EditableListItem;
            if (item != null)
            {
                // 先关闭其他项的编辑状态（避免多列同时编辑）
                foreach (var li in _listBItems) li.IsEditing = false;
                foreach (var li in _listPItems) li.IsEditing = false;
                // 开启当前项的编辑状态
                item.IsEditing = true;
            }
        }

        //删除按钮点击
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. 获取当前点击按钮对应的项
            if (!(sender is Button btn) || !(btn.DataContext is EditableListItem itemToDelete))
            {
                return;
            }

            if (!Msg.Question($"确定要删除「{itemToDelete.Content}」吗？"))
            {
                return;
            }
            //条目只会存在于其中一个列表中,定位后移除即可,无需 try/catch 吞异常
            if (_listBItems.Contains(itemToDelete))
            {
                _listBItems.Remove(itemToDelete);
            }
            else
            {
                _listPItems.Remove(itemToDelete);
            }
        }

        // 保存按钮点击：退出编辑模式，保存修改
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var item = btn.DataContext as EditableListItem;
            if (item != null)
            {
                item.IsEditing = false;
            }
        }

        // 取消按钮点击：恢复原始值，退出编辑模式
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var item = btn.DataContext as EditableListItem;
            if (item != null)
            {
                item.RevertOriginalContent();
            }
        }
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        }
    }
}
