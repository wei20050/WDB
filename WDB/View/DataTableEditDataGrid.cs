using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WDB.Entity;

namespace WDB.View
{
    public class DataTableEditDataGrid : DataGrid
    {
        public const string Null = "[ NULL ]";

        /// <summary>
        /// 结果集列上附加的字段类型键(数据页写入 DataColumn.ExtendedProperties,供结果表头显示)
        /// </summary>
        public const string FieldTypeKey = "FieldType";

        /// <summary>
        /// 结果集列上附加的字段注释键(数据页写入 DataColumn.ExtendedProperties,供结果表头显示)
        /// </summary>
        public const string FieldCommentKey = "FieldComment";

        /// <summary>
        /// 结果集列上附加的主键标记键(数据页写入 DataColumn.ExtendedProperties,生成SQL时用于确定WHERE条件)
        /// </summary>
        public const string FieldPrimaryKeyKey = "FieldPrimaryKey";

        /// <summary>
        /// 判断结果集列是否主键:优先用数据页写入的主键标记,取不到时退回 DataTable 自身的主键
        /// </summary>
        public static bool IsPrimaryKey(DataTable dt, DataColumn col)
        {
            if (col.ExtendedProperties[FieldPrimaryKeyKey] is bool isPk)
            {
                return isPk;
            }
            return dt.PrimaryKey.Contains(col);
        }

        /// <summary>
        /// 把字段清单里的类型/注释/主键附到结果集列上(按列名忽略大小写匹配),供结果表头显示;
        /// 匹配不到的列(表达式/别名列)不写,表头对应的类型/注释行自动折叠
        /// </summary>
        public static void AttachFieldMeta(DataTable dt, IEnumerable<ZiDuan> fields)
        {
            if (dt == null || fields == null)
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
                col.ExtendedProperties[FieldTypeKey] = f.Type;
                col.ExtendedProperties[FieldCommentKey] = f.Comment;
                //主键标记:生成UPDATE/DELETE时用主键做WHERE条件
                col.ExtendedProperties[FieldPrimaryKeyKey] = f.IsPK;
            }
        }

        /// <summary>
        /// 结果表头内容:字段名(含主键标记) + 类型 + 注释(纵向排列显示)
        /// ToString 返回字段名,兼容按列名取值的地方(复制列名/生成条件等)
        /// </summary>
        public class HeaderText
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Comment { get; set; }

