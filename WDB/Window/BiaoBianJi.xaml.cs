using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    /// <summary>
    /// 可视化表结构编辑(新建表 / 修改表结构)
    /// </summary>
    public partial class BiaoBianJi
    {
        /// <summary>
        /// 保存成功后触发(宿主据此刷新表数据/树节点)
        /// </summary>
        public event Action Saved;

        /// <summary>
        /// 请求关闭所在页签(菜单"取消")
        /// </summary>
        public event Action CloseRequested;

        private readonly BiaoBianJiModel _m;
        private readonly LianJie _lj;

        /// <summary>
        /// 是否新建表(新建保存成功后转为修改模式)
        /// </summary>
        private bool _isNew;

        /// <summary>
        /// 结构基准快照(保存成功后刷新,页签内继续修改时按最新结构生成SQL)
        /// </summary>
        private List<ZiDuan> _oldList;

        private string _oldComment;

        public BiaoBianJi(LianJie lj, Biao b, bool isNew)
        {
            InitializeComponent();
            _m = (BiaoBianJiModel)DataContext;
            _lj = lj;
            _isNew = isNew;
            _m.IsNew = isNew;
            _m.LianjieMing = $"{lj.Name} ( host: {lj.Host} database: {lj.DataBaseName} schema: {lj.ServiceName} username: {lj.UserName} )";
            _m.TypeList = Db.GetCommonTypes(lj.DataBaseType);
            if (isNew)
            {
                _m.BiaoMing = string.Empty;
                _m.BiaoZhuShi = string.Empty;
                SetZiDuanList(new List<ZiDuan> { NewZiDuan() });
                _oldList = new List<ZiDuan>();
                _oldComment = string.Empty;
            }
            else
            {
                _m.BiaoMing = b.Name;
                _m.BiaoZhuShi = b.Comment;
                var list = b.ZiDuanList ?? Db.GetZiDuans(b, lj);
                foreach (var z in list)
                {
                    //记录原始名,用于识别改名
                    z.OldName = z.Name;
                }
                SetZiDuanList(list);
                _oldList = CoreConvert.DeepClone(list);
                _oldComment = b.Comment;
            }
            txtBiaoMing.IsReadOnly = !isNew;
        }

        private ZiDuan NewZiDuan()
        {
            return new ZiDuan
            {
                Name = "id",
                Type = _m.TypeList != null && _m.TypeList.Count > 0 ? _m.TypeList[0] : "int",
                Notnull = true,
                IsPK = true
            };
        }

        /// <summary>
        /// 设置字段列表(统一挂上主键联动)
        /// </summary>
        private void SetZiDuanList(List<ZiDuan> list)
        {
            //ZiDuan 的 INotifyPropertyChanged 由 Fody 编织生成,源码里只能通过接口订阅
            if (_m.ZiDuanList != null)
            {
                foreach (var z in _m.ZiDuanList)
                {
                    if (z is INotifyPropertyChanged old)
                    {
                        old.PropertyChanged -= ZiDuan_PropertyChanged;
                    }
                }
            }
            if (list != null)
            {
                foreach (var z in list)
                {
                    if (z is INotifyPropertyChanged now)
                    {
                        now.PropertyChanged += ZiDuan_PropertyChanged;
                    }
                }
            }
            _m.ZiDuanList = list;
        }

        /// <summary>
        /// 主键与非空联动:勾主键自动勾非空,取消非空自动取消主键
        /// </summary>
        private static void ZiDuan_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is ZiDuan z))
            {
                return;
            }
            if (e.PropertyName == nameof(ZiDuan.IsPK) && z.IsPK)
            {
                z.Notnull = true;
            }
            else if (e.PropertyName == nameof(ZiDuan.Notnull) && !z.Notnull && z.IsPK)
            {
                z.IsPK = false;
            }
        }

        private void BtnTianJia(object sender, RoutedEventArgs e)
        {
            var list = _m.ZiDuanList == null ? new List<ZiDuan>() : new List<ZiDuan>(_m.ZiDuanList);
            list.Add(new ZiDuan
            {
                Name = GetNewColumnName(list),
                Type = _m.TypeList != null && _m.TypeList.Count > 0 ? _m.TypeList[0] : "varchar(50)"
            });
            SetZiDuanList(list);
        }

        /// <summary>
        /// 生成不重复的新字段名(new_col、new_col1...)
        /// </summary>
        private static string GetNewColumnName(List<ZiDuan> list)
        {
            var name = "new_col";
            var i = 1;
            while (list.Any(p => p.Name == name))
            {
                name = "new_col" + i++;
            }
            return name;
        }

        /// <summary>
        /// 复制选中字段(插入到其后)
        /// </summary>
        private void BtnFuzhi(object sender, RoutedEventArgs e)
        {
            if (!(dg.SelectedItem is ZiDuan z))
            {
                Msg.Warning("请先选中要复制的字段");
                return;
            }
            var list = _m.ZiDuanList == null ? new List<ZiDuan>() : new List<ZiDuan>(_m.ZiDuanList);
            var index = list.IndexOf(z);
            var copy = new ZiDuan
            {
                //复制出来的字段视为新增(无原始名),且不继承主键
                Name = GetNewZiDuanName(list, z.Name),
                Type = z.Type,
                Notnull = z.Notnull,
                DefaultValue = z.DefaultValue,
                Comment = z.Comment
            };
            if (index < 0)
            {
                list.Add(copy);
            }
            else
            {
                list.Insert(index + 1, copy);
            }
            SetZiDuanList(list);
            dg.SelectedItem = copy;
        }

        /// <summary>
        /// 删除选中字段(支持多选)
        /// </summary>
        private void BtnShanchu(object sender, RoutedEventArgs e)
        {
            var items = new List<ZiDuan>();
            foreach (var item in dg.SelectedItems)
            {
                if (item is ZiDuan z)
                {
                    items.Add(z);
                }
            }
            if (items.Count == 0)
            {
                Msg.Warning("请先选中要删除的字段");
                return;
            }
            if (_m.ZiDuanList == null)
            {
                return;
            }
            var list = new List<ZiDuan>(_m.ZiDuanList);
            if (list.Count - items.Count < 1)
            {
                Msg.Warning("至少需要保留一个字段");
                return;
            }
            foreach (var z in items)
            {
                list.Remove(z);
            }
            SetZiDuanList(list);
        }

        /// <summary>
        /// 生成一个不重复的字段名
        /// </summary>
        private static string GetNewZiDuanName(List<ZiDuan> list, string name)
        {
            var newName = name + "_copy";
            var i = 1;
            while (list.Any(p => p.Name == newName))
            {
                newName = $"{name}_copy{i++}";
            }
            return newName;
        }

        /// <summary>
        /// 校验输入
        /// </summary>
        private bool Check()
        {
            if (string.IsNullOrWhiteSpace(txtBiaoMing.Text))
            {
                Msg.Warning("请输入表名");
                return false;
            }
            var cols = _m.ZiDuanList ?? new List<ZiDuan>();
            if (cols.Count == 0)
            {
                Msg.Warning("请至少添加一个字段");
                return false;
            }
            foreach (var z in cols)
            {
                if (string.IsNullOrWhiteSpace(z.Name))
                {
                    Msg.Warning("字段名不能为空");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(z.Type))
                {
                    Msg.Warning($"字段[{z.Name}]的类型不能为空");
                    return false;
                }
            }
            var repeat = cols.GroupBy(p => p.Name).FirstOrDefault(g => g.Count() > 1);
            if (repeat != null)
            {
                Msg.Warning($"字段名[{repeat.Key}]重复");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 生成SQL(必须在UI线程调用)
        /// </summary>
        private List<SqlItem> BuildSql()
        {
            var name = txtBiaoMing.Text.Trim();
            var cols = _m.ZiDuanList ?? new List<ZiDuan>();
            return _isNew
                ? Db.GetCreateTableSqlList(_lj, name, _m.BiaoZhuShi, cols)
                : Db.GetAlterTableSqlList(_lj, name, _oldList, cols, _oldComment, _m.BiaoZhuShi);
        }

        private void BtnYulan(object sender, RoutedEventArgs e)
        {
            if (!Check())
            {
                return;
            }
            var sqlList = BuildSql();
            if (sqlList == null || sqlList.Count == 0)
            {
                Msg.Warning("没有需要执行的语句");
                return;
            }
            var text = string.Join(Environment.NewLine + Environment.NewLine, sqlList.Select(p => p.Sql));
            _ = new SqlWindow(text.SqlFormat())
            {
                Owner = System.Windows.Window.GetWindow(this)
            }.ShowDialog();
        }

        /// <summary>
        /// Ctrl+S 保存
        /// </summary>
        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnBaocun(this, null);
                e.Handled = true;
            }
        }

        private void BtnBaocun(object sender, RoutedEventArgs e)
        {
            //保存中不重复提交(防 Ctrl+S 连按)
            if (LoadingG.Visibility == Visibility.Visible)
            {
                return;
            }
            if (!Check())
            {
                return;
            }
            var sqlList = BuildSql();
            if (sqlList == null || sqlList.Count == 0)
            {
                Msg.Warning("没有需要保存的修改");
                return;
            }
            var name = txtBiaoMing.Text.Trim();
            if (_isNew)
            {
                if (!Msg.Question($"确认创建表[{name}]？"))
                {
                    return;
                }
            }
            else
            {
                if (!Msg.Question($"确认保存对表[{name}]的结构修改？"))
                {
                    return;
                }
            }
            LoadingG.Visibility = Visibility.Visible;
            _ = Task.Factory.StartNew(
                new System.Action(() =>
                {
                    var ok = Db.ExecuteSqlList(_lj, sqlList, out string error);
                    Dispatcher.Invoke(new System.Action(() =>
                    {
                        LoadingG.Visibility = Visibility.Collapsed;
                        if (ok)
                        {
                            Oops.Ok(_isNew ? $"表[{name}]创建成功" : $"表[{name}]结构修改成功");
                            //把当前结构记为新的基准,否则二次保存仍按进入时的结构生成SQL
                            RefreshBaseline();
                            //页签模式下不再关闭自己,由宿主决定是否刷新/关页签
                            Saved?.Invoke();
                        }
                        else
                        {
                            Msg.Warning(error);
                        }
                    }));
                }));
        }

        /// <summary>
        /// 保存成功后刷新结构基准快照(类型/默认值/注释等对比基准),并把新建表转为修改模式
        /// </summary>
        private void RefreshBaseline()
        {
            var cols = _m.ZiDuanList ?? new List<ZiDuan>();
            foreach (var z in cols)
            {
                //当前字段名记为原始名,避免二次保存重复新增/改名
                z.OldName = z.Name;
            }
            _oldList = CoreConvert.DeepClone(cols);
            _oldComment = _m.BiaoZhuShi;
            if (_isNew)
            {
                //表已创建,后续保存按"修改已有表"处理
                _isNew = false;
                _m.IsNew = false;
                txtBiaoMing.IsReadOnly = true;
            }
        }

        private void BtnGuanbi(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke();
        }
    }
}
