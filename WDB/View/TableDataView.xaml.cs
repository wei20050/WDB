using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WDB.Common;
using WDB.Entity;
using WDB.Window;

namespace WDB.View
{
    /// <summary>
    /// 查看数据页:单个表格 + 分页(默认100条/页) + 筛选&排序 + 筛选列
    /// 与SQL编辑器页(TabView)的区别:没有SQL编辑框,条件与排序直接作用于当前数据查询
    /// </summary>
    public partial class TableDataView : UserControl, IDataGridHost
    {
        private TableDataViewModel _model;
        private DataTableEditDataGridView _gridView;
        private readonly KuaiSuChaXunModel _kuaiSu = new KuaiSuChaXunModel();
        private bool _inited;

        /// <summary>
        /// 字段清单缓存(字段类型/注释附到结果集列上,供表头显示);表结构变化时置空重取
        /// </summary>
        private List<ZiDuan> _fieldMeta;

        public TableDataView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inited)
            {
                return;
            }
            _model = DataContext as TableDataViewModel;
            if (_model?.Lj == null || _model.B == null)
            {
                return;
            }
            _inited = true;
            //PropertyChanged 由 Fody 织入,源码里只能通过接口订阅
            ((INotifyPropertyChanged)_model).PropertyChanged += Model_PropertyChanged;
            //筛选&排序面板
            _kuaiSu.SelectedBiao = _model.B;
            BorderKuaisu.DataContext = _kuaiSu;
            LoadZiDuan();
            //表格只创建一次,翻页时只换 DataContext(列的显示/隐藏状态得以保留)
            _gridView = new DataTableEditDataGridView
            {
                DataContext = new DataTableEditDataGridViewModel { Lj = _model.Lj }
            };
            GridHost.Content = _gridView;
            Query(1);
        }

        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TableDataViewModel.PageSize) && _model.PageSize > 0)
            {
                //每页条数变更后回到第一页
                Query(1);
            }
        }

        #region 数据查询与分页

        /// <summary>
        /// 查询指定页(页码从1开始;每页多取一条用于判断是否还有下一页,不做 COUNT(*))
        /// </summary>
        private void Query(int pageIndex)
        {
            if (_model?.Lj == null || _model.B == null)
            {
                return;
            }
            if (pageIndex < 1)
            {
                pageIndex = 1;
            }
            var pageSize = _model.PageSize > 0 ? _model.PageSize : 100;
            var offset = (pageIndex - 1) * pageSize;
            string error;
            var sql = _kuaiSu.BuildPageSql(_model.Lj, offset, pageSize + 1, out error);
            if (string.IsNullOrEmpty(sql))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Msg.Warning(error);
                }
                return;
            }
            SetLoading(true);
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    //静默执行:翻页不再弹"执行成功,查到数据行数"
                    var ds = Db.GetDataTable(_model.Lj, sql, out var isRet, out _, true);
                    if (isRet && ds != null && ds.Tables.Count > 0)
                    {
                        //附上字段类型/注释,供结果表头显示(后台线程,取不到不影响数据展示)
                        AttachFieldMeta(ds.Tables[0]);
                    }
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetLoading(false);
                        if (!isRet || ds == null || ds.Tables.Count == 0)
                        {
                            return;
                        }
                        var dt = ds.Tables[0];
                        //Oracle/达梦分页会多带一个 rownum_ 辅助列
                        RemoveHelperColumn(dt);
                        var hasNext = dt.Rows.Count > pageSize;
                        if (hasNext)
                        {
                            //多取的那条只用于判断下一页,不显示
                            dt.Rows.RemoveAt(dt.Rows.Count - 1);
                        }
                        //DataTable 名称用于生成增删改SQL,必须是真实表名(分页SQL里解析出来的名字不可靠)
                        dt.TableName = _model.B.Name;
                        _model.PageIndex = pageIndex;
                        _model.HasNext = hasNext;
                        _model.CanPrev = pageIndex > 1;
                        _model.PageInfo = hasNext
                            ? $"第 {pageIndex} 页,本页 {dt.Rows.Count} 条"
                            : $"第 {pageIndex} 页,本页 {dt.Rows.Count} 条(已是最后一页)";
                        EnsureLieList(dt);
                        _gridView.DataContext = new DataTableEditDataGridViewModel { Lj = _model.Lj, Dt = dt };
                    }));
                }));
        }

        /// <summary>
        /// 移除Oracle/达梦分页SQL带出来的 rownum_ 辅助列
        /// </summary>
        private static void RemoveHelperColumn(DataTable dt)
        {
            var col = dt.Columns.Cast<DataColumn>()
                .FirstOrDefault(p => string.Equals(p.ColumnName, "ROWNUM_", StringComparison.OrdinalIgnoreCase));
            if (col != null)
            {
                dt.Columns.Remove(col);
            }
        }

        /// <summary>
        /// 给结果集列附上字段类型与注释(供结果表头显示);字段清单优先用已加载的,否则取一次并缓存
        /// </summary>
        private void AttachFieldMeta(DataTable dt)
        {
            try
            {
                var fields = _model.B.ZiDuanList;
                if (fields == null || fields.Count == 0)
                {
                    fields = _fieldMeta ?? (_fieldMeta = Db.GetZiDuans(_model.B, _model.Lj, true) ?? new List<ZiDuan>());
                }
                if (fields.Count == 0)
                {
                    return;
                }
                foreach (DataColumn col in dt.Columns)
                {
                    var f = fields.FirstOrDefault(p => string.Equals(p.Name, col.ColumnName, StringComparison.OrdinalIgnoreCase));
                    if (f == null)
                    {
                        continue;
                    }
                    col.ExtendedProperties[DataTableEditDataGrid.FieldTypeKey] = f.Type;
                    col.ExtendedProperties[DataTableEditDataGrid.FieldCommentKey] = f.Comment;
                    //主键标记:生成UPDATE/DELETE时用主键做WHERE条件(表无主键时仍按第一个字段兜底)
                    col.ExtendedProperties[DataTableEditDataGrid.FieldPrimaryKeyKey] = f.IsPK;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        private void BtnFirstPage_Click(object sender, RoutedEventArgs e)
        {
            Query(1);
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            Query(_model.PageIndex - 1);
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            Query(_model.PageIndex + 1);
        }

        private void Shuaxin_Click(object sender, RoutedEventArgs e)
        {
            Query(_model.PageIndex);
        }

        #endregion

        #region 筛选列

        /// <summary>
        /// 首次查询后按结果集列初始化筛选列,翻页时保留已有勾选状态
        /// </summary>
        private void EnsureLieList(DataTable dt)
        {
            foreach (DataColumn col in dt.Columns)
            {
                if (_model.LieList.Any(p => p.Name == col.ColumnName))
                {
                    continue;
                }
                var item = new XuanLieItem { Name = col.ColumnName, IsVisible = true };
                ((INotifyPropertyChanged)item).PropertyChanged += Lie_PropertyChanged;
                _model.LieList.Add(item);
            }
        }

        private void Lie_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(XuanLieItem.IsVisible) || !(sender is XuanLieItem item))
            {
                return;
            }
            _gridView?.SetColumnVisible(item.Name, item.IsVisible);
        }

        /// <summary>
        /// [筛选列]:点完菜单再弹出复选框面板(菜单会先关闭,延后一帧打开避免被菜单的鼠标捕获立刻关掉)
        /// </summary>
        private void MiLie_Click(object sender, RoutedEventArgs e)
        {
            PopLie.IsOpen = false;
            _ = Dispatcher.BeginInvoke(
                new Action(() => PopLie.IsOpen = true),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void BtnLieAll_Click(object sender, RoutedEventArgs e)
        {
            SetAllLieVisible(true);
        }

        private void BtnLieNone_Click(object sender, RoutedEventArgs e)
        {
            SetAllLieVisible(false);
        }

        /// <summary>
        /// 全选/取消全选(勾选变化会经 Lie_PropertyChanged 同步到表格列)
        /// </summary>
        private void SetAllLieVisible(bool visible)
        {
            foreach (var item in _model.LieList)
            {
                item.IsVisible = visible;
            }
        }

        #endregion

        #region 筛选&排序

        private void ShaiXuanPaiXu_Click(object sender, RoutedEventArgs e)
        {
            BorderKuaisu.Visibility = BorderKuaisu.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        /// <summary>
        /// [筛选&排序]:按当前条件与排序直接查询(不再生成SQL到编辑器)
        /// </summary>
        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            Query(1);
        }

        /// <summary>
        /// [查询语句]:按当前条件与排序组装成查询语句弹窗显示
        /// </summary>
        private void BtnSql_Click(object sender, RoutedEventArgs e)
        {
            var sql = _kuaiSu.BuildSql(_model.Lj, out var error);
            if (string.IsNullOrEmpty(sql))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Msg.Warning(error);
                }
                return;
            }
            _ = new SqlWindow(sql)
            {
                Owner = System.Windows.Window.GetWindow(this)
            }.ShowDialog();
        }

        private void BtnQingkong_Click(object sender, RoutedEventArgs e)
        {
            _kuaiSu.QingKong();
            Query(1);
        }

        private void BtnTiaoJianAdd_Click(object sender, RoutedEventArgs e)
        {
            _kuaiSu.AddTiaoJian();
            GuanDaoDibu();
        }

        private void BtnTiaoJianDelete_Click(object sender, RoutedEventArgs e)
        {
            _kuaiSu.RemoveTiaoJian((sender as FrameworkElement)?.Tag as TiaoJianItem);
        }

        private void BtnPaiXuAdd_Click(object sender, RoutedEventArgs e)
        {
            _kuaiSu.AddPaiXu();
            GuanDaoDibu();
        }

        private void BtnPaiXuDelete_Click(object sender, RoutedEventArgs e)
        {
            _kuaiSu.RemovePaiXu((sender as FrameworkElement)?.Tag as PaiXuItem);
        }

        /// <summary>
        /// 新增行后滚到底部(等布局更新后再滚,否则新行还没生成出来)
        /// </summary>
        private void GuanDaoDibu()
        {
            _ = SvKuaisu.Dispatcher.BeginInvoke(
                new Action(() => SvKuaisu.ScrollToBottom()),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 加载当前表字段(供条件/排序下拉选择)
        /// </summary>
        private void LoadZiDuan()
        {
            var b = _model.B;
            if (b.ZiDuanList != null && b.ZiDuanList.Count > 0)
            {
                ApplyZiDuan(b.ZiDuanList);
                return;
            }
            var lj = _model.Lj;
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var zd = Db.GetZiDuans(b, lj, true);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        if (zd == null || zd.Count == 0)
                        {
                            return;
                        }
                        b.ZiDuanList = zd;
                        ApplyZiDuan(zd);
                    }));
                }));
        }

        private void ApplyZiDuan(List<ZiDuan> list)
        {
            _kuaiSu.ZiDuanList = new ObservableCollection<ZiDuanXuanZe>(
                list.Select(p => new ZiDuanXuanZe
                {
                    Name = p.Name,
                    Comment = p.Comment,
                    Type = p.Type
                }));
            _kuaiSu.Refresh();
        }

        #endregion

        #region 供应表格右键菜单调用(IDataGridHost)

        public void SetLoading(bool visible)
        {
            LoadingG.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 表格右键[插入一个过滤条件]:条件加进筛选面板并立即查询
        /// </summary>
        public void ApplyFilter(string condition)
        {
            var item = ParseTiaoJian(condition);
            if (item == null)
            {
                Oops.Warning("无法识别的筛选条件");
                return;
            }
            _kuaiSu.AddTiaoJian(item);
            BorderKuaisu.Visibility = Visibility.Visible;
            Query(1);
        }

        /// <summary>
        /// 表格右键[插入一个排序条件]:排序加进筛选面板并立即查询
        /// </summary>
        public void ApplyOrderBy(string orderBy)
        {
            var arr = ParseOrderBy(orderBy);
            if (arr == null)
            {
                return;
            }
            var exist = _kuaiSu.PaiXuList.FirstOrDefault(p => p.Field == arr[0]);
            if (exist != null)
            {
                exist.Enable = true;
                exist.FangXiang = arr[1];
            }
            else
            {
                _kuaiSu.AddPaiXu(new PaiXuItem { Field = arr[0], FangXiang = arr[1], Enable = true });
            }
            BorderKuaisu.Visibility = Visibility.Visible;
            Query(1);
        }

        /// <summary>
        /// 解析表格右键生成的筛选条件(形如 `列`='值'、`列`LIKE'%值%'、`列`IS NULL)
        /// </summary>
        private static TiaoJianItem ParseTiaoJian(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return null;
            }
            var text = condition.Trim();
            //按长度倒序匹配,避免 >= 被 > 抢先匹配
            var ops = new[]
            {
                "IS NOT NULL", "IS NULL", "NOT LIKE", "NOT IN", "LIKE", "BETWEEN", "IN", ">=", "<=", "<>", "=", ">", "<"
            }.OrderByDescending(p => p.Length);
            foreach (var op in ops)
            {
                var index = text.IndexOf(op, StringComparison.OrdinalIgnoreCase);
                if (index <= 0)
                {
                    continue;
                }
                var field = UnEscapeName(text.Substring(0, index));
                var value = text.Substring(index + op.Length).Trim();
                if (string.IsNullOrEmpty(field))
                {
                    continue;
                }
                if (string.IsNullOrEmpty(value) && op != "IS NULL" && op != "IS NOT NULL")
                {
                    return null;
                }
                return new TiaoJianItem
                {
                    Field = field,
                    CaoZuoFu = op,
                    Value = value,
                    GuanXi = "AND",
                    Enable = true
                };
            }
            return null;
        }

        /// <summary>
        /// 解析表格右键生成的排序(形如 " ORDER BY `列` ASC")
        /// </summary>
        private static string[] ParseOrderBy(string orderBy)
        {
            if (string.IsNullOrWhiteSpace(orderBy))
            {
                return null;
            }
            var text = orderBy.Trim();
            var index = text.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
            var part = index >= 0 ? text.Substring(index + "ORDER BY".Length).Trim() : text;
            var arr = part.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (arr.Length == 0)
            {
                return null;
            }
            var field = UnEscapeName(arr[0]);
            if (string.IsNullOrEmpty(field))
            {
                return null;
            }
            var direction = arr.Length > 1 && string.Equals(arr[1], "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            return new[] { field, direction };
        }

        /// <summary>
        /// 去掉字段名的转义符(表格右键传来的列名已转义过,放进筛选面板前要还原,否则会重复转义)
        /// </summary>
        private static string UnEscapeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }
            var n = name.Trim();
            if (n.Length > 1 && ((n.StartsWith("`") && n.EndsWith("`"))
                                 || (n.StartsWith("[") && n.EndsWith("]"))
                                 || (n.StartsWith("\"") && n.EndsWith("\""))))
            {
                n = n.Substring(1, n.Length - 2);
            }
            return n;
        }

        #endregion

        #region 表结构与导出

        private void Biaojiegou_Click(object sender, RoutedEventArgs e)
        {
            //表结构改为在主窗口页签中打开(不再弹窗);结构改动后的刷新由编辑页保存成功的回调触发
            var host = System.Windows.Window.GetWindow(this) as WDB.MainWindow;
            if (host == null)
            {
                return;
            }
            var b = _model.B;
            SetLoading(true);
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    b.ZiDuanList = Db.GetZiDuans(b, _model.Lj);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetLoading(false);
                        if (b.IsView)
                        {
                            //视图不支持编辑,只查看
                            host.OpenBiaoJieGouTab(_model.Lj, b);
                            return;
                        }
                        host.OpenBiaoBianJiTab(_model.Lj, b, false, () =>
                        {
                            //本页签可能已被关闭,不再刷新
                            if (!IsLoaded)
                            {
                                return;
                            }
                            //表结构可能已变,重新加载字段与数据
                            b.ZiDuanList = Db.GetZiDuans(b, _model.Lj);
                            _fieldMeta = null;
                            _model.LieList.Clear();
                            Query(1);
                        });
                    }));
                }));
        }

        private void Daochu_Click(object sender, RoutedEventArgs e)
        {
            var b = _model.B;
            var item = new DaoChuBiao
            {
                IsChecked = true,
                Name = b.Name,
                Comment = b.Comment,
                IsView = b.IsView,
                SchemaName = _model.SchemaName,
                Biao = b,
                LianJie = _model.Lj
            };
            var f = new DaoChuSql(_model.Lj, item)
            {
                Owner = System.Windows.Window.GetWindow(this)
            };
            _ = f.ShowDialog();
        }

        #endregion
    }
}
