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
    /// MongoDB 集合数据页:单个表格 + 分页(默认100条/页) + 筛选&amp;排序 + 筛选列
    /// 与 SQL 的"查看数据"页(TableDataView)保持一致,只是条件与排序按 MongoDB 的 JSON 语义翻译;
    /// 不含"打开表结构"与"导出数据"(MongoDB 没有表结构)
    /// </summary>
    public partial class MongoTableDataView : UserControl, IDataGridHost
    {
        private MongoTableDataViewModel _model;
        private DataTableEditDataGridView _gridView;
        private readonly KuaiSuChaXunModel _kuaiSu = new KuaiSuChaXunModel();

        /// <summary>
        /// 字段名 → 样本类型(集合没有固定类型,取样本值用于条件值的字面量判断)
        /// </summary>
        private readonly Dictionary<string, string> _fieldTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private bool _inited;

        public MongoTableDataView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inited)
            {
                return;
            }
            _model = DataContext as MongoTableDataViewModel;
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
            if (e.PropertyName == nameof(MongoTableDataViewModel.PageSize) && _model.PageSize > 0)
            {
                //每页条数变更后回到第一页
                Query(1);
            }
        }

        #region 数据查询与分页

        /// <summary>
        /// 查询指定页(页码从1开始;每页多取一条用于判断是否还有下一页)
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
            //筛选&排序面板 → MongoDB 过滤/排序 JSON
            var filter = MongoFilterBuilder.Build(_kuaiSu.TiaoJianList, GetFieldType, out var error);
            if (!string.IsNullOrEmpty(error))
            {
                Msg.Warning(error);
                return;
            }
            var sort = MongoFilterBuilder.BuildSort(_kuaiSu.PaiXuList);
            var pageSize = _model.PageSize > 0 ? _model.PageSize : 100;
            var offset = (pageIndex - 1) * pageSize;
            var lj = _model.Lj;
            var collection = _model.B.Name;
            SetLoading(true);
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    //多取一条用于判断下一页
                    var dt = Db.GetMongoDataTable(lj, collection, filter, sort, null, offset, pageSize + 1, out var err);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetLoading(false);
                        try
                        {
                            if (dt == null)
                            {
                                if (!string.IsNullOrEmpty(err))
                                {
                                    Msg.Warning(err);
                                }
                                return;
                            }
                            var hasNext = dt.Rows.Count > pageSize;
                            if (hasNext)
                            {
                                //多取的那条只用于判断下一页,不显示
                                dt.Rows.RemoveAt(dt.Rows.Count - 1);
                            }
                            _model.PageIndex = pageIndex;
                            _model.HasNext = hasNext;
                            _model.CanPrev = pageIndex > 1;
                            _model.PageInfo = hasNext
                                ? $"第 {pageIndex} 页,本页 {dt.Rows.Count} 条"
                                : $"第 {pageIndex} 页,本页 {dt.Rows.Count} 条(已是最后一页)";
                            EnsureLieList(dt);
                            //注入 MongoDB 编辑器,表格内的新增/修改/删除按文档回写
                            _gridView.DataContext = new DataTableEditDataGridViewModel
                            {
                                Lj = lj,
                                Dt = dt,
                                MongoEditor = new MongoDataGridEditor { Lj = lj, Collection = collection }
                            };
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"展示MongoDB数据失败:{ex}");
                            Msg.Warning(ex.Message);
                        }
                    }));
                }));
        }

        /// <summary>
        /// 取字段的样本类型(供条件值的字面量判断)
        /// </summary>
        private string GetFieldType(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return null;
            }
            return _fieldTypes.TryGetValue(field, out var type) ? type : null;
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
            LoadZiDuan();
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
        /// [筛选&排序]:按当前条件与排序直接查询
        /// </summary>
        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            Query(1);
        }

        /// <summary>
        /// [查询语句]:按当前条件与排序组装成 MongoDB 查询命令弹窗显示
        /// </summary>
        private void BtnSql_Click(object sender, RoutedEventArgs e)
        {
            var filter = MongoFilterBuilder.Build(_kuaiSu.TiaoJianList, GetFieldType, out var error);
            if (!string.IsNullOrEmpty(error))
            {
                Msg.Warning(error);
                return;
            }
            var sort = MongoFilterBuilder.BuildSort(_kuaiSu.PaiXuList);
            var pageSize = _model.PageSize > 0 ? _model.PageSize : 100;
            var cmd = "db." + _model.B.Name + ".find(" + (filter ?? "{}") + ")";
            if (!string.IsNullOrEmpty(sort))
            {
                cmd += ".sort(" + sort + ")";
            }
            cmd += ".limit(" + pageSize + ");";
            _ = new SqlWindow(cmd)
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
        /// 取样集合文档,加载字段(供条件/排序下拉选择)
        /// </summary>
        private void LoadZiDuan()
        {
            var lj = _model.Lj;
            var collection = _model.B.Name;
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var list = Db.GetMongoCollectionZiDuans(lj, collection);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        if (list == null || list.Count == 0)
                        {
                            return;
                        }
                        foreach (var zd in list)
                        {
                            _fieldTypes[zd.Name] = zd.Type;
                        }
                        //更换字段下拉的数据源会让 ComboBox 先清空选中再回写绑定,导致条件/排序行的字段被置空,
                        //这里先记住各行已选的字段,更新数据源后再按顺序恢复
                        var tiaoJianFields = _kuaiSu.TiaoJianList.Select(p => p.Field).ToList();
                        var paiXuFields = _kuaiSu.PaiXuList.Select(p => p.Field).ToList();

                        _kuaiSu.ZiDuanList = new ObservableCollection<ZiDuanXuanZe>(
                            list.Select(p => new ZiDuanXuanZe
                            {
                                Name = p.Name,
                                Comment = p.Comment,
                                Type = p.Type
                            }));

                        for (var i = 0; i < _kuaiSu.TiaoJianList.Count && i < tiaoJianFields.Count; i++)
                        {
                            _kuaiSu.TiaoJianList[i].Field = tiaoJianFields[i];
                        }
                        for (var i = 0; i < _kuaiSu.PaiXuList.Count && i < paiXuFields.Count; i++)
                        {
                            _kuaiSu.PaiXuList[i].Field = paiXuFields[i];
                        }
                        _kuaiSu.Refresh();
                    }));
                }));
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
        /// 解析表格右键生成的筛选条件(形如 列='值'、列LIKE'%值%'、列IS NULL)
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
        /// 解析表格右键生成的排序(形如 " ORDER BY 列 ASC")
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
        /// 去掉字段名的转义符(表格右键传来的列名可能带转义)
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
    }
}