            /// <summary>
            /// 主键标记(主键列显示PK,其它列为空不显示)
            /// </summary>
            public string PK { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        public class ModifiedCell
        {
            public object OldValue { get; set; }
            public object NewValue { get; set; }
        }

        public class EditChangeResult
        {
            public List<DataRow> AddedRows { get; set; }
            public List<DataRow> DeletedRows { get; set; }
            public Dictionary<DataRow, HashSet<string>> ModifiedCellsInfo { get; set; }

            public EditChangeResult()
            {
                AddedRows = new List<DataRow>();
                DeletedRows = new List<DataRow>();
                ModifiedCellsInfo = new Dictionary<DataRow, HashSet<string>>();
            }
        }

        #region 私有字段
        private object _lastEditOldValue;
        private readonly List<DataRow> _addedRows = new List<DataRow>();
        private readonly List<DataRow> _toDeleteRows = new List<DataRow>();
        private readonly Dictionary<DataRow, Dictionary<string, ModifiedCell>> _modifiedCells = new Dictionary<DataRow, Dictionary<string, ModifiedCell>>();
        private DataGridRow _lastSelectedRowForShift;

        //样式缓存:滚动/选中/编辑这些热路径复用同一实例,避免每次 new Style + new SolidColorBrush + 字符串查资源
        private Style _cellBaseStyle;
        private Style _cellNullStyle;
        private Style _cellModifiedStyle;
        private Style _cellModifiedNullStyle;
        private Style _deletedRowStyle;
        private Style _addedRowStyle;
        #endregion

        public DataTableEditDataGrid()
        {
            AutoGenerateColumns = true;
            CanUserAddRows = false;
            ColumnWidth = new DataGridLength(1, DataGridLengthUnitType.Star);
            SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;

            BeginningEdit += OnBeginningEdit;
            CellEditEnding += OnCellEditEnding;
            Sorting += OnDataGridSorted;

            //这个属性固定可以选择单元格否则无法编辑
            SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;
            //开启序号列
            HandyControl.Controls.DataGridAttach.SetShowRowNumber(this, true);
            //这个宽度必须设置否则无法显示序号样式
            RowHeaderWidth = 60;

            //样式定义
            Style = FindResource("DataGridBaseStyle") as Style;

            //防止列头丢失下划线
            var baseHeaderStyle = FindResource("DataGridColumnHeaderStyle") as Style;
            Style headerStyle = new Style(typeof(DataGridColumnHeader), baseHeaderStyle);
            //列头高度改为随内容撑开:基础样式固定高度会让"三行内容+边距"被裁掉(表现为底部内容被遮);
            //MinHeight 保底,单行列头高度和原来保持一致
            headerStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, double.NaN));
            headerStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 30.0));
            //列头纵向排列:字段名(默认色,主键跟PK标记) / 类型(淡蓝) / 注释(淡灰,超长省略),完整内容通过悬停提示查看
            var template = new DataTemplate();
            var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
            panelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            panelFactory.SetValue(FrameworkElement.MaxWidthProperty, 220.0);
            //底部留一点边距,避免列头内容贴着列头底线(表头已改为随内容撑开,不会被裁掉)
            panelFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 5));

            //字段名行:字段名 + 主键标记(横向排列,非主键列的标记自动折叠)
            var nameRowFactory = new FrameworkElementFactory(typeof(StackPanel));
            nameRowFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(HeaderText.Name)));
            nameFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            nameFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
            //横向排列时字段名要限宽,否则省略号不生效会把列头顶宽
            nameFactory.SetValue(FrameworkElement.MaxWidthProperty, 180.0);

            var pkFactory = new FrameworkElementFactory(typeof(TextBlock));
            pkFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(HeaderText.PK)));
            pkFactory.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(HeaderText.PK)) { Converter = new EmptyToCollapsedConverter() });
            pkFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xd4, 0x88, 0x06)));
            pkFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            pkFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            pkFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 1, 0, 0));

            nameRowFactory.AppendChild(nameFactory);
            nameRowFactory.AppendChild(pkFactory);

            var typeFactory = new FrameworkElementFactory(typeof(TextBlock));
            typeFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(HeaderText.Type)));
            typeFactory.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(HeaderText.Type)) { Converter = new EmptyToCollapsedConverter() });
            typeFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xeb)));
            typeFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            typeFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            typeFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);

            var commentFactory = new FrameworkElementFactory(typeof(TextBlock));
            commentFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(HeaderText.Comment)));
            commentFactory.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(HeaderText.Comment)) { Converter = new EmptyToCollapsedConverter() });
            commentFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x73, 0x73, 0x73)));
            commentFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            commentFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            commentFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);

            panelFactory.AppendChild(nameRowFactory);
            panelFactory.AppendChild(typeFactory);
            panelFactory.AppendChild(commentFactory);
            template.VisualTree = panelFactory;
            headerStyle.Setters.Add(new Setter(ContentControl.ContentTemplateProperty, template));
            ColumnHeaderStyle = headerStyle;
            //表头行高随内容自适应(默认就是NaN,这里显式声明,防止基础样式固定行高导致列头内容被裁)
            ColumnHeaderHeight = double.NaN;

            AutoGeneratingColumn += DataTableEditDataGrid_AutoGeneratingColumn;

            // 监听预编辑事件，自动给 TextBox 赋值 Tag
            PreparingCellForEdit += DataTableEditDataGrid_PreparingCellForEdit;

            //行容器生成时只处理这一行(滚动时虚拟化会不断生成行,处理量随可见行数走);
            //原来在 ItemContainerGenerator.StatusChanged 里做全表刷新:数据量大时每滚动一次都全表遍历+强制布局,是滚动卡的主因
            LoadingRow += OnLoadingRow;
        }

        /// <summary>
        /// 行容器生成:先刷行底色,再等这一帧布局完成后补一次本行单元格样式(此时单元格才生成)
        /// </summary>
        private void OnLoadingRow(object sender, DataGridRowEventArgs e)
        {
            DataGridRow row = e.Row;
            if (row == null)
            {
                return;
            }
            ApplyRowStyle(row);
            //单元格要等这一帧布局完成才生成;行底色也跟着补一次(行容器准备过程可能晚于本事件覆盖样式)
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate ()
            {
                ApplyRowStyle(row);
                ApplyRowCellsStyle(row);
            });
        }

        // 自动设置 TextBox.Tag = Null
        private void DataTableEditDataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.Row.Item is DataRowView drv && e.Column is DataGridTextColumn col)
            {
                var row = drv.Row;
                var field = col.SortMemberPath;
                if (string.IsNullOrEmpty(field)) return;

                var value = row[field];
                if (e.EditingElement is TextBox tb)
                {
                    tb.Tag = value == DBNull.Value ? Null : string.Empty;

                    // 如果是 NULL，编辑框自动清空
                    if (value == DBNull.Value)
                        tb.Text = string.Empty;
                }
            }
        }

        private void DataTableEditDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var binding = new Binding(e.PropertyName)
            {
                Converter = new DBNullToNullTextConverter()
            };

            if (e.PropertyType.FullName.Contains(nameof(DateTime)))
            {
                binding.Converter = new UniversalDateTimeConverter();
            }

            // 单元格样式：只有 Null 变灰
            var textStyle = new Style(typeof(TextBlock));
            //var nullStyleTrigger = new DataTrigger
            //{
            //    Binding = new Binding(e.PropertyName)
            //    {
            //        Converter = new DBNullToNullTextConverter()
            //    },
            //    Value = Null
            //};
            //nullStyleTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.DarkGray));
            //nullStyleTrigger.Setters.Add(new Setter(TextBlock.FontStyleProperty, FontStyles.Italic));
            //textStyle.Triggers.Add(nullStyleTrigger);

            textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));

            // ToolTip
            var toolTipBinding = new Binding(e.PropertyName);
            if (e.PropertyType.FullName.Contains(nameof(DateTime)))
                toolTipBinding.Converter = new UniversalDateTimeConverter();
            textStyle.Setters.Add(new Setter(ToolTipProperty, toolTipBinding));

            // 列头:字段名(含主键标记)/类型/注释(纵向排列),完整信息通过鼠标悬停提示查看
            // 类型/注释/主键来自数据页写入的字段元数据,取不到时该行自动折叠(其它页面列头维持原样)
            var fieldName = e.PropertyName;
            var typeName = GetColumnMeta(e.PropertyName, FieldTypeKey);
            var comment = GetColumnMeta(e.PropertyName, FieldCommentKey);
            var isPk = GetColumnPrimaryKey(e.PropertyName);

            var column = new DataGridTextColumn
            {
                Binding = binding,
                Header = new HeaderText { Name = fieldName, Type = typeName, Comment = comment, PK = isPk ? "PK" : null },
                SortMemberPath = e.PropertyName,
                Width = DataGridLength.Auto,
                MaxWidth = 260,
                MinWidth = 100,
                ElementStyle = textStyle
            };
            var headerTip = BuildHeaderTip(fieldName, typeName, comment, isPk);
            if (!string.IsNullOrEmpty(headerTip))
            {
                var columnHeaderStyle = new Style(typeof(DataGridColumnHeader), ColumnHeaderStyle);
                columnHeaderStyle.Setters.Add(new Setter(ToolTipProperty, headerTip));
                column.HeaderStyle = columnHeaderStyle;
            }
            e.Column = column;
        }

        /// <summary>
        /// 读取结果集列上附加的字段信息(类型/注释),没有时返回 null
        /// </summary>
        private string GetColumnMeta(string propertyName, string key)
        {
            var dt = GetItemsView()?.Table;
            if (dt == null || string.IsNullOrEmpty(propertyName) || !dt.Columns.Contains(propertyName))
            {
                return null;
            }
            return dt.Columns[propertyName].ExtendedProperties[key] as string;
        }

        /// <summary>
        /// 判断结果集列是否主键(优先用数据页写入的主键标记,取不到时退回 DataTable 自身的主键)
        /// </summary>
        private bool GetColumnPrimaryKey(string propertyName)
        {
            var dt = GetItemsView()?.Table;
            if (dt == null || string.IsNullOrEmpty(propertyName) || !dt.Columns.Contains(propertyName))
            {
                return false;
            }
            return IsPrimaryKey(dt, dt.Columns[propertyName]);
        }

        /// <summary>
        /// 列头悬停提示:逐行列出字段名/类型/注释/主键
        /// </summary>
        private static string BuildHeaderTip(string name, string type, string comment, bool isPk)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(name)) lines.Add("字段：" + name);
            if (!string.IsNullOrWhiteSpace(type)) lines.Add("类型：" + type);
            if (!string.IsNullOrWhiteSpace(comment)) lines.Add("注释：" + comment);
            if (isPk) lines.Add("主键：是");
            return string.Join(Environment.NewLine, lines);
        }

        public void AddNewRow()
        {
            var dv = GetItemsView();
            if (dv?.Table == null)
                return;

            DataTable dt = dv.Table;
            DataRow newRow = dt.NewRow();
            int insertIndex = dt.Rows.Count;

            DataRowView drv = GetSelectedRowView();
            if (drv != null)
            {
                insertIndex = dt.Rows.IndexOf(drv.Row);
                insertIndex++;
            }

            dt.Rows.InsertAt(newRow, insertIndex);
            _addedRows.Add(newRow);
            RefreshAllStyles();
        }

        public void CopySelectedRow()
        {
            DataRowView drv = GetSelectedRowView();
            if (drv == null || drv.Row == null)
                return;

            DataTable dt = drv.Row.Table;
            DataRow newRow = dt.NewRow();
            foreach (DataColumn c in dt.Columns)
                newRow[c] = drv.Row[c];

            int insertIndex = dt.Rows.IndexOf(drv.Row) + 1;
            dt.Rows.InsertAt(newRow, insertIndex);
            _addedRows.Add(newRow);
            RefreshAllStyles();
        }

        public void DeleteSelectedRows()
        {
            HashSet<DataRow> selectedRows = new HashSet<DataRow>();
            foreach (var cell in SelectedCells)
            {
                if (cell.Item is DataRowView drv)
                    selectedRows.Add(drv.Row);
            }

            foreach (var row in selectedRows)
            {
                if (_addedRows.Contains(row))
                {
                    row.Table.Rows.Remove(row);
                    _addedRows.Remove(row);
                }
                else
                {
                    if (_toDeleteRows.Contains(row))
                    {
                        _toDeleteRows.Remove(row);
                    }
                    else
                    {
                        foreach (KeyValuePair<DataRow, Dictionary<string, ModifiedCell>> rowKv in _modifiedCells)
                        {
                            foreach (KeyValuePair<string, ModifiedCell> cellKv in rowKv.Value)
                            {
                                try
                                {
                                    if (rowKv.Key == row)
                                    {
                                        rowKv.Key[cellKv.Key] = cellKv.Value.OldValue ?? DBNull.Value;
                                    }
                                }
                                catch { }
                            }
                        }
                        for (int i = _modifiedCells.Keys.Count - 1; i >= 0; i--)
                        {
                            var key = _modifiedCells.Keys.ToList()[i];
                            if (key == row)
                            {
                                _modifiedCells.Remove(key);
                            }
                        }
                        _toDeleteRows.Add(row);
                    }
                }
            }

            SelectedItems.Clear();
            SelectedCells.Clear();
            RefreshAllStyles();
        }

        public EditChangeResult GetChanges()
        {
            EditChangeResult result = new EditChangeResult();
            foreach (KeyValuePair<DataRow, Dictionary<string, ModifiedCell>> kp in _modifiedCells)
            {
                HashSet<string> fields = new HashSet<string>(kp.Value.Keys);
                result.ModifiedCellsInfo[kp.Key] = fields;
            }

            result.AddedRows = new List<DataRow>(_addedRows);
            result.DeletedRows = _toDeleteRows.ToList();
            return result;
        }

        public void RemoveDeletedRowsFromView()
        {
            var dv = GetItemsView();
            if (dv?.Table == null)
                return;

            List<DataRow> tempList = new List<DataRow>(_toDeleteRows);
            foreach (DataRow row in tempList)
            {
                if (row.Table == dv.Table)
                {
                    dv.Table.Rows.Remove(row);
                    _toDeleteRows.Remove(row);
                }
            }
            RefreshAllStyles();
        }

        public void ClearEditState()
        {
            _modifiedCells.Clear();
            _addedRows.Clear();
            _toDeleteRows.Clear();
            RefreshAllStyles();
        }

        public void Cancel()
        {
            foreach (KeyValuePair<DataRow, Dictionary<string, ModifiedCell>> rowKv in _modifiedCells)
            {
                foreach (KeyValuePair<string, ModifiedCell> cellKv in rowKv.Value)
                {
                    try
                    {
                        rowKv.Key[cellKv.Key] = cellKv.Value.OldValue ?? DBNull.Value;
                    }
                    catch { }
                }
            }

            var dv = GetItemsView();
            if (dv?.Table != null)
            {
                List<DataRow> tempAdd = new List<DataRow>(_addedRows);
                foreach (DataRow r in tempAdd)
                {
                    if (dv.Table.Rows.IndexOf(r) >= 0)
                        dv.Table.Rows.Remove(r);
                }
            }

            ClearEditState();
        }

        /// <summary>
        /// 取当前绑定的 DataView(ItemsSource 可能是 DataView,也可能直接是 DataTable)
        /// </summary>
        private DataView GetItemsView()
        {
            if (ItemsSource is DataView dv)
            {
                return dv;
            }
            return (ItemsSource as DataTable)?.DefaultView;
        }

        public void RefreshAllStyles()
        {
            RefreshRowColors();
            RefreshAllCells();
        }

        public void RefreshRowColors()
        {
            foreach (DataGridRow row in GetRealizedRows())
            {
                ApplyRowStyle(row);
            }
        }

        public void RefreshAllCells()
        {
            foreach (DataGridRow row in GetRealizedRows())
            {
                ApplyRowCellsStyle(row);
            }
        }

        /// <summary>
        /// 应用行底色(新增绿色/待删除红色),不碰单元格内容
        /// </summary>
        private void ApplyRowStyle(DataGridRow row)
        {
            if (row == null || !(row.Item is DataRowView drv))
            {
                return;
            }
            DataRow dr = drv.Row;
            Style target;
            if (_toDeleteRows.Contains(dr))
            {
                target = GetDeletedRowStyle();
            }
            else if (_addedRows.Contains(dr))
            {
                target = GetAddedRowStyle();
            }
            else
            {
                target = RowStyle;
            }
            //同一样式重复赋值会白跑一遍样式应用,这里只在真正变化时才写
            if (!ReferenceEquals(row.Style, target))
            {
                row.Style = target;
            }
        }

        /// <summary>
        /// 应用一行的单元格样式(修改底色/NULL 灰斜体)
        /// </summary>
        private void ApplyRowCellsStyle(DataGridRow row)
        {
            if (row == null || !(row.Item is DataRowView))
            {
                return;
            }
            foreach (DataGridColumn col in Columns)
            {
                FrameworkElement cellContent = col.GetCellContent(row);
                DataGridCell cell = cellContent?.Parent as DataGridCell;
                if (cell != null)
                {
                    UpdateCellStyle(cell);
                }
            }
        }

        /// <summary>
        /// 取当前已经生成出来的行容器:虚拟化下只有可见行,数量只与窗口高度有关,与总行数无关
        /// </summary>
        private IEnumerable<DataGridRow> GetRealizedRows()
        {
            DataGridRowsPresenter presenter = FindVisualChild<DataGridRowsPresenter>(this);
            if (presenter != null)
            {
                foreach (UIElement child in presenter.Children)
                {
                    if (child is DataGridRow row)
                    {
                        yield return row;
                    }
                }
                yield break;
            }
            //兜底:取不到行面板时按索引取已生成的容器
            for (int i = 0; i < Items.Count; i++)
            {
                if (ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
                {
                    yield return row;
                }
            }
        }

        private static T FindVisualChild<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                return null;
            }
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed)
                {
                    return typed;
                }
                T found = FindVisualChild<T>(child);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        public object GetOldValue(DataRow row, string columnName)
        {
            Dictionary<string, ModifiedCell> cols;
            if (_modifiedCells.TryGetValue(row, out cols))
            {
                ModifiedCell cell;
                if (cols.TryGetValue(columnName, out cell))
                    return cell.OldValue;
            }
            return row[columnName];
        }

        private void OnBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            try
            {
                if (e.Row.Item is DataRowView drv && e.Column != null)
                {
                    string field = e.Column.SortMemberPath;
                    if (!string.IsNullOrEmpty(field))
                    {
                        _lastEditOldValue = GetOldValue(drv.Row, field);
                    }
                }
            }
            catch { }
        }

        private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                if (e.Cancel || !(e.Row.Item is DataRowView drv) || e.Column == null)
                    return;

                string field = e.Column.SortMemberPath;
                if (string.IsNullOrEmpty(field))
                    return;

                DataRow row = drv.Row;
                object oldValue = _lastEditOldValue;
                object newValue = DBNull.Value;

                if (e.EditingElement is TextBox tb)
                {
                    bool isNullCell = tb.Tag?.ToString() == Null;
                    string inputText = tb.Text;

                    if (string.IsNullOrEmpty(inputText) && isNullCell)
                    {
                        newValue = DBNull.Value;
                    }
                    else
                    {
                        Type colType = row.Table.Columns[field].DataType;
                        if (colType.FullName.Contains(nameof(DateTime)))
                        {
                            if (DateTime.TryParse(inputText, out DateTime dt))
                            {
                                try
                                {
                                    newValue = Activator.CreateInstance(colType, dt.Ticks);
                                }
                                catch
                                {
                                    newValue = Activator.CreateInstance(colType, dt);
                                }
                            }
                            else
                            {
                                newValue = oldValue;
                            }
                        }
                        else
                        {
                            newValue = inputText;
                        }
                    }
                }

                if (_toDeleteRows.Contains(row))
                {
                    row[field] = oldValue;
                    return;
                }

                row[field] = newValue;

                if (_addedRows.Contains(row))
                    return;

                bool isModified = !ValuesAreEqual(oldValue, newValue);
                if (!isModified)
                    return;

                if (!_modifiedCells.ContainsKey(row))
                    _modifiedCells[row] = new Dictionary<string, ModifiedCell>();

                _modifiedCells[row][field] = new ModifiedCell
                {
                    OldValue = oldValue,
                    NewValue = newValue
                };

                DataGridCell cell = GetParent<DataGridCell>(e.EditingElement);
                if (cell != null)
                    UpdateCellStyle(cell);

                //只刷这一行,不再全表刷
                ApplyRowStyle(e.Row);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        private bool ValuesAreEqual(object a, object b)
        {
            if (a == DBNull.Value) a = null;
            if (b == DBNull.Value) b = null;

            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            return a.ToString() == b.ToString();
        }

        private bool IsDateTimeType(Type type)
        {
            return type.FullName.Contains(nameof(DateTime));
        }

        private void UpdateCellStyle(DataGridCell cell)
        {
            try
            {
                if (cell == null || !(cell.DataContext is DataRowView drv)) return;
                string field = cell.Column?.SortMemberPath;
                if (string.IsNullOrEmpty(field)) return;

                DataRow row = drv.Row;
                bool isDBNull = row[field] == DBNull.Value;

                // 判断是否是修改过的单元格
                bool modified = _modifiedCells.TryGetValue(row, out var cols) && cols.ContainsKey(field);
                bool isAdded = _addedRows.Contains(row);
                bool isDeleted = _toDeleteRows.Contains(row);

                Style target;
                if (modified && !isAdded && !isDeleted)
                {
                    // 原有修改背景色逻辑保留
                    target = isDBNull ? GetModifiedNullCellStyle() : GetModifiedCellStyle();
                }
                else
                {
                    target = isDBNull ? GetNullCellStyle() : GetCellBaseStyle();
                }

                //缓存样式的实例复用:相同实例重复赋值不会再走一遍样式应用
                if (!ReferenceEquals(cell.Style, target))
                {
                    cell.Style = target;
                }
            }
            catch { }
        }

        /// <summary>
        /// 单元格基础样式(取一次缓存下来,不再每次查资源)
        /// </summary>
        private Style GetCellBaseStyle()
        {
            if (_cellBaseStyle == null)
            {
                _cellBaseStyle = TryFindResource("DataGridCellStyle") as Style ?? new Style(typeof(DataGridCell));
            }
            return _cellBaseStyle;
        }

        private Style GetNullCellStyle()
        {
            if (_cellNullStyle == null)
            {
                _cellNullStyle = CreateCellStyle(isModified: false, isNull: true);
            }
            return _cellNullStyle;
        }

        private Style GetModifiedCellStyle()
        {
            if (_cellModifiedStyle == null)
            {
                _cellModifiedStyle = CreateCellStyle(isModified: true, isNull: false);
            }
            return _cellModifiedStyle;
        }

        private Style GetModifiedNullCellStyle()
        {
            if (_cellModifiedNullStyle == null)
            {
                _cellModifiedNullStyle = CreateCellStyle(isModified: true, isNull: true);
            }
            return _cellModifiedNullStyle;
        }

        private Style CreateCellStyle(bool isModified, bool isNull)
        {
            Style style = new Style(typeof(DataGridCell), GetCellBaseStyle());
            if (isModified)
            {
                style.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 210, 160))));
            }
            if (isNull)
            {
                style.Setters.Add(new Setter(ForegroundProperty, Brushes.DarkGray));
                style.Setters.Add(new Setter(TextBlock.FontStyleProperty, FontStyles.Italic));
                style.Setters.Add(new Setter(FontStyleProperty, FontStyles.Italic));
            }
            return style;
        }

        /// <summary>
        /// 待删除行的底色样式(缓存一份,避免每行都 new)
        /// </summary>
        private Style GetDeletedRowStyle()
        {
            if (_deletedRowStyle == null)
            {
                Style style = new Style(typeof(DataGridRow), RowStyle);
                style.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 160, 160))));
                _deletedRowStyle = style;
            }
            return _deletedRowStyle;
        }

        /// <summary>
        /// 新增行的底色样式(缓存一份,避免每行都 new)
        /// </summary>
        private Style GetAddedRowStyle()
        {
            if (_addedRowStyle == null)
            {
                Style style = new Style(typeof(DataGridRow), RowStyle);
                style.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(160, 220, 160))));
                _addedRowStyle = style;
            }
            return _addedRowStyle;
        }

        protected override void OnSelectedCellsChanged(SelectedCellsChangedEventArgs e)
        {
            base.OnSelectedCellsChanged(e);
            foreach (DataGridCellInfo c in e.AddedCells.Concat(e.RemovedCells))
            {
                if (c.Column != null)
                {
                    FrameworkElement cellContent = c.Column.GetCellContent(c.Item);
                    DataGridCell cell = cellContent?.Parent as DataGridCell;
                    if (cell != null) UpdateCellStyle(cell);
                }
            }
            RefreshRowColors();
        }

        private void OnDataGridSorted(object sender, DataGridSortingEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate ()
            {
                RefreshAllStyles();
            });
        }

        private DataRowView GetSelectedRowView()
        {
            if (SelectedCells.Count == 0) return null;
            return SelectedCells[0].Item as DataRowView;
        }

        private T GetParent<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                d = VisualTreeHelper.GetParent(d);
                if (d is T) return (T)d;
            }
            return null;
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            Point point = e.GetPosition(this);

            if (IsInRowHeaderArea(point))
            {
                DataGridRow row = GetRowAtPoint(point);
                if (row == null) return;

                Focus();
                bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                if (!isCtrl && !isShift)
                {
                    SelectedItems.Clear();
                    row.IsSelected = true;
                    _lastSelectedRowForShift = row;
                }
                else if (isShift && _lastSelectedRowForShift != null)
                {
                    int start = Items.IndexOf(_lastSelectedRowForShift.Item);
                    int end = Items.IndexOf(row.Item);
                    if (start > end) { int t = start; start = end; end = t; }
                    for (int i = start; i <= end; i++)
                    {
                        DataGridRow r = ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;
                        if (r != null) r.IsSelected = true;
                    }
                }
                else if (isCtrl)
                {
                    row.IsSelected = !row.IsSelected;
                    _lastSelectedRowForShift = row;
                }

                if (Columns.Count > 0)
                    CurrentCell = new DataGridCellInfo(row.Item, Columns[0]);
                e.Handled = true;
            }
        }

        private bool IsInRowHeaderArea(Point point) => point.X <= RowHeaderWidth;

        private DataGridRow GetRowAtPoint(Point point)
        {
            DependencyObject element = VisualTreeHelper.HitTest(this, point).VisualHit;
            while (element != null && !(element is DataGridRow))
                element = VisualTreeHelper.GetParent(element);
            return element as DataGridRow;
        }

        public class DBNullToNullTextConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value == DBNull.Value || value == null)
                    return Null;
                return value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var text = value?.ToString().Trim();
                if (string.IsNullOrEmpty(text) || text == Null)
                    return DBNull.Value;
                return value;
            }
        }
        /// <summary>
        /// 空字符串(或空白)折叠,避免列头空的类型/注释行占位
        /// </summary>
        public class EmptyToCollapsedConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Binding.DoNothing;
            }
        }

        public class UniversalDateTimeConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value == null || value == DBNull.Value)
                    return Null;
                try
                {
                    DateTime dt = System.Convert.ToDateTime(value);
                    return dt.ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch
                {
                    return Null;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var text = value?.ToString().Trim();
                if (string.IsNullOrEmpty(text) || text == Null)
                    return DBNull.Value;
                return value;
            }
        }
    }
}