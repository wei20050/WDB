using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    /// <summary>
    /// 导出SQL(可勾选表,选择仅结构或结构+数据,导出为当前数据库类型的sql文件)
    /// </summary>
    public partial class DaoChuSql
    {
        private readonly List<DaoChuBiao> _all;
        private readonly string _defaultName;
        private CancellationTokenSource _cts;
        private bool _isBusy;
        private bool _cancel;
        private int _lastPercent = -1;

        /// <summary>
        /// 库/模式/连接级导出:可勾选多张表
        /// </summary>
        /// <param name="lj">连接上下文</param>
        /// <param name="items">该范围内的全部表/视图</param>
        /// <param name="defaultName">默认文件名(库名/模式名/连接名)</param>
        public DaoChuSql(LianJie lj, List<DaoChuBiao> items, string defaultName)
        {
            InitializeComponent();
            _all = items ?? new List<DaoChuBiao>();
            _defaultName = defaultName;
            Title = $"导出SQL [{defaultName}]";
            gbOption.Header = $"导出选项 [{lj?.DataBaseType}]";
            gbBiao.Header = $"选择要导出的表 (共 {_all.Count} 个)";
            RefreshList();
            UpdateCount();
        }

        /// <summary>
        /// 单表导出:直接导出该表,不显示表列表
        /// </summary>
        public DaoChuSql(LianJie lj, DaoChuBiao item)
        {
            InitializeComponent();
            _all = new List<DaoChuBiao> { item };
            _defaultName = item.Name;
            Title = $"导出SQL [{item.ShowName}]";
            gbOption.Header = $"导出选项 [{lj?.DataBaseType}]";
            gbBiao.Visibility = Visibility.Collapsed;
            //单表导出不需要表列表,收起该行避免留白
            gridMain.RowDefinitions[1].Height = new GridLength(0);
            Height = 180;
        }

        /// <summary>
        /// 按关键字过滤并刷新列表
        /// </summary>
        private void RefreshList()
        {
            var key = txtKey?.Text ?? "";
            lbBiao.ItemsSource = string.IsNullOrWhiteSpace(key)
                ? _all
                : _all.Where(p => (p.ShowName ?? "").ContainsAnyKeyword(key)).ToList();
        }

        private void UpdateCount()
        {
            if (tbCount == null)
            {
                return;
            }
            tbCount.Text = $"已选 {_all.Count(p => p.IsChecked)} / {_all.Count}";
        }

        private void TxtKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshList();
        }

        private void BtnQuanXuan(object sender, RoutedEventArgs e)
        {
            foreach (var item in lbBiao.Items)
            {
                if (item is DaoChuBiao b)
                {
                    b.IsChecked = true;
                }
            }
            UpdateCount();
        }

        private void BtnQuanBuXuan(object sender, RoutedEventArgs e)
        {
            foreach (var item in lbBiao.Items)
            {
                if (item is DaoChuBiao b)
                {
                    b.IsChecked = false;
                }
            }
            UpdateCount();
        }

        /// <summary>
        /// 点击行任意位置切换勾选(CheckBox 自身点击由控件处理,避免重复切换)
        /// </summary>
        private void LbBiao_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isBusy)
            {
                return;
            }
            if (FindVisualParent<CheckBox>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }
            if ((e.OriginalSource as FrameworkElement)?.DataContext is DaoChuBiao b)
            {
                b.IsChecked = !b.IsChecked;
                UpdateCount();
            }
        }

        private void BtnDaoChu(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
            {
                return;
            }
            var list = _all.Where(p => p.IsChecked).ToList();
            if (list.Count == 0)
            {
                Oops.Warning("请勾选要导出的表");
                return;
            }
            if (cbDrop.IsChecked == true && !Msg.Question("勾选了删除表语句,导入该SQL时会先删除已存在的同名表/视图,确认继续吗?"))
            {
                return;
            }
            var save = new System.Windows.Forms.SaveFileDialog
            {
                Filter = @"SQL文件(*.sql)|*.sql",
                FileName = _defaultName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss")
            };
            if (save.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            StartExport(list, save.FileName);
        }

        /// <summary>
        /// 后台导出并刷新进度
        /// </summary>
        private void StartExport(List<DaoChuBiao> list, string path)
        {
            var withData = rbShuju.IsChecked == true;
            var withDrop = cbDrop.IsChecked == true;
            _isBusy = true;
            _cancel = false;
            _lastPercent = -1;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            BtnOk.IsEnabled = false;
            BtnCancel.Visibility = Visibility.Visible;
            BtnCancel.IsEnabled = true;
            panelProgress.Visibility = Visibility.Visible;
            tbStatus.Text = "准备导出...";
            pb.Value = 0;
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var ok = SqlExport.Export(list, withData, withDrop, path, token, Report, out string error);
                    Dispatcher.Invoke(
                        new Action(() =>
                        {
                            _isBusy = false;
                            BtnOk.IsEnabled = true;
                            BtnCancel.Visibility = Visibility.Collapsed;
                            if (_cancel)
                            {
                                tbStatus.Text = "已取消导出";
                                Oops.Warning("已取消导出");
                                return;
                            }
                            if (!ok)
                            {
                                tbStatus.Text = "导出失败";
                                Msg.Warning($"导出失败:{Environment.NewLine}{error}");
                                return;
                            }
                            DialogResult = true;
                            Close();
                            Msg.Ok($"导出完成:{Environment.NewLine}{path}");
                        }));
                }));
        }

        /// <summary>
        /// 进度回调(后台线程调用)
        /// </summary>
        private void Report(int percent, string msg)
        {
            //百分比未变化时跳过,避免表很多时界面线程积压
            if (percent == _lastPercent)
            {
                return;
            }
            _lastPercent = percent;
            _ = Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    pb.Value = percent;
                    if (!string.IsNullOrEmpty(msg))
                    {
                        tbStatus.Text = msg;
                    }
                }));
        }

        private void BtnQuXiaoDaoChu(object sender, RoutedEventArgs e)
        {
            if (!_isBusy)
            {
                return;
            }
            _cancel = true;
            _cts?.Cancel();
            tbStatus.Text = "正在取消...";
            BtnCancel.IsEnabled = false;
        }

        private void BtnGuanbi(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_isBusy)
            {
                return;
            }
            if (!Msg.Question("正在导出,确认要取消并关闭吗?"))
            {
                e.Cancel = true;
                return;
            }
            _cancel = true;
            _cts?.Cancel();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !_isBusy)
            {
                Close();
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                {
                    return parent;
                }
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
