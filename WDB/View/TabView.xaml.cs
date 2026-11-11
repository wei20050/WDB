using HandyControl.Data;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WDB.Common;
using WDB.Entity;
using WDB.Window;

namespace WDB.View
{
    public partial class TabView : UserControl
    {
        TabViewModel _model;
        public CompletionWindow completionWindow;
        public ObservableCollection<TabModel> tabList = new ObservableCollection<TabModel>();

        /// <summary>
        /// 下拉里选中的原始连接(库/模式上下文由它派生)
        /// </summary>
        private LianJie _baseLj;

        /// <summary>
        /// 正在按代码设置下拉选中项(此时忽略 SelectionChanged,避免重复查库)
        /// </summary>
        private bool _ctxLoading;

        private bool _ctxInited;

        /// <summary>
        /// 本次执行是否走了分页(单条 SELECT 且没自带行数限制)
        /// </summary>
        private bool _pageable;

        /// <summary>
        /// 分页用的查询主体(不含 ORDER BY)与排序子句,执行时拆好供翻页复用
        /// </summary>
        private string _pageBody;
        private string _pageOrderBy;

        /// <summary>
        /// 正在查分页(避免连点翻页)
        /// </summary>
        private bool _pageLoading;

        /// <summary>
        /// 是否已订阅视图模型的通知
        /// </summary>
        private bool _pageHooked;

        public TabView()
        {
            InitializeComponent();
            //新建查询默认只显示SQL编辑部分,执行SQL后才显示数据部分
            SetDataAreaVisible(false);
            //AI助手面板自己不认识库/表结构:由本页给上下文,并把生成的语句写回编辑器
            AiChat.CaptureContext = CaptureAiContext;
            AiChat.BuildAsk = BuildAiAsk;
            AiChat.SqlReady = WriteAiSql;
            AiChat.CloseRequested += CloseAiChat;
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //只接受本页的视图模型:ContentPresenter 有可能把外层标签对象当数据上下文传下来,
            //不能因为这种继承覆盖掉已经拿到的_model(否则后续执行SQL会在这里空引用)
            if (e.NewValue is TabViewModel tab)
            {
                _model = tab;
                InitContext();
                HookModel();
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            HookModel();
            TextEditor.TextArea.TextEntered += TextArea_TextEntered;
            HighlightingProvider.Register(SkinType.Dark, new HighlightingProviderDark());
            TextEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "SQL");
            TextEditor.TextArea.SelectionCornerRadius = 0;
            TextEditor.TextArea.SelectionBorder = null;
            TextEditor.TextArea.SelectionForeground = null;
            TextEditor.TextArea.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFADD6FF"));

            //默认展开右侧 AI 助手(不抢输入框焦点,焦点仍留在编辑器)。
            //只在本页第一次加载时展开:切页签会重新触发 Loaded,用户手动收起后不该又被弹开
            if (!_aiDefaultOpened)
            {
                _aiDefaultOpened = true;
                if (!_aiOpen)
                {
                    OpenAiChat(false);
                }
            }

            TextEditor.Focus();
            TextEditor.SelectionStart = TextEditor.Text.Length;
        }

        #region 智能提示

        /// <summary>
        /// 当前上下文下的表/视图(智能提示用,切换连接/库/模式后重新加载)
        /// </summary>
        private List<Biao> _biaoList;

        /// <summary>
        /// 字段缓存(表名 -> 字段):SQL里引用到的表按需加载
        /// </summary>
        private readonly Dictionary<string, List<ZiDuan>> _ziDuanCache = new Dictionary<string, List<ZiDuan>>();

        /// <summary>
        /// 正在加载字段的表(避免重复查询)
        /// </summary>
        private readonly HashSet<string> _ziDuanLoading = new HashSet<string>();

        /// <summary>
        /// 匹配SQL里 FROM/JOIN 后面的"表名 [as 别名]"
        /// </summary>
        private static readonly Regex TableAliasRegex = new Regex(@"\b(?:from|join)\s+([`""\[\]\w\.]+)(?:\s+(?:as\s+)?([A-Za-z_][\w$]*))?", RegexOptions.IgnoreCase);

        /// <summary>
        /// 匹配SQL里的子句关键字(用于判断光标前处在哪个子句里)
        /// </summary>
        private static readonly Regex ClauseRegex = new Regex(
            @"\b(select|from|where|group\s+by|order\s+by|having|on|join|inner\s+join|left\s+join|right\s+join|full\s+join|cross\s+join|set|values|into|update|insert|union|and|or)\b",
            RegexOptions.IgnoreCase);

        /// <summary>
        /// 比较运算符:这些符号后面该填"值",不再优先提示字段
        /// </summary>
        private static readonly HashSet<string> ValueOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "=", "<>", "!=", ">", ">=", "<", "<=", "like", "not", "in", "between", "is"
        };

