using HandyControl.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// <summary>
    /// MongoDB 查询框:上面是命令编辑区(带 MongoDB 智能提示),下面是结果表格(集合查询结果可在表格内修改保存)
    /// 结构与 SQL 编辑器页(TabView)保持一致:执行上下文下拉(连接/数据库)+ 结果页签 + 默认隐藏结果区
    /// </summary>
    public partial class TabMongoQueryView : UserControl
    {
        private readonly ObservableCollection<TabModel> _tabList = new ObservableCollection<TabModel>();

        private TabViewModel _model;
        private MongoCompletionProvider _provider;
        private CompletionController _completion;

        /// <summary>
        /// 正在后台加载字段的集合(避免重复查询)
        /// </summary>
        private readonly HashSet<string> _fieldLoading = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 正在程序化改动上下文下拉(避免把变更当成用户操作再触发一次查询)
        /// </summary>
        private bool _ctxLoading;

        private bool _inited;

        /// <summary>
        /// 本次执行是否走了分页(单条 find 命令且没自带 limit/skip)
        /// </summary>
        private bool _pageable;

        /// <summary>
        /// 分页用的查询主体(find 提示部分)与当时的库,执行时存好供翻页复用
        /// </summary>
        private string _pageHead;
        private string _pageDb;

        /// <summary>
        /// 正在查分页(避免连点翻页)
        /// </summary>
        private bool _pageLoading;

        /// <summary>
        /// 是否已订阅视图模型的属性变更
        /// </summary>
        private bool _pageHooked;

        public TabMongoQueryView()
        {
            InitializeComponent();
            tc.ItemsSource = _tabList;
            //新建查询默认只显示命令编辑区,执行后才显示结果部分(与 SQL 编辑器页一致)
            SetDataAreaVisible(false);
            //AI助手面板自己不认识连接/集合结构:由本页给上下文,并把生成的命令写回编辑器
            AiChat.TableLabel = "相关集合:";
            AiChat.CaptureContext = CaptureAiContext;
            AiChat.BuildAsk = BuildAiAsk;
            AiChat.SqlReady = WriteAiSql;
            AiChat.CloseRequested += CloseAiChat;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inited)
            {
                return;
            }
            _model = DataContext as TabViewModel;
            if (_model?.Lj == null)
            {
                return;
            }
            _inited = true;
            HookModel();
            HighlightingProvider.Register(SkinType.Dark, new HighlightingProviderDark());
            TextEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "Json");
            TextEditor.TextArea.SelectionCornerRadius = 0;
            TextEditor.TextArea.SelectionBorder = null;
            TextEditor.TextArea.SelectionForeground = null;
            TextEditor.TextArea.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFADD6FF"));

            _provider = new MongoCompletionProvider();
            _completion = new CompletionController(TextEditor)
            {
                Provider = word =>
                {
                    //光标在 db.集合. 后面时按需把该集合的字段拉回来(下次输入即可提示)
                    EnsureFieldsLoaded(word);
                    return _provider.GetCompletions(word);
                }
            };

            _ctxLoading = true;
            CbDb.Text = _model.Lj.DataBaseName ?? string.Empty;
            _ctxLoading = false;

            SetupConnections();
            TextEditor.Focus();
            LoadDatabases();
            if (!string.IsNullOrEmpty(CbDb.Text))
            {
                LoadCollections(CbDb.Text);
            }
        }

        /// <summary>
        /// 结果区显示/隐藏(与 SQL 编辑器页一致)
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
            }
            Splitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            tc.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
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
            LieColumnHelper.Apply(_model?.LieList, LieColumnHelper.GetGrids(_tabList));
        }

        #endregion

        #region 执行上下文(连接/数据库)

        /// <summary>
        /// 填充连接下拉(只列 MongoDB 连接,进入时选中当前连接)
        /// </summary>
        private void SetupConnections()
        {
            var list = _model.LjList != null && _model.LjList.Count > 0
                ? _model.LjList
                : new List<LianJie> { _model.Lj };
            CbLj.ItemsSource = list;
            CbLj.SelectedItem = list.FirstOrDefault(p => p.Id == _model.Lj?.Id) ?? list.FirstOrDefault();
        }

        private void CbLj_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ctxLoading || !_inited || !(CbLj.SelectedItem is LianJie lj))
            {
                return;
            }
            if (lj.Id == _model.Lj?.Id)
            {
                return;
            }
            //切换连接:库/集合/字段缓存全部失效,重新加载
            _model.Lj = lj;
            _provider.Databases = new List<string>();
            _provider.Collections = new List<string>();
            _provider.Fields.Clear();
            _fieldLoading.Clear();
            _ctxLoading = true;
            CbDb.ItemsSource = null;
            CbDb.Text = lj.DataBaseName ?? string.Empty;
            _ctxLoading = false;
            LoadDatabases();
            if (!string.IsNullOrEmpty(CbDb.Text))
            {
                LoadCollections(CbDb.Text);
            }
        }

        #endregion

        #region 智能提示的数据加载

        /// <summary>
        /// 加载服务器上的数据库列表
        /// </summary>
        private void LoadDatabases()
        {
            var lj = _model.Lj;
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var list = Db.GetMongo(lj);
                    if (list == null)
                    {
                        return;
                    }
                    Dispatcher.Invoke(new Action(() =>
                    {
                        _provider.Databases = list.Select(p => p.Name).ToList();
                        //List 不会通知界面,这里重新赋一次 ItemsSource
                        _ctxLoading = true;
                        CbDb.ItemsSource = null;
                        CbDb.ItemsSource = _provider.Databases;
                        //下拉不可编辑:只会显示选中项,光设 Text 不会自动匹配到列表项,所以要真的选中
                        //优先选中连接上配置的默认库,没有就用第一个库
                        if (_provider.Databases.Count > 0)
                        {
                            var defaultDb = lj.DataBaseName;
                            CbDb.SelectedItem = !string.IsNullOrEmpty(defaultDb) && _provider.Databases.Contains(defaultDb)
                                ? defaultDb
                                : _provider.Databases[0];
                        }
                        _ctxLoading = false;
                        if (!string.IsNullOrEmpty(CbDb.Text))
                        {
                            LoadCollections(CbDb.Text);
                        }
                    }));
                }));
        }

        /// <summary>
        /// 加载指定库的集合列表(智能提示用)
        /// </summary>
        private void LoadCollections(string db)
        {
            if (string.IsNullOrEmpty(db))
            {
                return;
            }
            var lj = Db.CreateContext(_model.Lj, db);
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var list = Db.GetMongoCollections(lj);
                    if (list == null)
                    {
                        return;
                    }
                    Dispatcher.Invoke(new Action(() =>
                    {
                        _provider.Collections = list.Select(p => p.Name).ToList();
                        //AI助手面板开着的话,把刚拿到的集合填进"相关集合"下拉(已勾选的会保住)
                        if (_aiOpen)
                        {
                            RefreshAiTables();
                        }
                    }));
                }));
        }

        /// <summary>
        /// 光标位于 db.集合. 之后时,后台取样该集合的字段
        /// </summary>
        private void EnsureFieldsLoaded(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return;
            }
            var lastDot = word.LastIndexOf('.');
            if (lastDot <= 3)
            {
                return;
            }
            var head = word.Substring(0, lastDot);
            if (!head.StartsWith("db.", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            var collection = head.Substring(3);
            if (collection.Length == 0 || collection.IndexOf('.') >= 0)
            {
                return;
            }
            if (_provider.Fields.ContainsKey(collection) || !_fieldLoading.Add(collection))
            {
                return;
            }
            var lj = Db.CreateContext(_model.Lj, (CbDb.Text ?? string.Empty).Trim());
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    try
                    {
                        var fields = Db.GetMongoCollectionFields(lj, collection);
                        Dispatcher.Invoke(new Action(() => _provider.Fields[collection] = fields));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                    }
                    finally
                    {
                        Dispatcher.Invoke(new Action(() => _fieldLoading.Remove(collection)));
                    }
                }));
        }

        #endregion

        #region 执行

        private void Zhixing_Click(object sender, RoutedEventArgs e)
        {
            var text = TextEditor.SelectedText?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                text = TextEditor.Text.Trim();
            }
            if (string.IsNullOrEmpty(text))
            {
                Msg.Warning("请输入要执行的 MongoDB 命令");
                return;
            }
            _completion?.Close();
            Zhixing(text);
        }

        #region AI助手

        /// <summary>AI助手面板是否已展开</summary>
        private bool _aiOpen;

        /// <summary>面板展开时的宽度(收起后记住,再次展开沿用)</summary>
        private double _aiWidth = 460;

        /// <summary>AI助手提前抓的快照:提问时的连接上下文(连接 + 当前库)</summary>
        private LianJie _aiLj;

        /// <summary>AI助手提前抓的快照:当前库名</summary>
        private string _aiDb;

        /// <summary>AI助手提前抓的快照:命令里 db.集合 引用到的集合名</summary>
        private List<string> _aiRefs;

        /// <summary>AI助手提前抓的快照:当前库的集合清单(问题里点名了集合时兜底用)</summary>
        private List<string> _aiCollections;

        /// <summary>匹配命令里的 db.集合</summary>
        private static readonly Regex MongoCollRegex = new Regex(@"\bdb\s*\.\s*([A-Za-z_][\w$]*)", RegexOptions.IgnoreCase);

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

        private void OpenAiChat()
        {
            _aiOpen = true;
            if (ColAi.ActualWidth > 0)
            {
                _aiWidth = ColAi.ActualWidth;
            }
            ColAi.MinWidth = 240;
            //页面窄的时候别让面板把编辑器挤没了
            ColAi.Width = new GridLength(ActualWidth > 0 ? Math.Min(_aiWidth, Math.Max(240, ActualWidth / 2)) : _aiWidth);
            ColAiSplit.Width = new GridLength(4);
            SplitterAi.Visibility = Visibility.Visible;
            AiChat.Visibility = Visibility.Visible;
            RefreshAiTables();
            AiChat.FocusInput();
        }

        /// <summary>把当前库的集合填进AI助手的"相关集合"下拉</summary>
        private void RefreshAiTables()
        {
            AiChat.SetTables(_provider == null ? new List<string>() : new List<string>(_provider.Collections));
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
            _aiDb = (CbDb.Text ?? string.Empty).Trim();
            _aiLj = _model?.Lj == null ? null : Db.CreateContext(_model.Lj, string.IsNullOrEmpty(_aiDb) ? null : _aiDb);
            _aiCollections = _provider == null ? new List<string>() : new List<string>(_provider.Collections);
            var refs = new List<string>();
            foreach (Match m in MongoCollRegex.Matches(TextEditor.Text ?? string.Empty))
            {
                var name = m.Groups[1].Value;
                if (!refs.Contains(name))
                {
                    refs.Add(name);
                }
            }
            _aiRefs = refs;
        }

        /// <summary>
        /// AI助手:按快照拼本次请求(普通对话与生成语句都会走这里)。
        /// 后台线程调用,所以这里只能读快照;系统提示词只在生成语句下生效
        /// </summary>
        private AiChatAsk BuildAiAsk(string need, List<string> tables)
        {
            var lj = _aiLj;
            return new AiChatAsk
            {
                System = AIHelp.MongoSystem,
                User = AIHelp.BuildQueryPrompt("MongoDB", string.IsNullOrEmpty(_aiDb) ? null : "库=" + _aiDb,
                    BuildAiSchemaText(lj, GetAiCollections(need, tables)), need)
            };
        }

        /// <summary>
        /// AI助手:这次要带哪些集合的结构:优先顶部勾选的集合,没勾选再用命令里写到的集合,
        /// 还没有就看问题里提到了当前库里的哪些集合
        /// </summary>
        private List<string> GetAiCollections(string need, List<string> tables)
        {
            var list = new List<string>(tables != null && tables.Count > 0 ? tables : (_aiRefs ?? new List<string>()));
            if (list.Count == 0 && _aiCollections != null && !string.IsNullOrEmpty(need))
            {
                //问题里点名的集合(如"查一下orders里本月未支付的")也带上,否则模型看不到字段只能靠猜
                foreach (var name in _aiCollections)
                {
                    if (need.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 && !list.Contains(name))
                    {
                        list.Add(name);
                    }
                    if (list.Count >= 5)
                    {
                        //带太多集合结构又慢又容易跑偏
                        break;
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// AI助手:拼紧凑集合结构,每个字段一行"名称 类型 -- 注释"
        /// </summary>
        private string BuildAiSchemaText(LianJie lj, List<string> names)
        {
            if (lj == null || names == null || names.Count == 0)
            {
                return null;
            }
            var sb = new StringBuilder();
            foreach (var name in names)
            {
                List<ZiDuan> ziDuans;
                try
                {
                    //字段是取样出来的,拿不到就只报集合名,不影响这次提问
                    ziDuans = Db.GetMongoCollectionZiDuans(lj, name) ?? new List<ZiDuan>();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    continue;
                }
                sb.Append("集合 ").Append(name).AppendLine();
                foreach (var z in ziDuans)
                {
                    sb.AppendLine(AIHelp.BuildZiDuanLine(z));
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// AI助手(生成语句):把AI回复里的命令插到编辑器光标处(与原来"AI回复已插入"同一口径)
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

        private void TextEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //Ctrl+Enter 执行
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                Zhixing_Click(sender, e);
            }
        }

        private void Ningcunwei_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "MongoDB 命令文件(*.js)|*.js|文本文件(*.txt)|*.txt|所有文件(*.*)|*.*",
                FileName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")
            };
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextEditor.Save(saveFileDialog.FileName);
            }
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

        private void Daoru_Click(object sender, RoutedEventArgs e)
        {
            var open = new System.Windows.Forms.OpenFileDialog
            {
                Filter = @"js文件(*.js)|*.js|sql文件(*.sql)|*.sql|所有文件(*.*)|*.*"
            };
            if (open.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var str = File.ReadAllText(open.FileName);
                _model.SqlStr += Environment.NewLine + str;
            }
        }

        /// <summary>
        /// 逐行执行命令(以 # 或 -- 开头的行当注释跳过)
        /// </summary>
        private void Zhixing(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n')
                .Select(p => p.Trim().TrimEnd(';').Trim())
                .Where(p => p.Length > 0 && !p.StartsWith("#") && !p.StartsWith("--"))
                .ToList();
            if (lines.Count == 0)
            {
                return;
            }
            var lj = _model.Lj;
            var currentDb = (CbDb.Text ?? string.Empty).Trim();
            //每次执行先收起分页条,成功后按需显示
            SetPageBarVisible(false);
            //单条 find 命令走分页:不再用固定条数一次性拉回,改成按页取(与 SQL 编辑器页一致);
            //多命令、自带 limit/skip 的仍按原样执行
            if (lines.Count == 1 && Db.TrySplitMongoForPage(lines[0], out var head))
            {
                _pageable = true;
                _pageHead = head;
                _pageDb = currentDb;
                _model.PageIndex = 1;
                QueryPage();
                return;
            }
            _pageable = false;
            RunMongo(lj, currentDb, lines);
        }

        /// <summary>
        /// 按原样执行命令(不分页的场景),逐条执行并汇总结果
        /// </summary>
        private void RunMongo(LianJie lj, string currentDb, List<string> lines)
        {
            //分页关掉后 find 默认仍要有个上限,避免一条命令把整个集合拉回来
            var limit = _model.PageSize > 0 ? _model.PageSize : 100;
            SetLoading(true);
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    //注意:不能把各条命令返回的 DataTable 再汇总进一个新的 DataSet:
                    //DataTable 已经属于命令结果自己的 DataSet,重复加入会抛"DataTable 已属于另一个 DataSet"
                    var tables = new List<DataTable>();
                    var messages = new List<string>();
                    try
                    {
                        foreach (var cmd in lines)
                        {
                            //use <库>:切换后续命令使用的库
                            if (cmd.StartsWith("use ", StringComparison.OrdinalIgnoreCase))
                            {
                                currentDb = cmd.Substring(4).Trim();
                                messages.Add($"已切换到数据库 [{currentDb}]");
                                continue;
                            }
                            var part = Db.ExecuteMongoSet(lj, currentDb, cmd, limit, out _, out var error);
                            if (!string.IsNullOrEmpty(error))
                            {
                                messages.Add($"(error) {error}");
                                continue;
                            }
                            foreach (DataTable t in part.Tables)
                            {
                                //直接收集(不能改 TableName:它是集合名,表格保存时要用它写回对应集合)
                                tables.Add(t);
                            }
                            if (part.Tables.Count == 0)
                            {
                                messages.Add($"{cmd} 执行完成");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //后台异常必须兜住:否则下面的 Dispatcher.Invoke 不会执行,加载遮罩会一直转
                        Log.Error($"执行MongoDB命令失败:{ex}");
                        messages.Add($"(error) {ex.Message}");
                    }
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetLoading(false);
                        try
                        {
                            if (currentDb != (CbDb.Text ?? string.Empty).Trim())
                            {
                                _ctxLoading = true;
                                CbDb.Text = currentDb;
                                _ctxLoading = false;
                                LoadCollections(currentDb);
                            }
                            ShowResult(lj, currentDb, tables, messages);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"展示MongoDB执行结果失败:{ex}");
                            Msg.Warning(ex.Message);
                        }
                    }));
                }));
        }

        /// <summary>
        /// 查询当前页(每页多取一条,用于判断是否还有下一页)
        /// </summary>
        private void QueryPage()
        {
            var lj = _model?.Lj;
            if (lj == null || string.IsNullOrEmpty(_pageHead) || _pageLoading)
            {
                return;
            }
            var pageSize = _model.PageSize > 0 ? _model.PageSize : 100;
            var index = _model.PageIndex < 1 ? 1 : _model.PageIndex;
            var offset = (index - 1) * pageSize;
            //复用命令执行:在原查询主体后补 .skip()/.limit(),skip/limit 会被照常解析执行
            var cmd = $"{_pageHead}.skip({offset}).limit({pageSize + 1})";
            var db = _pageDb;
            _pageLoading = true;
            SetLoading(true);
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    DataSet ds = null;
                    string error = null;
                    try
                    {
                        ds = Db.ExecuteMongoSet(lj, db, cmd, pageSize + 1, out _, out error);
                    }
                    catch (Exception ex)
                    {
                        //后台异常必须兜住:否则下面的 Dispatcher.Invoke 不会执行,加载遮罩会一直转
                        Log.Error($"分页查询MongoDB失败:{ex}");
                        error = ex.Message;
                    }
                    Dispatcher.Invoke(new Action(() =>
                    {
                        _pageLoading = false;
                        SetLoading(false);
                        if (!string.IsNullOrEmpty(error))
                        {
                            Msg.Warning(error);
                            return;
                        }
                        var dt = ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : null;
                        if (dt == null)
                        {
                            return;
                        }
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
                        SetPageBarVisible(true);
                        //TableName 是集合名(表格保存时要写回对应集合),这里不能改
                        ShowResult(lj, db, new List<DataTable> { dt }, new List<string>());
                    }));
                }));
        }

        /// <summary>
        /// 展示执行结果:集合查询用表格(只读),其它命令用只读文本
        /// </summary>
        private void ShowResult(LianJie lj, string db, List<DataTable> tables, List<string> messages)
        {
            _tabList.Clear();
            var context = string.IsNullOrEmpty(db) ? lj : Db.CreateContext(lj, db);
            foreach (var t in tables)
            {
                if (t.ExtendedProperties.Contains(MongoDataGridEditor.DocMapKey))
                {
                    //集合文档结果:与其它查询画面一致,结果一律只读(改数据走左侧集合节点【查看数据】);
                    //MongoEditor 仍注入,供右键"创建SQL/复制列名"按 MongoDB 口径生成命令
                    var grid = new DataTableEditDataGridView
                    {
                        DataContext = new DataTableEditDataGridViewModel
                        {
                            Lj = context,
                            Dt = t,
                            ReadOnly = true,
                            MongoEditor = new MongoDataGridEditor { Lj = context, Collection = t.TableName }
                        }
                    };
                    _tabList.Add(new TabModel { Header = BuildTabHeader($"{t.TableName} ({t.Rows.Count})"), Content = grid });
                }
                else
                {
                    _tabList.Add(new TabModel { Header = BuildTabHeader(t.TableName), Content = BuildTextView(t) });
                }
            }
            if (messages.Count > 0)
            {
                _tabList.Insert(0, new TabModel { Header = "消息", Content = BuildMessageView(messages) });
            }
            if (_tabList.Count > 0)
            {
                SetDataAreaVisible(true);
                tc.SelectedIndex = 0;
            }
            //筛选列:按结果集列补齐勾选项,并把已有勾选状态应用到新表格
            LieColumnHelper.EnsureItems(_model?.LieList, tables);
            ApplyLieColumns();
        }

        /// <summary>
        /// 多条命令返回同名结果集时给页签名加序号(只影响页签显示,不改 DataTable 名)
        /// </summary>
        private string BuildTabHeader(string baseHeader)
        {
            var header = baseHeader;
            var index = 2;
            while (_tabList.Any(p => p.Header == header))
            {
                header = $"{baseHeader} ({index++})";
            }
            return header;
        }

        /// <summary>
        /// 单列结果集拼成只读文本视图
        /// </summary>
        private static TextBox BuildTextView(DataTable dt)
        {
            var lines = dt.Rows.Cast<DataRow>().Select(p => p[0]?.ToString() ?? string.Empty);
            return BuildMessageView(lines.ToList());
        }

        private static TextBox BuildMessageView(List<string> lines)
        {
            return new TextBox
            {
                Text = string.Join(Environment.NewLine, lines),
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Padding = new Thickness(5),
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap
            };
        }

        private void SetLoading(bool visible)
        {
            LoadingG.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
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
        /// 翻页:按本次执行已存好的查询主体重查(编辑器里改了命令要重新执行才对翻页生效)
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

        #endregion

        #region 数据库下拉与编辑器右键

        private void CbDb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ctxLoading || !_inited)
            {
                return;
            }
            //非可编辑 ComboBox 在 SelectionChanged 里 Text 还是上一个库,这里必须取 SelectedItem
            LoadCollections((CbDb.SelectedItem as string) ?? string.Empty);
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

        #endregion
    }
}