        /// <summary>
        /// 不能当成表别名的关键字
        /// </summary>
        private static readonly HashSet<string> SqlKeywordsForAlias = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "where", "join", "inner", "left", "right", "full", "outer", "cross", "on", "group", "order", "having",
            "limit", "union", "all", "as", "and", "or", "set", "values", "select", "from", "by", "using", "with",
            "distinct", "top", "desc", "asc", "offset", "fetch", "rownum"
        };

        /// <summary>
        /// 弹出智能提示:字母/数字/下划线/点号/空格都按当前光标位置给出候选,
        /// 每次输入都重建提示框(不用AvalonEdit的增量过滤),没有匹配项就不弹,避免留下空白框
        /// </summary>
        private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
            {
                return;
            }
            //先关掉旧提示框:否则旧列表被过滤空之后会留一个空白框
            CloseCompletion();
            var c = e.Text[0];
            var isTypingWord = char.IsLetterOrDigit(c) || c == '_' || c == '$';
            if (!isTypingWord && c != '.' && c != ' ')
            {
                return;
            }
            //延后一帧:确保刚输入的字符已经进入文档,再按光标前的文本取词
            _ = Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    //智能提示出问题不影响编辑,单独兜住并记日志
                    try
                    {
                        ShowCompletion();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"智能提示异常:{ex}");
                    }
                }),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 关闭当前提示框
        /// </summary>
        private void CloseCompletion()
        {
            var win = completionWindow;
            completionWindow = null;
            win?.Close();
        }

        /// <summary>
        /// 解析光标前正在输入的词:qualifier 为限定符(u.name 里的 u),prefix 为匹配前缀
        /// </summary>
        private void ParseInputWord(out string qualifier, out string prefix)
        {
            qualifier = null;
            prefix = string.Empty;
            var text = TextEditor.Text;
            var end = Math.Min(TextEditor.CaretOffset, text.Length);
            var start = end;
            while (start > 0 && IsWordChar(text[start - 1]))
            {
                start--;
            }
            var word = text.Substring(start, end - start);
            var dot = word.LastIndexOf('.');
            if (dot >= 0)
            {
                qualifier = word.Substring(0, dot).Trim();
                prefix = word.Substring(dot + 1);
            }
            else
            {
                prefix = word;
            }
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '.';
        }

        /// <summary>
        /// 光标之前的SQL文本
        /// </summary>
        private string GetTextBeforeCaret()
        {
            var text = TextEditor.Text;
            var offset = Math.Min(TextEditor.CaretOffset, text.Length);
            return text.Substring(0, offset);
        }

        /// <summary>
        /// 分析光标前的SQL,判断当前处在哪个子句里(取当前语句里最后一个子句关键字)
        /// </summary>
        private string GetClauseContext()
        {
            var before = GetTextBeforeCaret();
            //只看最后一条语句
            var index = before.LastIndexOf(';');
            if (index >= 0)
            {
                before = before.Substring(index + 1);
            }
            //刚写完比较运算符:这里是"值"的位置
            if (ValueOperators.Contains(GetLastToken(before)))
            {
                return "value";
            }
            Match last = null;
            foreach (Match m in ClauseRegex.Matches(before))
            {
                last = m;
            }
            return last?.Value.ToLowerInvariant();
        }

        /// <summary>
        /// 取文本里最后一个词(忽略结尾空白)
        /// </summary>
        private static string GetLastToken(string text)
        {
            var t = text.TrimEnd();
            var start = t.Length;
            while (start > 0 && !char.IsWhiteSpace(t[start - 1]))
            {
                start--;
            }
            return t.Substring(start);
        }

        /// <summary>
        /// 各类候选在不同子句下的权重(数值越大越靠前):
        /// 语句开头推关键字、列清单推字段与 *、FROM/JOIN 推表、WHERE 推字段与运算符…
        /// </summary>
        private static CompletionWeight GetWeight(string clause)
        {
            if (string.IsNullOrEmpty(clause))
            {
                //语句开头:先提示关键字(SELECT/INSERT/…),再提示表
                return new CompletionWeight { Column = 10, Table = 70, View = 60, Keyword = 100, Func = 10 };
            }
            if (clause.Contains("join"))
            {
                return new CompletionWeight { Column = 20, Table = 100, View = 90, Keyword = 60, Func = 5 };
            }
            switch (clause)
            {
                case "select":
                    //列清单:字段与 * 优先,其次是函数、关键字(FROM/DISTINCT…),表放最后
                    return new CompletionWeight { Star = 110, Column = 100, Table = 20, View = 15, Keyword = 40, Func = 60 };
                case "from":
                case "into":
                case "update":
                case "insert":
                    return new CompletionWeight { Column = 20, Table = 100, View = 90, Keyword = 60, Func = 5 };
                case "where":
                case "on":
                case "having":
                case "and":
                case "or":
                    //条件:字段优先,其次是 LIKE/IN/IS 这类关键字与函数
                    return new CompletionWeight { Column = 100, Table = 30, View = 25, Keyword = 70, Func = 50 };
                case "group by":
                case "order by":
                    return new CompletionWeight { Column = 100, Table = 10, View = 10, Keyword = 60, Func = 30 };
                case "set":
                    return new CompletionWeight { Column = 100, Table = 10, View = 10, Keyword = 50, Func = 40 };
                case "values":
                    return new CompletionWeight { Column = 10, Table = 10, Keyword = 40, Func = 60 };
                case "value":
                    //比较符后面是值,值猜不出来,退化为提示函数与关键字
                    return new CompletionWeight { Column = 5, Table = 5, Keyword = 60, Func = 70 };
                case "union":
                    return new CompletionWeight { Column = 10, Table = 70, View = 60, Keyword = 100, Func = 10 };
                default:
                    return new CompletionWeight { Column = 60, Table = 60, View = 55, Keyword = 60, Func = 30 };
            }
        }

        private void ShowCompletion()
        {
            string qualifier;
            string prefix;
            ParseInputWord(out qualifier, out prefix);
            var data = new List<ICompletionData>();
            var exists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            //每次输入都按光标前的SQL重新分析上下文,推荐顺序随子句变化(不再固定排序)
            var weight = GetWeight(GetClauseContext());
            if (!string.IsNullOrEmpty(qualifier))
            {
                //写了别名/表名限定的字段:只提示该表的字段
                AddNameItems(data, exists, GetZiDuanNames(qualifier), prefix, "Col", weight.Column);
            }
            else
            {
                if (weight.Star > 0)
                {
                    AddNameItems(data, exists, new[] { "*" }, prefix, null, weight.Star);
                }
                AddNameItems(data, exists, GetZiDuanNames(null), prefix, "Col", weight.Column);
                var biaoList = _biaoList;
                AddBiaoItems(data, exists, biaoList?.Where(p => !p.IsView), prefix, "Table", weight.Table);
                AddBiaoItems(data, exists, biaoList?.Where(p => p.IsView), prefix, "View", weight.View);
                AddNameItems(data, exists, GConst.Sys_SqlKeywords, prefix, null, weight.Keyword);
                AddNameItems(data, exists, GConst.Sys_SqlFuns, prefix, "Func", weight.Func);
            }
            if (data.Count == 0)
            {
                //没有匹配项时什么都不显示(不弹空框)
                return;
            }
            ShowCompletionWindow(data);
        }

        /// <summary>
        /// 一类候选在当前子句下的权重
        /// </summary>
        private class CompletionWeight
        {
            /// <summary>* 通配符(仅 select 列清单使用)</summary>
            public double Star { get; set; }

            public double Column { get; set; }
            public double Table { get; set; }
            public double View { get; set; }
            public double Keyword { get; set; }
            public double Func { get; set; }
        }

        /// <summary>
        /// 按前缀匹配添加表/视图提示项(名称去重,保证先加的表优先)
        /// </summary>
        private static void AddBiaoItems(ICollection<ICompletionData> data, ISet<string> exists, IEnumerable<Biao> list, string prefix, string type, double priority)
        {
            if (list == null)
            {
                return;
            }
            var index = 0;
            foreach (var b in list)
            {
                if (b == null || string.IsNullOrEmpty(b.Name) || b.Name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    continue;
                }
                if (!exists.Add(b.Name))
                {
                    continue;
                }
                //同一类里保持加入顺序:逐个递减一个极小值,保证排序稳定
                data.Add(new SqlCompletionData(b.Name, type, b.Comment, priority - index * 0.0001));
                index++;
            }
        }

        /// <summary>
        /// 按前缀匹配添加字段/关键字提示项(名称去重)
        /// </summary>
        private static void AddNameItems(ICollection<ICompletionData> data, ISet<string> exists, IEnumerable<string> names, string prefix, string type, double priority)
        {
            if (names == null)
            {
                return;
            }
            var index = 0;
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name) || name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    continue;
                }
                if (!exists.Add(name))
                {
                    continue;
                }
                //同一类里保持加入顺序:逐个递减一个极小值,保证排序稳定
                data.Add(new SqlCompletionData(name, type, null, priority - index * 0.0001));
                index++;
            }
        }

        /// <summary>
        /// 参与提示的字段名:限定符(别名/表名)能定位到表时只取该表字段,否则取SQL里引用到的所有表字段
        /// </summary>
        private IEnumerable<string> GetZiDuanNames(string qualifier)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(qualifier) && _model?.B?.ZiDuanList != null)
            {
                names.AddRange(_model.B.ZiDuanList.Select(p => p.Name));
            }
            foreach (var b in GetZiDuanBiaoList(qualifier))
            {
                List<ZiDuan> zd;
                if (_ziDuanCache.TryGetValue(b.Name, out zd))
                {
                    if (zd != null)
                    {
                        names.AddRange(zd.Select(p => p.Name));
                    }
                    continue;
                }
                //第一次遇到该表:后台把字段查出来缓存,下次输入时即可提示(不阻塞输入)
                LoadZiDuanAsync(b);
            }
            return names;
        }

        /// <summary>
        /// 字段来源的表:带限定符时按别名/表名匹配,否则取SQL里 FROM/JOIN 引用到的所有表
        /// </summary>
        private List<Biao> GetZiDuanBiaoList(string qualifier)
        {
            var refs = GetSqlTableRefs();
            var list = string.IsNullOrEmpty(qualifier)
                ? refs.Select(p => p.Value)
                : refs.Where(p => string.Equals(p.Key, qualifier, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value);
            var ret = new List<Biao>();
            foreach (var b in list)
            {
                if (b != null && !ret.Contains(b))
                {
                    ret.Add(b);
                }
            }
            return ret;
        }

        /// <summary>
        /// 解析SQL里 FROM/JOIN 的表与别名(Key为别名或表名,Value为表),只认当前上下文里存在的表
        /// </summary>
        private List<KeyValuePair<string, Biao>> GetSqlTableRefs()
        {
            var refs = new List<KeyValuePair<string, Biao>>();
            var biaoList = _biaoList;
            if (biaoList == null || biaoList.Count == 0)
            {
                return refs;
            }
            foreach (Match m in TableAliasRegex.Matches(TextEditor.Text))
            {
                var name = CleanTableName(m.Groups[1].Value);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                var b = biaoList.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (b == null)
                {
                    continue;
                }
                //别名与表名都记上,方便 u.name 与 表名.name 两种写法
                var alias = m.Groups[2].Success ? m.Groups[2].Value : null;
                if (!string.IsNullOrEmpty(alias) && !SqlKeywordsForAlias.Contains(alias))
                {
                    AddTableRef(refs, alias, b);
                }
                AddTableRef(refs, b.Name, b);
            }
            return refs;
        }

        private static void AddTableRef(List<KeyValuePair<string, Biao>> refs, string key, Biao b)
        {
            if (string.IsNullOrEmpty(key) || refs.Any(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            refs.Add(new KeyValuePair<string, Biao>(key, b));
        }

        /// <summary>
        /// 去掉表名的引号与库/模式前缀(dbo.users -> users)
        /// </summary>
        private static string CleanTableName(string name)
        {
            var n = name.Trim().Trim('`', '"', '[', ']');
            var dot = n.LastIndexOf('.');
            if (dot >= 0)
            {
                n = n.Substring(dot + 1).Trim().Trim('`', '"', '[', ']');
            }
            return n;
        }

        private void LoadZiDuanAsync(Biao b)
        {
            var lj = _model?.Lj;
            if (lj == null || !_ziDuanLoading.Add(b.Name))
            {
                return;
            }
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    List<ZiDuan> zd = null;
                    try
                    {
                        zd = Db.GetZiDuans(b, lj, true);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                    }
                    Dispatcher.Invoke(new Action(() =>
                    {
                        _ziDuanLoading.Remove(b.Name);
                        _ziDuanCache[b.Name] = zd;
                    }));
                }));
        }

        /// <summary>
        /// 加载当前上下文下的表/视图供智能提示使用(切换连接/库/模式时重新加载)
        /// </summary>
        private void LoadBiaoList()
        {
            var lj = _model?.Lj;
            if (lj == null)
            {
                return;
            }
            _biaoList = null;
            _ziDuanCache.Clear();
            _ziDuanLoading.Clear();
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    List<Biao> list = null;
                    try
                    {
                        list = string.IsNullOrEmpty(lj.ServiceName) ? Db.GetTables(lj) : Db.GetTablesBySchema(lj, lj.ServiceName);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                    }
                    Dispatcher.Invoke(new Action(() =>
                    {
                        _biaoList = list;
                        //AI助手面板开着的话,把刚拿到的表填进"相关表"下拉(已勾选的会保住)
                        if (_aiOpen)
                        {
                            RefreshAiTables();
                        }
                    }));
                }));
        }

        private void ShowCompletionWindow(List<ICompletionData> data)
        {
            //同一帧内可能排了多次重建,打开前先关掉旧的,保证同一时间只有一个提示框
            CloseCompletion();
            completionWindow = new CompletionWindow(TextEditor.TextArea);
            completionWindow.BorderThickness = new Thickness(0);
            var list = completionWindow.CompletionList.CompletionData;
            foreach (var item in data)
            {
                list.Add(item);
            }
            completionWindow.Show();
            completionWindow.Closed += delegate
            {
                completionWindow = null;
            };
            completionWindow.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Space && !IsFocused)
                {
                    //空格结束当前提示框,并为"select 空格"这类位置重新给出候选
                    CloseCompletion();
                }
            };
            //默认选择第一项
            completionWindow.CompletionList.SelectedItem = data[0];
            completionWindow.Width = 550;
            var listBox = completionWindow.CompletionList.ListBox;
            listBox.Margin = new Thickness(0);
            //设置ListBox的边框样式
            listBox.BorderThickness = new Thickness(0);
            //创建新的样式以应用于每个ListBox项
            var style = new Style(typeof(ListBoxItem));
            style.BasedOn = listBox.ItemContainerStyle;
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 25.0));
            //创建触发器,以便在项被选中时更改背景色
            var trigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            trigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#aee1ff"))));
            trigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000"))));
            style.Triggers.Add(trigger);
            //应用样式到ListBox的每个项
            listBox.ItemContainerStyle = style;
        }

        #endregion

        /// <summary>
        /// 执行sql点击
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Zhixing_Click(object sender, RoutedEventArgs e)
        {
            var sqlStr = string.IsNullOrEmpty(TextEditor.SelectedText.Trim()) ? TextEditor.Text.Trim() : TextEditor.SelectedText.Trim();
            if (string.IsNullOrEmpty(sqlStr))
            {
                Msg.Warning("SQL不能为空!");
                return;
            }
            Zhixing(sqlStr);
        }

        private void Meihua_Click(object sender, RoutedEventArgs e)
        {
            var sqlScript = TextEditor.Text;
            if (string.IsNullOrEmpty(sqlScript))
            {
                return;
            }
            if (string.IsNullOrEmpty(TextEditor.SelectedText))
            {
                TextEditor.Text = sqlScript.SqlFormat();
                return;
            }
            var oldSqlScript = TextEditor.SelectedText;
            sqlScript = TextEditor.SelectedText;
            TextEditor.Text = TextEditor.Text.Replace(oldSqlScript, sqlScript.SqlFormat());
        }

        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            SafeClipboard.SetText(TextEditor.SelectedText);
        }

        private void MenuPaste_Click(object sender, RoutedEventArgs e)
        {
            TextEditor.Paste();
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            TextEditor.Delete();
        }

        private void Ningcunwei_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.Filter = "SQL 文件(*.sql)|*.sql|所有文件(*.*)|*.*|文本文件(*.txt)|*.txt"; // 设置保存文件的筛选器
            saveFileDialog.FileName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var fileName = saveFileDialog.FileName;
                TextEditor.Save(fileName);
            }
        }

        #region AI助手

        /// <summary>AI助手面板是否已展开</summary>
        private bool _aiOpen;

        /// <summary>是否已经做过"首次加载默认展开"(切页签会重新 Loaded,不能重复弹开)</summary>
        private bool _aiDefaultOpened;

        /// <summary>面板展开时的宽度(收起后记住,再次展开沿用)</summary>
        private double _aiWidth = 460;

        /// <summary>AI助手提前抓的快照:提问时的连接上下文</summary>
        private LianJie _aiLj;

        /// <summary>AI助手提前抓的快照:提问涉及的表</summary>
        private List<Biao> _aiRefs;

        /// <summary>AI助手提前抓的快照:字段缓存副本(后台拼表结构用,避免跨线程读共享缓存)</summary>
        private Dictionary<string, List<ZiDuan>> _aiZiDuans;

        /// <summary>AI助手提前抓的快照:当前上下文的表清单(问题里点名了表时兜底用)</summary>
        private List<Biao> _aiBiaoList;

        /// <summary>
        /// [AI助手]:展开/收起编辑器右侧的对话面板
        /// </summary>
        private void AI_Click(object sender, RoutedEventArgs e)
        {
            if (_aiOpen)
            {
                CloseAiChat();
                return;
            }
            OpenAiChat();
        }

        /// <summary>
        /// 展开 AI 助手面板
        /// </summary>
        /// <param name="focusInput">是否把焦点放进提问框(页面加载时默认展开就不抢焦点)</param>
        private void OpenAiChat(bool focusInput = true)
        {
            _aiOpen = true;
            if (ColAi.ActualWidth > 0)
            {
                _aiWidth = ColAi.ActualWidth;
            }
            ColAi.MinWidth = 300;
            //页面窄的时候别让面板把编辑器挤没了
            ColAi.Width = new GridLength(ActualWidth > 0 ? Math.Min(_aiWidth, Math.Max(240, ActualWidth / 2)) : _aiWidth);
            ColAiSplit.Width = new GridLength(4);
            SplitterAi.Visibility = Visibility.Visible;
            AiChat.Visibility = Visibility.Visible;
            RefreshAiTables();
            if (_biaoList == null)
            {
                //表清单还没拉回来:拉一次,回来时会自动填进"相关表"下拉
                LoadBiaoList();
            }
            if (focusInput)
            {
                AiChat.FocusInput();
            }
        }

        /// <summary>把当前上下文的表填进AI助手的"相关表"下拉(默认勾上打开该页时所在的那张表)</summary>
        private void RefreshAiTables()
        {
            AiChat.SetTables(
                _biaoList == null ? new List<string>() : _biaoList.Select(p => p.Name).ToList(),
                string.IsNullOrEmpty(_model?.B?.Name) ? null : new List<string> { _model.B.Name });
        }

        /// <summary>
        /// 设置里改完 AI 模型后刷新右侧助手面板的模型下拉(每个查询页签都持有一个助手面板)
        /// </summary>
        public void RefreshAiModels()
        {
            AiChat.RefreshModels();
        }

        private void CloseAiChat()
        {
            if (ColAi.ActualWidth > 0)
            {
                _aiWidth = ColAi.ActualWidth;
            }
            _aiOpen = false;
            ColAi.MinWidth = 0;
            ColAi.Width = new GridLength(0);
            ColAiSplit.Width = new GridLength(0);
            SplitterAi.Visibility = Visibility.Collapsed;
            AiChat.Visibility = Visibility.Collapsed;
            //焦点跟着一起还回编辑器,免得留在已经收起的输入框上
            TextEditor.Focus();
        }

        /// <summary>
        /// AI助手:抓提问上下文快照。主线程调用,后台拼提示词时只读这里的数据(不碰控件)
        /// </summary>
        private void CaptureAiContext()
        {
            _aiLj = _model?.Lj;
            _aiBiaoList = _biaoList;
            _aiZiDuans = new Dictionary<string, List<ZiDuan>>(_ziDuanCache);
            var refs = new List<Biao>();
            foreach (var item in GetSqlTableRefs())
            {
                if (item.Value != null && !refs.Contains(item.Value))
                {
                    refs.Add(item.Value);
                }
            }
            //编辑器里还没写表时,用打开该页时所在的那张表兜底
            if (refs.Count == 0 && _model?.B != null)
            {
                refs.Add(_model.B);
            }
            _aiRefs = refs;
        }

        /// <summary>
        /// AI助手:按快照拼本次请求(普通对话与SQL技能都会走这里)。
        /// 后台线程调用,所以这里只能读快照;系统提示词只在SQL技能下生效
        /// </summary>
        private AiChatAsk BuildAiAsk(string need, List<string> tables)
        {
            var lj = _aiLj;
            return new AiChatAsk
            {
                System = AIHelp.SqlSystem,
                User = AIHelp.BuildQueryPrompt(lj?.DataBaseType.ToString(), BuildAiContextText(lj),
                    BuildAiSchemaText(lj, GetAiTables(need, tables)), need)
            };
        }

        /// <summary>
        /// AI助手:这次要带哪些表的结构:优先顶部勾选的相关表,没勾选再用编辑器里写的表,
        /// 还没有就看问题里提到了当前上下文中的哪些表
        /// </summary>
        private List<Biao> GetAiTables(string need, List<string> tables)
        {
            var list = new List<Biao>();
            if (tables != null && tables.Count > 0)
            {
                foreach (var name in tables)
                {
                    var b = _aiBiaoList?.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                    //勾选的表不在当前上下文里也照样带上(字段按名字去查)
                    list.Add(b ?? new Biao { Name = name });
                }
                return list;
            }
            list.AddRange(_aiRefs ?? new List<Biao>());
            if (list.Count == 0 && _aiBiaoList != null && !string.IsNullOrEmpty(need))
            {
                //问题里点名的表(如"查询users表最近7天注册用户")也带上,否则模型看不到字段只能靠猜
                foreach (var b in _aiBiaoList)
                {
                    if (!string.IsNullOrEmpty(b.Name) && need.IndexOf(b.Name, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        !list.Contains(b))
                    {
                        list.Add(b);
                    }
                    if (list.Count >= 5)
                    {
                        //带太多表结构又慢又容易跑偏
                        break;
                    }
                }
            }
            if (list.Count == 0 && string.IsNullOrEmpty(need) && _aiBiaoList != null)
            {
                //一句问题都没有(比如"提取提示词"):随便带几张上下文里的表,
                //保证提示词里至少有库/表/字段可参考,贴到网页版大模型后能直接接着问
                foreach (var b in _aiBiaoList)
                {
                    if (string.IsNullOrEmpty(b.Name) || list.Contains(b))
                    {
                        continue;
                    }
                    list.Add(b);
                    if (list.Count >= 5)
                    {
                        break;
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// AI助手:库/模式上下文(没有的可省)
        /// </summary>
        private static string BuildAiContextText(LianJie lj)
        {
            if (lj == null)
            {
                return null;
            }
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(lj.DataBaseName))
            {
                parts.Add("库=" + lj.DataBaseName.Trim());
            }
            if (!string.IsNullOrWhiteSpace(lj.ServiceName))
            {
                parts.Add("模式=" + lj.ServiceName.Trim());
            }
            return parts.Count == 0 ? null : string.Join("  ", parts);
        }

        /// <summary>
        /// AI助手:拼紧凑表结构,每个字段一行"名称 类型 标记 -- 注释"(与"AI提问"窗口同口径)
        /// </summary>
        private string BuildAiSchemaText(LianJie lj, List<Biao> items)
        {
            if (lj == null || items == null || items.Count == 0)
            {
                return null;
            }
            var sb = new StringBuilder();
            foreach (var b in items)
            {
                sb.Append("表 ").Append(b.Name);
                if (!string.IsNullOrWhiteSpace(b.Comment))
                {
                    sb.Append('(').Append(b.Comment.Trim()).Append(')');
                }
                sb.AppendLine();
                foreach (var z in GetAiZiDuans(lj, b))
                {
                    sb.AppendLine(AIHelp.BuildZiDuanLine(z));
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// AI助手:取字段清单(优先用快照里的,缺的在后台补查一次;不写共享缓存,免得和智能提示抢)
        /// </summary>
        private List<ZiDuan> GetAiZiDuans(LianJie lj, Biao b)
        {
            if (b.ZiDuanList != null && b.ZiDuanList.Count > 0)
            {
                return b.ZiDuanList;
            }
            List<ZiDuan> zd;
            if (_aiZiDuans != null && _aiZiDuans.TryGetValue(b.Name, out zd) && zd != null && zd.Count > 0)
            {
                return zd;
            }
            try
            {
                return Db.GetZiDuans(b, lj, true) ?? new List<ZiDuan>();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return new List<ZiDuan>();
            }
        }

        /// <summary>
        /// AI助手(SQL技能):把AI回复里的语句插到编辑器光标处(与原来"AI回复已插入"同一口径,可按Ctrl+Z撤销)
        /// </summary>
        private void WriteAiSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return;
            }
            TextEditor.SelectedText = Environment.NewLine + sql + Environment.NewLine;
        }

        #endregion

        private void Daoru_Click(object sender, RoutedEventArgs e)
        {
            var open = new System.Windows.Forms.OpenFileDialog
            {
                Filter = @"sql文件(*.sql)|*.sql|所有文件(*.*)|*.*"
            };
            if (open.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var str = File.ReadAllText(open.FileName);
                _model.SqlStr += Environment.NewLine + str;
            }
        }

        private void TextEditor_DragEnter(object sender, DragEventArgs e)
        {
            // 判断拖过来的是不是文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 鼠标样式显示为可放置
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void TextEditor_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // 获取拖放的文件路径数组
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null && files.Length > 0)
                {
                    // 取第一个文件（支持多文件拖放，这里只读取第一个）
                    string filePath = files[0];

                    // 读取文件内容（自动识别编码，支持中文不乱码）
                    string str = File.ReadAllText(filePath);
                    _model.SqlStr += Environment.NewLine + str;
                }
            }
            catch (Exception ex)
            {
                Oops.Error(ex.Message);
            }
        }

        private void Zhixing(string sqlStr)
        {
            if (string.IsNullOrEmpty(sqlStr))
            {
                return;
            }
            var lj = _model?.Lj;
            if (lj == null)
            {
                Msg.Warning("当前查询页的连接上下文已失效,请重新打开新建查询");
                return;
            }
            //每次执行先收起分页条,成功后按需显示
            SetPageBarVisible(false);
            //单条 SELECT 走分页:只取当前页,几十万行的结果集不会再一次性拉回来把界面拖死;
            //多语句、非查询语句、自己写了行数限制(LIMIT/TOP/ROWNUM等)的仍按原样执行
            if (Db.TrySplitForPage(sqlStr, out _pageBody, out _pageOrderBy))
            {
                _pageable = true;
                _model.PageIndex = 1;
                QueryPage();
                return;
            }
            _pageable = false;
            RunSql(lj, sqlStr);
        }

        /// <summary>
        /// 按原样执行SQL(不分页的场景)
        /// </summary>
        private void RunSql(LianJie lj, string sqlStr)
        {
            SetLoading(true);
            _ = Task.Factory.StartNew(
               new Action(() =>
               {
                   try
                   {
                       var ds = Db.GetDataTable(lj, sqlStr, out var isRet, out _);
                       //附上字段类型/注释/主键,供结果表头显示(后台线程,取不到不影响数据展示)
                       AttachResultFieldMeta(lj, sqlStr, ds);
                       Dispatcher.Invoke(new Action(() =>
                       {
                           SetLoading(false);
                           if (isRet)
                           {
                               ShowResult(lj, ds);
                           }
                       }));
                   }
                   catch (Exception ex)
                   {
                       //异常也必须收掉加载遮罩并把错误抛给用户,否则页面会一直停在加载状态(看起来"数据再也不显示")
                       Log.Error($"执行SQL失败:{ex}");
                       Dispatcher.Invoke(new Action(() =>
                       {
                           SetLoading(false);
                           Msg.Warning(ex.Message);
                       }));
                   }
               }));
        }

        /// <summary>
        /// 查询当前页(每页多取一条,用于判断是否还有下一页)
        /// </summary>
        private void QueryPage()
        {
            var lj = _model?.Lj;
            if (lj == null || string.IsNullOrEmpty(_pageBody))
            {
                return;
            }
            var pageSize = _model.PageSize > 0 ? _model.PageSize : 100;
            var index = _model.PageIndex < 1 ? 1 : _model.PageIndex;
            var offset = (index - 1) * pageSize;
            var sql = Db.BuildPageSql(lj, _pageBody, _pageOrderBy, offset, pageSize + 1);
            if (string.IsNullOrEmpty(sql))
            {
                return;
            }
            _pageLoading = true;
            SetLoading(true);
            _ = Task.Factory.StartNew(
               new Action(() =>
               {
                   try
                   {
                       //静默执行:翻页不再弹"执行成功,查到数据行数"
                       var ds = Db.GetDataTable(lj, sql, out var isRet, out _, true);
                       //附上字段类型/注释/主键,供结果表头显示(按查询主体解析表名)
                       AttachResultFieldMeta(lj, _pageBody, ds);
                       Dispatcher.Invoke(new Action(() =>
                       {
                           _pageLoading = false;
                           SetLoading(false);
                           if (!isRet || ds == null || ds.Tables.Count == 0)
                           {
                               return;
                           }
                           var dt = ds.Tables[0];
                           Db.RemovePageHelperColumns(dt);
                           var hasNext = dt.Rows.Count > pageSize;
                           if (hasNext)
                           {
                               //多取的那条只用于判断下一页,不显示
                               dt.Rows.RemoveAt(dt.Rows.Count - 1);
                           }
                           _model.PageIndex = index;
                           _model.HasNext = hasNext;
                           _model.CanPrev = index > 1;
                           _model.PageInfo = hasNext
                               ? $"第 {index} 页,本页 {dt.Rows.Count} 条"
                               : $"第 {index} 页,本页 {dt.Rows.Count} 条(已是最后一页)";
                           //分页SQL外面包了一层(Oracle/SqlServer),按查询主体取表名,页签显示才直观
                           dt.TableName = Db.GetFirstTableNameFromSql(_pageBody) + $" ({index})";
                           SetPageBarVisible(true);
                           ShowResult(lj, ds);
                       }));
                   }
                   catch (Exception ex)
                   {
                       Log.Error($"分页查询失败:{ex}");
                       Dispatcher.Invoke(new Action(() =>
                       {
                           _pageLoading = false;
                           SetLoading(false);
                           Msg.Warning(ex.Message);
                       }));
                   }
               }));
        }

        #region 分页

        private void BtnFirstPage_Click(object sender, RoutedEventArgs e)
        {
            GotoPage(1);
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_model != null)
            {
                GotoPage(_model.PageIndex - 1);
            }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_model != null)
            {
                GotoPage(_model.PageIndex + 1);
            }
        }

        /// <summary>
        /// 翻页:按本次执行已拆好的查询主体重查(编辑器里改了SQL要重新执行才对翻页生效)
        /// </summary>
        private void GotoPage(int index)
        {
            if (!_pageable || _pageLoading || _model == null || index < 1)
            {
                return;
            }
            _model.PageIndex = index;
            QueryPage();
        }

        /// <summary>
        /// 每页条数改变:回第一页重查
        /// </summary>
        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TabViewModel.PageSize) && _pageable && !_pageLoading)
            {
                _model.PageIndex = 1;
                QueryPage();
            }
        }

        /// <summary>
        /// 订阅视图模型的通知(Fody 织入的属性变更只能通过接口订阅)
        /// </summary>
        private void HookModel()
        {
            if (_model == null || _pageHooked)
            {
                return;
            }
            _pageHooked = true;
            ((INotifyPropertyChanged)_model).PropertyChanged += Model_PropertyChanged;
        }

        /// <summary>
        /// 分页条显示/隐藏(只有可分页的执行才出现)
        /// </summary>
        private void SetPageBarVisible(bool visible)
        {
            PanelPage.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        /// <summary>
        /// 显示查询结果:先展开底部数据区,再把每个结果集放进一个表格页签
        /// </summary>
        private void ShowResult(LianJie lj, DataSet ds)
        {
            try
            {
                //先展开数据区:即使后面构造表格出错,也不会出现"数据区再也不显示"的情况
                SetDataAreaVisible(true);
                tabList = new ObservableCollection<TabModel>();
                if (ds != null)
                {
                    //结果集列上的字段类型/注释/主键在执行时已附好(见 AttachResultFieldMeta),这里只负责显示
                    foreach (DataTable t in ds.Tables)
                    {
                        var tab = new DataTableEditDataGridViewModel
                        {
                            Lj = lj,
                            Dt = t,
                            //查询结果一律只读:多表连接的结果直接改数据会落到猜出来的目标表上,改数据走"查看数据"页
                            ReadOnly = true
                        };
                        var dataTableEditDataGridView = new DataTableEditDataGridView
                        {
                            DataContext = tab
                        };
                        TabModel tabItem = new TabModel
                        {
                            Header = t.TableName,
                            Content = dataTableEditDataGridView
                        };
                        tabList.Add(tabItem);
                    }
                }
                tc.ItemsSource = tabList;
                if (tabList.Count > 0)
                {
                    tc.SelectedIndex = 0;
                }
                //筛选列:按结果集列补齐勾选项,并把已有勾选状态应用到新表格
                LieColumnHelper.EnsureItems(_model?.LieList, ds?.Tables.Cast<DataTable>());
                ApplyLieColumns();
            }
            catch (Exception ex)
            {
                Log.Error($"显示查询结果失败:{ex}");
                Msg.Warning(ex.Message);
            }
        }

        /// <summary>
        /// 给结果集列附上字段类型/注释/主键(供结果表头显示,与"查看数据"页表头一致):
        /// 按SQL里 FROM/JOIN 引用的表取字段清单,取不到只影响表头显示,不弹提示(后台线程调用)
        /// </summary>
        private void AttachResultFieldMeta(LianJie lj, string sql, DataSet ds)
        {
            try
            {
                if (lj == null || ds == null || ds.Tables.Count == 0 || string.IsNullOrEmpty(sql))
                {
                    return;
                }
                var fields = GetSqlTableZiDuans(lj, sql);
                if (fields.Count == 0)
                {
                    return;
                }
                foreach (DataTable t in ds.Tables)
                {
                    DataTableEditDataGrid.AttachFieldMeta(t, fields);
                }
            }
            catch (Exception ex)
            {
                //只影响表头,不能因此把已经查到的数据挡掉
                Log.Error($"附结果表头字段信息失败:{ex.Message}");
            }
        }

        /// <summary>
        /// 取SQL里 FROM/JOIN 引用到的表的字段清单(顺序即 FROM/JOIN 顺序,同名列以先出现的表为准);
        /// 表清单还没加载出来时按表名直接查一次
        /// </summary>
        private List<ZiDuan> GetSqlTableZiDuans(LianJie lj, string sql)
        {
            var ret = new List<ZiDuan>();
            var biaoList = _biaoList;
            var loaded = biaoList != null && biaoList.Count > 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in TableAliasRegex.Matches(sql))
            {
                var name = CleanTableName(m.Groups[1].Value);
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                {
                    continue;
                }
                var b = loaded ? biaoList.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) : null;
                if (loaded && b == null)
                {
                    //上下文里没有这张表(子查询/CTE/临时表),跳过
                    continue;
                }
                var zd = b?.ZiDuanList != null && b.ZiDuanList.Count > 0 ? b.ZiDuanList : Db.GetZiDuans(b ?? new Biao { Name = name }, lj, true);
                if (zd != null && zd.Count > 0)
                {
                    ret.AddRange(zd);
                }
            }
            return ret;
        }

        #region 筛选列

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
        /// 全选/取消全选(勾选变化会经 Lie_Checked 同步到表格)
        /// </summary>
        private void SetAllLieVisible(bool visible)
        {
            if (_model?.LieList == null)
            {
                return;
            }
            foreach (var item in _model.LieList)
            {
                item.IsVisible = visible;
            }
            ApplyLieColumns();
        }

        private void Lie_Checked(object sender, RoutedEventArgs e)
        {
            ApplyLieColumns();
        }

        /// <summary>
        /// 把筛选列的勾选状态应用到当前所有结果表格
        /// </summary>
        private void ApplyLieColumns()
        {
            LieColumnHelper.Apply(_model?.LieList, LieColumnHelper.GetGrids(tabList));
        }

        #endregion

        #region 执行上下文(连接/数据库/模式)

        /// <summary>
        /// 工具栏上次的宽度(只有宽度真的变化才重新分配,避免内部调整反复触发)
        /// </summary>
        private double _toolBarWidth;

        /// <summary>
        /// 工具栏宽度变化:位置不够时优先隐藏菜单按钮,保留下拉(下拉是执行SQL的刚需)
        /// </summary>
        private void ToolBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var width = e.NewSize.Width;
            if (width <= 0 || Math.Abs(width - _toolBarWidth) < 1)
            {
                return;
            }
            _toolBarWidth = width;
            //延后到布局结束后再量,避免在布局过程中改可见性
            _ = Dispatcher.BeginInvoke(new Action(AdjustToolBar), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 按当前可用宽度分配工具栏:位置不够时从最后一个菜单项开始隐藏,
        /// 第一个[执行SQL]始终保留(编辑器右键菜单里也有执行入口)
        /// </summary>
        private void AdjustToolBar()
        {
            if (MenuToolBar == null || PanelContext == null)
            {
                return;
            }
            var items = MenuToolBar.Items.OfType<MenuItem>().ToList();
            if (items.Count == 0)
            {
                return;
            }
            //先把菜单项全部放出来,量一遍各自需要多宽
            var menuWidth = 0d;
            foreach (var item in items)
            {
                item.Visibility = Visibility.Visible;
                item.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                menuWidth += item.DesiredSize.Width;
            }
            //下拉区是刚需:先给它留够位置(还没布局时按经验值估算)
            var selectorsWidth = PanelContext.ActualWidth > 0 ? PanelContext.ActualWidth : 460;
            var available = _toolBarWidth - selectorsWidth - 12;
            if (menuWidth <= available)
            {
                return;
            }
            var width = menuWidth;
            for (var i = items.Count - 1; i >= 1 && width > available; i--)
            {
                width -= items[i].DesiredSize.Width;
                items[i].Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 初始化上下文下拉:按数据库类型决定显示哪几项
        /// </summary>
        private void InitContext()
        {
            var lj = _model?.Lj;
            if (_ctxInited || lj == null)
            {
                return;
            }
            _ctxInited = true;
            var list = _model.LjList;
            if (list == null || list.Count == 0)
            {
                //兜底:从配置读连接时同样只保留普通 SQL 连接(Redis/MongoDB 不在 SQL 查询页里选)
                list = (Config.GetLianjie() ?? new List<LianJie>())
                    .Where(p => string.IsNullOrEmpty(p.NoDbType)).ToList();
            }
            //用传入的上下文替换列表里的同名连接:右键某个库打开时上下文里带着该库,
            //不能被连接配置里的默认库覆盖(否则下拉会跳回默认库)
            list = new List<LianJie>(list);
            var index = list.FindIndex(p => p.Id == lj.Id);
            if (index >= 0)
            {
                list[index] = lj;
            }
            else
            {
                list.Add(lj);
            }
            CbLj.ItemsSource = list;
            _baseLj = lj;
            _ctxLoading = true;
            CbLj.SelectedItem = _baseLj;
            _ctxLoading = false;
            RefreshContext();
        }

        private void CbLj_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ctxLoading || !(CbLj.SelectedItem is LianJie lj))
            {
                return;
            }
            _baseLj = lj;
            RefreshContext();
        }

        private void CbDb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ctxLoading)
            {
                return;
            }
            RefreshSchema(CbDb.SelectedItem as string);
        }

        private void CbSchema_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ctxLoading)
            {
                return;
            }
            ApplyContext();
        }

        /// <summary>
        /// 按当前连接刷新数据库/模式下拉:
        /// 没有数据库层级的(Sqlite/Oracle/达梦等)隐藏数据库,没有模式层级的(MySql/Oracle/Sqlite等)隐藏模式
        /// </summary>
        private void RefreshContext()
        {
            var lj = _baseLj;
            if (lj == null)
            {
                return;
            }
            var noDb = !string.IsNullOrEmpty(lj.NoDbType);
            var hasDb = !noDb && Db.HasDatabaseLayer(lj.DataBaseType);
            var hasSchema = !noDb && Db.HasSchemaLayer(lj.DataBaseType);
            PanelDb.Visibility = hasDb ? Visibility.Visible : Visibility.Collapsed;
            PanelSchema.Visibility = hasSchema ? Visibility.Visible : Visibility.Collapsed;
            //下拉数量变了,工具栏可用宽度也变了,重新分配一次
            _ = Dispatcher.BeginInvoke(new Action(AdjustToolBar), System.Windows.Threading.DispatcherPriority.Loaded);
            if (!hasDb)
            {
                _ctxLoading = true;
                CbDb.ItemsSource = null;
                CbSchema.ItemsSource = null;
                _ctxLoading = false;
                ApplyContext();
                return;
            }
            SetLoading(true);
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var dbs = Db.GetDataBases(lj);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetLoading(false);
                        if (PanelDb.Visibility != Visibility.Visible)
                        {
                            return;
                        }
                        _ctxLoading = true;
                        CbDb.ItemsSource = dbs;
                        //优先选中打开该页时所在的库
                        CbDb.SelectedItem = dbs?.FirstOrDefault(p => p == lj.DataBaseName) ?? dbs?.FirstOrDefault();
                        _ctxLoading = false;
                        RefreshSchema(CbDb.SelectedItem as string);
                    }));
                }));
        }

        /// <summary>
        /// 加载指定库下的模式
        /// </summary>
        private void RefreshSchema(string database)
        {
            var lj = _baseLj;
            if (lj == null || PanelSchema.Visibility != Visibility.Visible || string.IsNullOrEmpty(database))
            {
                _ctxLoading = true;
                CbSchema.ItemsSource = null;
                _ctxLoading = false;
                ApplyContext();
                return;
            }
            var context = Db.CreateContext(lj, database);
            SetLoading(true);
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var schemas = Db.GetSchemas(context, database);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetLoading(false);
                        if (PanelSchema.Visibility != Visibility.Visible)
                        {
                            return;
                        }
                        _ctxLoading = true;
                        CbSchema.ItemsSource = schemas;
                        //优先选中打开该页时所在的模式
                        CbSchema.SelectedItem = schemas?.FirstOrDefault(p => p == lj.ServiceName) ?? schemas?.FirstOrDefault();
                        _ctxLoading = false;
                        ApplyContext();
                    }));
                }));
        }

        /// <summary>
        /// 用下拉的选择拼出执行SQL用的连接上下文
        /// </summary>
        private void ApplyContext()
        {
            if (_baseLj == null || _model == null)
            {
                return;
            }
            var db = PanelDb.Visibility == Visibility.Visible ? CbDb.SelectedItem as string : null;
            var schema = PanelSchema.Visibility == Visibility.Visible ? CbSchema.SelectedItem as string : null;
            _model.Lj = Db.CreateContext(_baseLj, db, schema);
            //上下文变了,重新加载表/视图供智能提示使用
            LoadBiaoList();
        }

        #endregion

        /// <summary>
        /// 显示/隐藏加载遮罩
        /// </summary>
        private void SetLoading(bool visible)
        {
            LoadingG.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 显示/隐藏底部数据区(新建查询默认只显示SQL编辑部分,执行SQL后才显示数据部分)
        /// </summary>
        private void SetDataAreaVisible(bool visible)
        {
            if (visible)
            {
                if (tc.Visibility == Visibility.Collapsed)
                {
                    RowSplitter.Height = new GridLength(3);
                    RowData.MinHeight = 200;
                    RowData.Height = new GridLength(1, GridUnitType.Star);
                }
            }
            else
            {
                RowSplitter.Height = new GridLength(0);
                RowData.MinHeight = 0;
                RowData.Height = new GridLength(0);
                SetPageBarVisible(false);
            }
            Splitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            tc.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

    }
}
