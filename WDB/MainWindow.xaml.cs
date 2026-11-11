using ICSharpCode.AvalonEdit.Editing;
using NetFrameWork.Database;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WDB.Common;
using WDB.Entity;
using WDB.View;
using WDB.Window;
using WDB.Window.Tools;

namespace WDB
{
    public partial class MainWindow
    {
        private MainWindowModel _mainWindowModel;

        private List<LianJie> LianJieList;

        /// <summary>
        /// 连接树的根节点集合(持有懒加载状态,不随过滤重建)
        /// </summary>
        private readonly List<DbNode> _rootNodes = new List<DbNode>();

        private readonly ObservableCollection<TabModel> tabList = new ObservableCollection<TabModel>();

        /// <summary>
        /// 首页视图(激活/过期后需同步刷新其上的授权状态文案)
        /// </summary>
        private WelcomeView _welcomeView;

        /// <summary>
        /// 待滚动的树节点:展开会异步加载子节点并重建绑定(滚动位置被重置到顶部),加载完成后按它再滚一次
        /// </summary>
        private DbNode _pendingRevealNode;


        public MainWindow()
        {
            StartupTrace.Mark("MainWindow 构造开始");
            InitializeComponent();
            StartupTrace.Mark("MainWindow.xaml 解析完成(InitializeComponent)");
            ORM.LogFolderPath = Path.Combine(Core.GetAppPath, "logs");
            Log.Info("Startup successful");
            _mainWindowModel = (MainWindowModel)DataContext;
            StartupTrace.Mark("DataContext 获取完成");
            if (Config.W > 0)
            {
                Left = Config.X;
                Top = Config.Y;
                Width = Config.W;
                Height = Config.H;
            }
            WindowState = Config.IsMax ? WindowState.Maximized : WindowState.Normal;
            StaticVar.TextFontSize = Config.TextFontSize == 0 ? 16 : Config.TextFontSize;
            StartupTrace.Mark("读取配置文件(窗口位置/字号)完成");
            tvLianjie.AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(tvLianjie_PreviewMouseDown), true);
            tvLianjie.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(tvLianjie_Expanded));
            Keyboard.AddPreviewKeyDownHandler(this, (s, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {

                    if (e.Key == Key.C)
                    {
                        IInputElement t = FocusManager.GetFocusedElement(this);
                        if (t is TextBox tb)
                        {
                            SafeClipboard.SetText(tb.SelectedText);
                            e.Handled = true;
                        }
                        else if (t is RichTextBox rtb)
                        {
                            SafeClipboard.SetText(rtb.Selection.Text);
                            e.Handled = true;
                        }
                        else if (t is TextArea ta)
                        {
                            if (!ta.Selection.IsEmpty)
                            {
                                SafeClipboard.SetText(ta.Selection.GetText());
                            }
                            e.Handled = true;
                        }
                        else if (t is PasswordBox pb)
                        {
                            SafeClipboard.SetText(pb.Password);
                            e.Handled = true;
                        }
                        else if (t is TreeViewItem tvi)
                        {
                            if (tvi.Header is LianJie l)
                            {
                                SafeClipboard.SetText(l.Name);
                            }
                            else
                            {
                                SafeClipboard.SetText(tvi.Header?.ToString());
                            }
                            e.Handled = true;
                        }
                        if (t is DataGridCell cell)
                        {
                            var text = UIHelp.GetSelectedCellsPrecise(UIHelp.GetOwnerDataGrid(cell));
                            SafeClipboard.SetText(text);
                            e.Handled = true;
                        }
                    }
                    if (e.Key == Key.X)
                    {
                        var t = FocusManager.GetFocusedElement(this);
                        if (t is TextBox tb)
                        {
                            SafeClipboard.SetText(tb.SelectedText);
                            tb.SelectedText = "";
                            e.Handled = true;
                        }
                        else if (t is RichTextBox rtb)
                        {
                            SafeClipboard.SetText(rtb.Selection.Text);
                            rtb.Selection.Text = "";
                            e.Handled = true;
                        }
                        else if (t is TextArea ta)
                        {
                            if (!ta.Selection.IsEmpty)
                            {
                                string text = ta.Selection.GetText();
                                SafeClipboard.SetText(text);
                                ta.Document.Replace(ta.Selection.SurroundingSegment, "");
                            }
                            e.Handled = true;
                        }
                    }
                }
            });
            StartupTrace.Mark("MainWindow 构造完成");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            StartupTrace.Mark("Window_Loaded 开始");
            var reg = RegHelp.Reg;
            StartupTrace.Mark("注册状态检查(RegHelp.Reg/机器码)完成");
            RefreshTitle();
            if (reg.Jhzt == 2)
            {
                Msg.Warning("当前软件已过期,如需继续使用请注册!");
                new GuanYu { Owner = this }.ShowDialog();
                //激活/关闭后同步标题状态
                RefreshTitle();
            }
            LianJieList = Config.GetLianjie();
            if (LianJieList == null)
            {
                LianJieList = new List<LianJie>();
            }
            InitNodes();
            Shuaxin(9);
            StartupTrace.Mark("连接列表加载与树绑定完成");
            foreach (var item in BuildNewConnectionItems())
            {
                XinJian.Items.Add(item);
            }
            StartupTrace.Mark("新建连接菜单已构建");
            ShowWelcomeTab();
            StartupTrace.Mark("首页标签已打开");
            StartupTrace.Mark("Window_Loaded 完成");
        }

        /// <summary>
        /// 构建"新建连接"菜单项(顶部菜单与首页快捷入口共用)
        /// </summary>
        private List<UIElement> BuildNewConnectionItems()
        {
            var items = new List<UIElement>();
            //构建"URL / JDBC 自动识别"快捷入口
            var itemAuto = new MenuItem()
            {
                Header = "URL / JDBC 自动识别",
                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/connect/link.png", 16, 16)
            };
            itemAuto.Click += (_, rea) =>
            {
                var f = new XinJianByUrl { Owner = this };
                if (f.ShowDialog() == true)
                {
                    OpenNewConnection(f.ParsedLianJie);
                }
            };
            items.Add(itemAuto);
            //自动识别入口与手工入口之间加分隔线
            items.Add(new Separator { Margin = new Thickness(0, 0, 10, 0) });
            //构建Redis新建连接
            var itemRedis = new MenuItem()
            {
                Header = "Redis",
                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/connect/redis.png", 16, 16)
            };
            itemRedis.Click += (_, rea) =>
            {
                var f = new XinJianNoDb(new LianJie { NoDbType = "Redis", IsFilter = true });
                f.Owner = this;
                if (f.ShowDialog() == true)
                {
                    Shuaxin(0, f._lj);
                }
            };
            items.Add(itemRedis);
            //构建MongoDB新建连接
            var itemMongo = new MenuItem()
            {
                Header = "MongoDB",
                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/connect/mongodb.png", 16, 16)
            };
            itemMongo.Click += (_, rea) =>
            {
                var f = new XinJianNoDb(new LianJie { NoDbType = "MongoDB", IsFilter = true });
                f.Owner = this;
                if (f.ShowDialog() == true)
                {
                    Shuaxin(0, f._lj);
                }
            };
            items.Add(itemMongo);
            items.Add(new Separator
            {
                Margin = new Thickness(0, 0, 20, 0)
            });

            //构建数据库新建连接
            foreach (ORM.DbTypeEnum type in Enum.GetValues(typeof(ORM.DbTypeEnum)))
            {
                var t = type.ToString();
                var item = new MenuItem()
                {
                    Header = t,
                    Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/connect/{t.ToLower()}.png", 16, 16)
                };
                item.Click += (_, rea) =>
                {
                    if (type == ORM.DbTypeEnum.SqlIte)
                    {
                        var f = new XinJianLocal(new LianJie { DataBaseType = type, IsFilter = true });
                        f.Owner = this;
                        if (f.ShowDialog() == true)
                        {
                            Shuaxin(0, f._lj);
                        }
                    }
                    else
                    {
                        var f = new XinJian(new LianJie { DataBaseType = type, IsFilter = true });
                        f.Owner = this;
                        if (f.ShowDialog() == true)
                        {
                            Shuaxin(0, f._lj);
                        }
                    }
                };
                items.Add(item);
            }
            return items;
        }

        /// <summary>
        /// 按连接类型打开对应的新建窗口,确认后落库(URL / JDBC 自动识别入口使用)
        /// </summary>
        private void OpenNewConnection(LianJie lj)
        {
            if (lj == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(lj.NoDbType))
            {
                var f = new XinJianNoDb(lj) { Owner = this };
                if (f.ShowDialog() == true)
                {
                    Shuaxin(0, f._lj);
                }
            }
            else if (lj.DataBaseType == ORM.DbTypeEnum.SqlIte)
            {
                var f = new XinJianLocal(lj) { Owner = this };
                if (f.ShowDialog() == true)
                {
                    Shuaxin(0, f._lj);
                }
            }
            else
            {
                var f = new XinJian(lj) { Owner = this };
                if (f.ShowDialog() == true)
                {
                    Shuaxin(0, f._lj);
                }
            }
        }

        /// <summary>
        /// 首帧渲染完成(WPF 至此才真正把主窗口画到屏幕上)
        /// </summary>
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            StartupTrace.Mark("主窗口首帧渲染完成");
            StartupTrace.Flush();
        }

        /// <summary>
        /// 筛选搜索
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Shuaxin(9);
        }

        /// <summary>
        /// 刷新连接列表(0:新增 1:修改 2:删除 3:空保存 9:初始加载)
        /// </summary>
        /// <param name="type">0:新增 1:修改 2:删除 3:空保存 9:初始加载</param>
        /// <param name="lianJie">连接数据</param>
        public void Shuaxin(int type, LianJie lianJie = null)
        {
            if (type != 9)
            {
                if (type == 0)
                {
                    if (RegCheck(LianJieList.Count))
                    {
                        return;
                    }
                    lianJie.Id = StrHelp.Id;
                    LianJieList.Add(lianJie);
                    _rootNodes.Add(BuildConnectionNode(lianJie));
                }
                else if (type == 1)
                {
                    for (var i = 0; i < LianJieList.Count; i++)
                    {
                        if (LianJieList[i].Id == lianJie.Id)
                        {
                            LianJieList[i] = lianJie;
                            break;
                        }
                    }
                    //连接配置变更:丢弃该连接已缓存的节点树
                    var index = _rootNodes.FindIndex(p => p.LianJie != null && p.LianJie.Id == lianJie.Id);
                    if (index >= 0)
                    {
                        _rootNodes[index] = BuildConnectionNode(lianJie);
                    }
                }
                else if (type == 2)
                {
                    LianJieList.RemoveAll(p => p.Id == lianJie.Id);
                    _rootNodes.RemoveAll(p => p.LianJie != null && p.LianJie.Id == lianJie.Id);
                }
                Config.SetLianjie(LianJieList);
            }
            //首页连接列表跟随实际连接变化(新增/修改/删除/导入);9为搜索/初始化,连接本身未变
            if (type != 9)
            {
                _welcomeView?.Load(LianJieList);
            }
            RebuildBindings();
        }

        #region 连接树节点构建与懒加载

        private const string IconRoot = "pack://application:,,,/WDB;component/Resources/";

        /// <summary>
        /// 根据连接配置初始化全部连接节点
        /// </summary>
        private void InitNodes()
        {
            _rootNodes.Clear();
            foreach (var l in LianJieList)
            {
                _rootNodes.Add(BuildConnectionNode(l));
            }
        }

        /// <summary>
        /// 构建连接(服务器)节点
        /// </summary>
        private DbNode BuildConnectionNode(LianJie l)
        {
            var icon = string.IsNullOrEmpty(l.NoDbType)
                ? $"{IconRoot}connect/{l.DataBaseType.ToString().ToLower()}.png"
                : $"{IconRoot}connect/{l.NoDbType.ToLower()}.png";
            var typeName = string.IsNullOrEmpty(l.NoDbType) ? l.DataBaseType.ToString() : l.NoDbType;
            return WithPlaceHolder(new DbNode
            {
                NodeType = DbNodeType.Connection,
                Name = l.Name,
                Comment = l.Host,
                Icon = icon,
                IsBold = true,
                ToolTipText = string.Join(Environment.NewLine,
                    $"连接名: {l.Name}",
                    $"类型: {typeName}",
                    $"服务: {l.Host}:{l.Port}",
                    $"初始数据库: {l.DataBaseName}",
                    $"模式: {l.ServiceName}",
                    $"用户: {l.UserName}"),
                LianJie = l,
                RootLianJie = l
            });
        }

        /// <summary>
        /// 构建数据库/模式容器节点
        /// </summary>
        private DbNode BuildContainerNode(DbNodeType nodeType, string name, LianJie context)
        {
            return WithPlaceHolder(new DbNode
            {
                NodeType = nodeType,
                Name = name,
                Icon = nodeType == DbNodeType.Database ? IconRoot + "database.png" : IconRoot + "schema.png",
                IsBold = true,
                DatabaseName = context.DataBaseName,
                SchemaName = nodeType == DbNodeType.Schema ? name : null,
                LianJie = context
            });
        }

        /// <summary>
        /// 构建表/视图节点
        /// </summary>
        private DbNode BuildTableNode(Biao b, LianJie context)
        {
            return new DbNode
            {
                NodeType = b.IsView ? DbNodeType.View : DbNodeType.Table,
                Name = b.Name,
                Comment = b.Comment,
                Icon = b.IsView ? IconRoot + "view.png" : IconRoot + "table.png",
                Biao = b,
                LianJie = context
            };
        }

        private List<DbNode> BuildTableNodes(List<Biao> biaoList, LianJie context)
        {
            if (biaoList == null)
            {
                return null;
            }
            //表在前、视图在后,同类内按名称排序:避免同一层级下表与视图混排
            return biaoList
                .OrderBy(p => p.IsView ? 1 : 0)
                .ThenBy(p => p.Name)
                .Select(b => BuildTableNode(b, context))
                .ToList();
        }

        /// <summary>
        /// 未加载的容器节点放置占位子节点(保证可展开并触发加载)
        /// </summary>
        private DbNode WithPlaceHolder(DbNode node)
        {
            node.IsLoaded = false;
            //重新加载:清掉上次的失败标记(右键「刷新」/重建节点走这里)
            node.LoadFailed = false;
            node.AllChildren = new List<DbNode>
            {
                new DbNode { NodeType = DbNodeType.Loading, Name = "加载中...", Parent = node }
            };
            node.Children = node.AllChildren;
            return node;
        }

        /// <summary>
        /// 树加载失败:错误信息直接挂在节点下面(红字占位子节点),不弹窗、不遮罩;
        /// 节点保持"未加载"并置失败标记,重复展开/双击不再自动重试(右键「刷新」可重试)
        /// </summary>
        private void SetNodeLoadFailed(DbNode node, string message)
        {
            node.IsLoaded = false;
            node.LoadFailed = true;
            node.AllChildren = new List<DbNode>
            {
                new DbNode { NodeType = DbNodeType.Loading, Name = message, IsError = true, Parent = node }
            };
            node.Children = node.AllChildren;
            RebuildBindings();
        }

        private void SetChildren(DbNode node, List<DbNode> children)
        {
            if (children != null)
            {
                foreach (var c in children)
                {
                    c.Parent = node;
                    //继承上下文:便于子节点定位所属数据库/模式以及根连接配置
                    c.RootLianJie = node.RootLianJie;
                    if (string.IsNullOrEmpty(c.DatabaseName))
                    {
                        c.DatabaseName = node.DatabaseName;
                    }
                    if (string.IsNullOrEmpty(c.SchemaName))
                    {
                        c.SchemaName = node.SchemaName;
                    }
                }
                node.AllChildren = children;
                node.Children = children;
            }
            else
            {
                node.AllChildren = new List<DbNode>();
                node.Children = node.AllChildren;
            }
            node.IsLoaded = true;
            node.LoadFailed = false;
        }

        /// <summary>
        /// 节点展开时加载子节点
        /// </summary>
        private void tvLianjie_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item && item.DataContext is DbNode node)
            {
                LoadNodeChildren(node);
            }
        }

        /// <summary>
        /// 加载容器节点的子节点(连接→数据库,数据库→模式/表,模式→表)
        /// </summary>
        private void LoadNodeChildren(DbNode node)
        {
            if (node == null || node.IsLoaded || node.IsLoading)
            {
                return;
            }
            if (node.NodeType != DbNodeType.Connection && node.NodeType != DbNodeType.Database && node.NodeType != DbNodeType.Schema && node.NodeType != DbNodeType.MongoDb)
            {
                return;
            }
            if (node.LoadFailed)
            {
                //失败后不再自动重试:重复展开/双击不重新请求,也不重复弹窗(错误已挂在节点下)
                return;
            }
            node.IsLoading = true;
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var children = QueryChildren(node, out string error);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        node.IsLoading = false;
                        if (children == null)
                        {
                            //失败:错误直接挂在节点下面(红字);弹窗交给数据层,避免同一错误弹两次
                            var reason = string.IsNullOrWhiteSpace(error) ? "加载失败" : error;
                            SetNodeLoadFailed(node, reason + ",右键「刷新」重试");
                            return;
                        }
                        SetChildren(node, children);
                        //当前上下文的表列表供SQL编辑器自动补全使用
                        if (node.NodeType != DbNodeType.Connection && node.LianJie != null)
                        {
                            node.LianJie.BiaoList = children?
                                .Where(p => p.NodeType == DbNodeType.Table || p.NodeType == DbNodeType.View)
                                .Select(p => p.Biao).ToList();
                        }
                        node.IsExpanded = true;
                        RebuildBindings();
                        //重建绑定会把滚动位置重置到顶部:若这正是一次定位请求,加载完后再滚一次
                        if (ReferenceEquals(_pendingRevealNode, node))
                        {
                            RevealTreeNodeLater(node);
                        }
                    }));
                }));
        }

        /// <summary>
        /// 是否 MongoDB 连接
        /// </summary>
        private static bool IsMongo(LianJie l)
        {
            return l != null && "MongoDB".Equals(l.NoDbType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 查询 MongoDB 节点的子节点(连接→数据库,数据库→集合)
        /// </summary>
        private List<DbNode> QueryMongoChildren(DbNode node, LianJie l)
        {
            if (node.NodeType == DbNodeType.Connection)
            {
                var dbs = Db.GetMongo(l);
                if (dbs == null)
                {
                    return null;
                }
                return dbs.Select(b => new DbNode
                {
                    NodeType = DbNodeType.MongoDb,
                    Name = b.Name,
                    Comment = b.Comment,
                    Icon = IconRoot + "database.png",
                    IsBold = true,
                    Biao = b,
                    DatabaseName = b.Name,
                    LianJie = Db.CreateContext(l, b.Name)
                }).ToList();
            }
            //数据库节点:列出集合(集合复用表节点,双击后按 NoDb 连接走文档浏览)
            return BuildTableNodes(Db.GetMongoCollections(l), l);
        }

        /// <summary>
        /// 查询节点的子节点(后台线程执行)。
        /// 返回 null 表示失败,失败原因由 error 带出(供树上红字提示);
        /// 返回空列表表示成功但下面没有东西
        /// </summary>
        private List<DbNode> QueryChildren(DbNode node, out string error)
        {
            error = null;
            try
            {
                var l = node.LianJie;
                if (l == null)
                {
                    error = "连接信息缺失,无法加载";
                    return null;
                }
                //无库类型连接(Redis/MongoDB 等)
                if (!string.IsNullOrEmpty(l.NoDbType))
                {
                    //MongoDB:连接→数据库→集合
                    if (IsMongo(l))
                    {
                        var mongo = QueryMongoChildren(node, l);
                        if (mongo == null)
                        {
                            error = node.NodeType == DbNodeType.Connection ? "读取数据库列表失败" : "读取集合列表失败";
                        }
                        return mongo;
                    }
                    //Redis:列出全部逻辑库
                    var redisList = Db.GetNoDb(l);
                    if (redisList == null)
                    {
                        error = "读取逻辑库列表失败";
                        return null;
                    }
                    return redisList.Select(b => new DbNode
                    {
                        NodeType = DbNodeType.RedisDb,
                        Name = b.Name,
                        Comment = b.Comment,
                        Icon = IconRoot + "database.png",
                        IsBold = true,
                        Biao = b,
                        LianJie = l
                    }).ToList();
                }
                switch (node.NodeType)
                {
                    case DbNodeType.Connection:
                        //服务器级连接:列出该实例上的全部数据库
                        if (Db.HasDatabaseLayer(l.DataBaseType))
                        {
                            var dbNames = Db.GetDataBases(l);
                            if (dbNames == null)
                            {
                                error = "读取数据库列表失败";
                                return null;
                            }
                            return dbNames.Select(name => BuildContainerNode(DbNodeType.Database, name, Db.CreateContext(l, name))).ToList();
                        }
                        //不支持数据库层级的类型(Oracle/Sqlite等)直接列出表
                        return BuildTableNodesChecked(Db.GetTables(l), l, out error);
                    case DbNodeType.Database:
                        //存在模式层级的库先列出模式,否则直接列出表
                        if (Db.HasSchemaLayer(l.DataBaseType))
                        {
                            var schemaNames = Db.GetSchemas(l, node.DatabaseName);
                            if (schemaNames == null)
                            {
                                error = "读取模式列表失败";
                                return null;
                            }
                            return schemaNames.Select(name => BuildContainerNode(DbNodeType.Schema, name, Db.CreateContext(l, node.DatabaseName, name))).ToList();
                        }
                        return BuildTableNodesChecked(Db.GetTables(node.LianJie), node.LianJie, out error);
                    case DbNodeType.Schema:
                        return BuildTableNodesChecked(Db.GetTablesBySchema(node.LianJie, node.SchemaName), node.LianJie, out error);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                error = ex.Message;
            }
            return null;
        }

        /// <summary>
        /// 构建表节点并区分"查询失败"与"确实没有表":前者带出错误信息,后者返回空列表
        /// </summary>
        private List<DbNode> BuildTableNodesChecked(List<Biao> biaoList, LianJie context, out string error)
        {
            error = biaoList == null ? "读取表列表失败" : null;
            return BuildTableNodes(biaoList, context);
        }

        /// <summary>
        /// 重新加载节点(丢弃已缓存的子节点)
        /// </summary>
        private void ReloadNode(DbNode node)
        {
            if (node == null)
            {
                return;
            }
            node.IsLoaded = false;
            WithPlaceHolder(node);
            RebuildBindings();
            LoadNodeChildren(node);
        }

        /// <summary>
        /// 重新加载父节点(表结构变动后刷新所在的表列表)
        /// </summary>
        private void ReloadParent(DbNode node)
        {
            if (node?.Parent != null)
            {
                ReloadNode(node.Parent);
            }
        }

        /// <summary>
        /// 在服务器上创建数据库
        /// </summary>
        private void CreateDataBase(DbNode node)
        {
            var l = node.LianJie;
            if (!Db.CanCreateDataBase(l.DataBaseType))
            {
                Msg.Warning($"{l.DataBaseType} 暂不支持创建数据库");
                return;
            }
            var f = new XinJianShuJuKu(l)
            {
                Owner = this
            };
            if (f.ShowDialog() != true)
            {
                return;
            }
            var name = f.DbName;
            LoadingG.Visibility = Visibility.Visible;
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var ok = Db.CreateDataBase(l, name, out string error, f.CharSet, f.Collate, f.OwnerName);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        LoadingG.Visibility = Visibility.Collapsed;
                        if (ok)
                        {
                            Oops.Ok($"[{name}]创建成功");
                            ReloadNode(node);
                        }
                        else
                        {
                            Msg.Warning(error);
                        }
                    }));
                }));
        }

        /// <summary>
        /// 在当前数据库下新建模式
        /// </summary>
        private void CreateSchema(DbNode node)
        {
            var l = node.LianJie;
            if (!Db.CanCreateSchema(l.DataBaseType))
            {
                Msg.Warning($"{l.DataBaseType} 暂不支持新建模式");
                return;
            }
            var f = new ShuRu("新建模式", $"在数据库[{node.Name}]下新建模式,请输入模式名:")
            {
                Owner = this
            };
            if (f.ShowDialog() != true)
            {
                return;
            }
            var name = f.Result;
            //建在当前库的连接上下文上(CREATE SCHEMA 作用于整个库,与 SearchPath 无关)
            var lj = Db.CreateContext(l, l.DataBaseName);
            LoadingG.Visibility = Visibility.Visible;
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var ok = Db.CreateSchema(lj, name, out string error);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        LoadingG.Visibility = Visibility.Collapsed;
                        if (ok)
                        {
                            Oops.Ok($"[{name}]创建成功");
                            ReloadNode(node);
                        }
                        else
                        {
                            Msg.Warning(error);
                        }
                    }));
                }));
        }

        /// <summary>
        /// 删除数据库/模式(删除成功后刷新父节点的子节点列表)
        /// </summary>
        /// <param name="node">数据库或模式节点</param>
        /// <param name="isDatabase">true 为删除数据库,false 为删除模式</param>
        private void DropDataBaseOrSchema(DbNode node, bool isDatabase)
        {
            var rootLj = node.RootLianJie ?? node.LianJie;
            //删除数据库:用根连接(连的是配置的初始库),不能连被删的库;
            //删除模式:切到所在库的连接上下文,模式内还有对象时保留原生报错
            var lj = isDatabase ? rootLj : Db.CreateContext(rootLj, node.DatabaseName);
            var name = isDatabase ? node.Name : node.SchemaName ?? node.Name;
            LoadingG.Visibility = Visibility.Visible;
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var error = string.Empty;
                    var ok = false;
                    if (isDatabase)
                    {
                        ok = Db.DropDataBase(lj, name, out error);
                    }
                    else
                    {
                        ok = Db.DropSchema(lj, name, out error);
                    }
                    Dispatcher.Invoke(new Action(() =>
                    {
                        LoadingG.Visibility = Visibility.Collapsed;
                        if (ok)
                        {
                            Oops.Ok($"[{name}]删除成功");
                            ReloadParent(node);
                        }
                        else
                        {
                            Msg.Warning(error);
                        }
                    }));
                }));
        }

        /// <summary>
        /// 在当前数据库/模式下新建表(可视化编辑字段)
        /// </summary>
        private void CreateTable(DbNode node)
        {
            var l = node.LianJie;
            OpenBiaoBianJiTab(l, new Biao(), true, () => ReloadAfterCreateTable(node, l));
        }

        /// <summary>
        /// 新建表成功后刷新节点:含模式层级的库,新建的表落在默认模式下,需刷新该模式的子节点
        /// </summary>
        private void ReloadAfterCreateTable(DbNode node, LianJie l)
        {
            if (node.NodeType == DbNodeType.Database && Db.HasSchemaLayer(l.DataBaseType))
            {
                var schemaNode = node.AllChildren?.FirstOrDefault(p => p.NodeType == DbNodeType.Schema && p.Name == l.ServiceName);
                ReloadNode(schemaNode ?? node);
            }
            else
            {
                ReloadNode(node);
            }
        }

        /// <summary>
        /// 可视化修改表结构
        /// </summary>
        private void EditBiaoJieGou(DbNode node)
        {
            var lj = node.LianJie;
            var b = node.Biao;
            LoadingG.Visibility = Visibility.Visible;
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    b.ZiDuanList = Db.GetZiDuans(b, lj);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        LoadingG.Visibility = Visibility.Collapsed;
                        OpenBiaoBianJiTab(lj, b, false, () => ReloadParent(node));
                    }));
                }));
        }

        #endregion

        #region 导入导出SQL

        /// <summary>
        /// 收集节点范围内可导出的表/视图(后台线程调用)
        /// </summary>
        /// <param name="node">数据库/模式/连接节点</param>
        private List<DaoChuBiao> CollectDaoChuBiao(DbNode node)
        {
            var lj = node.LianJie;
            if (lj == null)
            {
                return null;
            }
            var list = new List<DaoChuBiao>();
            if (Db.HasSchemaLayer(lj.DataBaseType))
            {
                //有模式层级:模式节点只取自身,数据库节点汇总其下全部模式
                var schemas = new List<string>();
                if (node.NodeType == DbNodeType.Schema)
                {
                    schemas.Add(node.SchemaName ?? node.Name);
                }
                else if (node.IsLoaded && node.AllChildren != null)
                {
                    schemas.AddRange(node.AllChildren
                        .Where(p => p.NodeType == DbNodeType.Schema)
                        .Select(p => p.Name));
                }
                else
                {
                    var names = Db.GetSchemas(lj, node.Name);
                    if (names != null)
                    {
                        schemas.AddRange(names);
                    }
                }
                foreach (var sn in schemas)
                {
                    var ctx = Db.CreateContext(lj, lj.DataBaseName, sn);
                    var tables = Db.GetTablesBySchema(ctx, sn);
                    if (tables == null)
                    {
                        continue;
                    }
                    foreach (var t in tables)
                    {
                        list.Add(new DaoChuBiao
                        {
                            Name = t.Name,
                            Comment = t.Comment,
                            IsView = t.IsView,
                            SchemaName = sn,
                            Biao = t,
                            LianJie = ctx
                        });
                    }
                }
                return list;
            }
            //无模式层级:已加载时直接用缓存的子节点,否则实时查询
            List<Biao> tables2;
            if (node.IsLoaded && node.AllChildren != null && node.AllChildren.Count > 0)
            {
                tables2 = node.AllChildren
                    .Where(p => p.NodeType == DbNodeType.Table || p.NodeType == DbNodeType.View)
                    .Select(p => p.Biao)
                    .ToList();
            }
            else
            {
                tables2 = Db.GetTables(lj);
            }
            if (tables2 == null)
            {
                return list;
            }
            foreach (var t in tables2)
            {
                list.Add(new DaoChuBiao
                {
                    Name = t.Name,
                    Comment = t.Comment,
                    IsView = t.IsView,
                    SchemaName = node.SchemaName,
                    Biao = t,
                    LianJie = lj
                });
            }
            return list;
        }

        /// <summary>
        /// 导出数据库/模式/连接下的表(先查询表列表,再弹出勾选窗口)
        /// </summary>
        private void ExportSql(DbNode node)
        {
            LoadingG.Visibility = Visibility.Visible;
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var items = CollectDaoChuBiao(node);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        LoadingG.Visibility = Visibility.Collapsed;
                        if (items == null || items.Count == 0)
                        {
                            Msg.Warning("未查询到可导出的表");
                            return;
                        }
                        var f = new DaoChuSql(node.LianJie, items, node.Name)
                        {
                            Owner = this
                        };
                        _ = f.ShowDialog();
                    }));
                }));
        }

        /// <summary>
        /// 导出当前表/视图
        /// </summary>
        private void ExportSqlBiao(DbNode node)
        {
            var b = node.Biao;
            if (b == null)
            {
                return;
            }
            var item = new DaoChuBiao
            {
                IsChecked = true,
                Name = b.Name,
                Comment = b.Comment,
                IsView = b.IsView,
                SchemaName = node.SchemaName,
                Biao = b,
                LianJie = node.LianJie
            };
            var f = new DaoChuSql(node.LianJie, item)
            {
                Owner = this
            };
            _ = f.ShowDialog();
        }

        /// <summary>
        /// 导入SQL到当前数据库/模式/连接
        /// </summary>
        private void ImportSql(DbNode node)
        {
            var f = new DaoRuSql(node.LianJie, node.Name)
            {
                Owner = this
            };
            _ = f.ShowDialog();
        }

        #endregion

        #region 过滤与绑定

        /// <summary>
        /// 按当前关键字与过滤规则重建树绑定(不会重新查询数据库)
        /// </summary>
        private void RebuildBindings()
        {
            var searchKey = _mainWindowModel.Key ?? "";
            var list = new List<DbNode>();
            foreach (var root in _rootNodes.OrderBy(p => p.Name))
            {
                if (ApplyFilter(root, searchKey, root.LianJie))
                {
                    list.Add(root);
                }
            }
            _mainWindowModel.LianJieBindList = list;
        }

        /// <summary>
        /// 递归过滤节点并返回该节点是否可见
        /// </summary>
        private bool ApplyFilter(DbNode node, string key, LianJie rootLj)
        {
            if (node.NodeType == DbNodeType.Loading)
            {
                return true;
            }
            if (node.NodeType == DbNodeType.Table || node.NodeType == DbNodeType.View)
            {
                //无库类型连接的集合(MongoDB):只按关键字过滤
                if (!string.IsNullOrEmpty(rootLj?.NoDbType))
                {
                    return node.Name.ContainsAnyKeyword(key);
                }
                return MatchName(node.Name, key, GetTableFilter(node, rootLj));
            }
            if (node.NodeType == DbNodeType.RedisDb || node.NodeType == DbNodeType.MongoDb)
            {
                return node.Name.ContainsAnyKeyword(key);
            }
            if (node.NodeType == DbNodeType.Database)
            {
                //存在数据库层级时,连接级过滤作用于数据库名
                if (Db.HasDatabaseLayer(rootLj.DataBaseType) && rootLj.IsFilter)
                {
                    if (!PassFilter(node.Name, rootLj.IncludingList, rootLj.ExcludeList))
                    {
                        return false;
                    }
                }
            }
            if (node.NodeType == DbNodeType.Schema)
            {
                //存在模式层级时,数据库节点上的过滤作用于模式名
                var dbCfg = GetDbFilter(rootLj, node.DatabaseName);
                if (dbCfg != null && dbCfg.IsFilter)
                {
                    if (!PassFilter(node.Name, dbCfg.IncludingList, dbCfg.ExcludeList))
                    {
                        return false;
                    }
                }
            }
            if (node.AllChildren != null)
            {
                var visible = new List<DbNode>();
                foreach (var c in node.AllChildren)
                {
                    if (ApplyFilter(c, key, rootLj))
                    {
                        visible.Add(c);
                    }
                }
                node.Children = visible;
                //尚未加载的节点其子节点未知,保留该节点以便展开后继续加载
                if (node.IsLoaded)
                {
                    return visible.Count > 0 || node.Name.ContainsAnyKeyword(key);
                }
            }
            return true;
        }

        /// <summary>
        /// 获取表/视图节点适用的过滤规则
        /// </summary>
        private FilterConfig GetTableFilter(DbNode node, LianJie rootLj)
        {
            //无数据库层级的类型(Oracle/Sqlite/达梦):沿用连接级的表过滤
            if (!Db.HasDatabaseLayer(rootLj.DataBaseType))
            {
                return new FilterConfig
                {
                    IsFilter = rootLj.IsFilter,
                    IncludingList = rootLj.IncludingList,
                    ExcludeList = rootLj.ExcludeList
                };
            }
            var dbName = node.DatabaseName;
            if (string.IsNullOrEmpty(dbName))
            {
                return null;
            }
            if (Db.HasSchemaLayer(rootLj.DataBaseType))
            {
                //存在模式层级:表过滤配置在模式上
                var schemaName = node.SchemaName;
                if (string.IsNullOrEmpty(schemaName))
                {
                    return null;
                }
                var schemaKey = SchemaFilterKey(dbName, schemaName);
                if (rootLj.TableFilterDict != null && rootLj.TableFilterDict.TryGetValue(schemaKey, out var cfg))
                {
                    return cfg;
                }
                return null;
            }
            //无模式层级(MySQL):库级配置即表过滤
            if (rootLj.DbFilterDict != null && rootLj.DbFilterDict.TryGetValue(dbName, out var dbCfg))
            {
                return dbCfg;
            }
            //兼容旧配置(曾以库名为键存表过滤)
            if (rootLj.TableFilterDict != null && rootLj.TableFilterDict.TryGetValue(dbName, out var oldCfg))
            {
                return oldCfg;
            }
            return null;
        }

        /// <summary>
        /// 获取数据库节点下的过滤规则(有模式层级时为模式过滤,否则为表过滤)
        /// </summary>
        private FilterConfig GetDbFilter(LianJie rootLj, string dbName)
        {
            if (string.IsNullOrEmpty(dbName) || rootLj.DbFilterDict == null)
            {
                return null;
            }
            return rootLj.DbFilterDict.TryGetValue(dbName, out var cfg) ? cfg : null;
        }

        /// <summary>
        /// 模式级表过滤的键
        /// </summary>
        private static string SchemaFilterKey(string dbName, string schemaName)
        {
            return $"{dbName}.{schemaName}";
        }

        /// <summary>
        /// 获取(没有则创建)指定数据库下的过滤配置
        /// </summary>
        private FilterConfig GetOrCreateDbFilter(LianJie rootLj, string dbName)
        {
            if (rootLj.DbFilterDict == null)
            {
                rootLj.DbFilterDict = new Dictionary<string, FilterConfig>();
            }
            if (!rootLj.DbFilterDict.ContainsKey(dbName) || rootLj.DbFilterDict[dbName] == null)
            {
                rootLj.DbFilterDict[dbName] = new FilterConfig();
            }
            return rootLj.DbFilterDict[dbName];
        }

        /// <summary>
        /// 获取(没有则创建)指定模式下的表过滤配置
        /// </summary>
        private FilterConfig GetOrCreateTableFilter(LianJie rootLj, string schemaKey)
        {
            if (rootLj.TableFilterDict == null)
            {
                rootLj.TableFilterDict = new Dictionary<string, FilterConfig>();
            }
            if (!rootLj.TableFilterDict.ContainsKey(schemaKey) || rootLj.TableFilterDict[schemaKey] == null)
            {
                rootLj.TableFilterDict[schemaKey] = new FilterConfig();
            }
            return rootLj.TableFilterDict[schemaKey];
        }

        /// <summary>
        /// 名称是否匹配关键字与过滤规则
        /// </summary>
        private bool MatchName(string name, string key, FilterConfig cfg)
        {
            if (!name.ContainsAnyKeyword(key))
            {
                return false;
            }
            if (cfg == null || !cfg.IsFilter)
            {
                return true;
            }
            return PassFilter(name, cfg.IncludingList, cfg.ExcludeList);
        }

        /// <summary>
        /// 名称是否通过包含/排除规则
        /// </summary>
        private bool PassFilter(string name, List<string> includingList, List<string> excludeList)
        {
            //包含的列表(白名单)
            if (includingList != null && includingList.Count > 0)
            {
                var included = false;
                foreach (var inc in includingList)
                {
                    if (string.IsNullOrEmpty(inc))
                    {
                        continue;
                    }
                    if (StrHelp.Match(name, inc))
                    {
                        included = true;
                        break;
                    }
                }
                if (!included)
                {
                    return false;
                }
            }
            //排除的列表(黑名单)
            if (excludeList != null)
            {
                foreach (var exc in excludeList)
                {
                    if (string.IsNullOrEmpty(exc))
                    {
                        continue;
                    }
                    if (StrHelp.Match(name, exc))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        #endregion

        #region 托盘相关
        private void XianShi_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = Config.IsMax ? WindowState.Maximized : WindowState.Normal;
            Show();
            Activate();
        }

        private void GongJu_OnClick(object sender, RoutedEventArgs e)
        {
            var box = new ToolBox();
            //先把工具面板装配好再显示,避免窗口先出来空壳、内容晚一拍才出现
            box.UcBox.Content = new MainTools();
            box.Show();
        }

        /// <summary>
        /// AI对话:在页签里打开通用 AI 对话页(已打开则直接切回)
        /// </summary>
        private void AiDuiHua_OnClick(object sender, RoutedEventArgs e)
        {
            const string tabKey = "ai-chat";
            if (ActivateTab(tabKey))
            {
                var exist = tabList.FirstOrDefault(p => p.Key == tabKey)?.Content as AiChatTabView;
                exist?.FocusInput();
                return;
            }
            var view = new AiChatTabView();
            tabList.Add(new TabModel
            {
                Header = "AI对话",
                Key = tabKey,
                Content = view
            });
            tc.ItemsSource = tabList;
            tc.SelectedIndex = tc.Items.Count - 1;
            view.FocusInput();
        }

        private void SheZhi_OnClick(object sender, RoutedEventArgs e)
        {
            new SheZhi { Owner = this }.ShowDialog();
            //设置里改了 AI 模型配置,刷新所有查询页签里常驻的 AI 助手面板下拉(设置是模态窗,关掉才算改完)
            foreach (var t in tabList)
            {
                if (t.Content is TabView tv)
                {
                    tv.RefreshAiModels();
                }
                else if (t.Content is AiChatTabView chat)
                {
                    chat.RefreshModels();
                }
            }
        }

        private void RiZhi_OnClick(object sender, RoutedEventArgs e)
        {
            var dir = Path.Combine(Core.GetAppPath, "logs");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            Process.Start("explorer.exe", @"/e, " + dir);
        }

        private void TuiChu_OnClick(object sender, RoutedEventArgs e)
        {
            WindowClose();
        }
        #endregion

        /// <summary>
        /// 窗体关闭前判断是否最小化托盘
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GlowWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {

                if (!Config.IsClose)
                {
                    e.Cancel = true;
                    Hide();
                    Shuaxin(3);
                    if (WindowState == WindowState.Normal)
                    {
                        Config.IsMax = false;
                        Config.X = (int)Left;
                        Config.Y = (int)Top;
                        Config.W = (int)Width;
                        Config.H = (int)Height;
                    }
                    else
                    {
                        Config.IsMax = true;
                    }
                }
                else
                {
                    WindowClose();
                }
            }
            catch (Exception ex)
            {
                Oops.Warning(ex.Message);
            }

        }

        /// <summary>
        /// 关闭窗体处理数据保存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowClose()
        {
            try
            {
                ni.Dispose();
                Shuaxin(3);
                if (WindowState == WindowState.Normal)
                {
                    Config.IsMax = false;
                    Config.X = (int)Left;
                    Config.Y = (int)Top;
                    Config.W = (int)Width;
                    Config.H = (int)Height;
                }
                else
                {
                    Config.IsMax = true;
                }
                Core.RegisterDeleteDllOnExit(Path.Combine(Core.GetAppPath, "10940_x64"));
                Core.RegisterDeleteDllOnExit(Path.Combine(Core.GetAppPath, "10940_x86"));
            }
            catch (Exception ex)
            {
                Oops.Warning(ex.Message);
            }
            finally
            {
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// 连接列表右键菜单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvLianjie_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 过滤：仅处理右键点击
            if (e.RightButton == MouseButtonState.Pressed)
            {
                // 找到触发事件的TreeViewItem（关键：OriginalSource向上遍历）
                var treeViewItem = FindVisualParent<TreeViewItem>((DependencyObject)e.OriginalSource);
                if (treeViewItem != null)
                {
                    // 1. 强制选中当前节点
                    treeViewItem.IsSelected = true;

                    // 2. 定义菜单
                    var contextMenu = new ContextMenu();
                    if (tvLianjie.SelectedItem is DbNode node && node.NodeType == DbNodeType.Connection)
                    {
                        var l = node.LianJie;
                        var menuItem0 = new MenuItem
                        {
                            Header = "刷新连接",
                            Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/reconnect.png", 16, 16)
                        };
                        menuItem0.Click += (s, args) =>
                        {
                            ReloadNode(node);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItem0);
                        var menuItem1 = new MenuItem
                        {
                            Header = "编辑连接",
                            Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/edit.png", 16, 16)
                        };
                        menuItem1.Click += (s, args) =>
                        {
                            if (!string.IsNullOrEmpty(l.NoDbType))
                            {
                                var f = new XinJianNoDb(l)
                                {
                                    Owner = this
                                };
                                if (f.ShowDialog() == true)
                                {
                                    Shuaxin(1, f._lj);
                                }
                            }
                            else
                            {
                                if (l.DataBaseType == ORM.DbTypeEnum.SqlIte)
                                {
                                    var f = new XinJianLocal(l)
                                    {
                                        Owner = this
                                    };
                                    if (f.ShowDialog() == true)
                                    {
                                        Shuaxin(1, f._lj);
                                    }
                                }
                                else
                                {
                                    var f = new XinJian(l)
                                    {
                                        Owner = this
                                    };
                                    if (f.ShowDialog() == true)
                                    {
                                        Shuaxin(1, f._lj);
                                    }
                                }
                            }
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItem1);
                        var menuItem11 = new MenuItem
                        {
                            Header = "复制连接",
                            Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/copy.png", 16, 16)
                        };
                        menuItem11.Click += (s, args) =>
                        {
                            var lj = CoreConvert.Map<LianJie>(l);
                            lj.Id = null;
                            lj.Name += " 复制";
                            lj.BiaoList = null;
                            lj.IncludingList = null;
                            lj.ExcludeList = null;
                            if (!string.IsNullOrEmpty(l.NoDbType))
                            {
                                var f = new XinJianNoDb(lj)
                                {
                                    Owner = this
                                };
                                if (f.ShowDialog() == true)
                                {
                                    Shuaxin(0, f._lj);
                                }
                            }
                            else
                            {
                                if (lj.DataBaseType == ORM.DbTypeEnum.SqlIte)
                                {
                                    var f = new XinJianLocal(lj)
                                    {
                                        Owner = this
                                    };
                                    if (f.ShowDialog() == true)
                                    {
                                        Shuaxin(0, f._lj);
                                    }
                                }
                                else
                                {
                                    var f = new XinJian(lj)
                                    {
                                        Owner = this
                                    };
                                    if (f.ShowDialog() == true)
                                    {
                                        Shuaxin(0, f._lj);
                                    }
                                }
                            }
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItem11);
                        var menuItem12 = new MenuItem
                        {
                            Header = "导出连接",
                            Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/export.png", 16, 16)
                        };
                        menuItem12.Click += (s, args) =>
                        {
                            var save = new System.Windows.Forms.SaveFileDialog
                            {
                                Filter = @"WDB连接文件(*.connect)|*.connect",
                                FileName = l.Name + "_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")
                            };
                            if (save.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                var ljList = new List<LianJie>
                                {
                                    l
                                };
                                File.WriteAllText(save.FileName, CoreEncrypt.AesEncrypt(JsonConvert.SerializeObject(ljList), GConst.KEY));
                                Msg.Ok(@"导出完成!");
                            }
                        };
                        contextMenu.Items.Add(menuItem12);
                        contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                        var menuItem2 = new MenuItem
                        {
                            Header = "删除连接",
                            Foreground = Brushes.DarkRed,
                            Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/delete.png", 16, 16)
                        };
                        menuItem2.Click += (s, args) =>
                        {
                            if (Msg.Question($"确认删除 {l.Name}？"))
                            {
                                Shuaxin(2, l);
                            }
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItem2);
                        contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                        if (string.IsNullOrEmpty(l.NoDbType))
                        {
                            //有数据库层级的类型过滤数据库名;Oracle/Sqlite/达梦等无库层级,仍过滤表名
                            var isDbLayer = Db.HasDatabaseLayer(l.DataBaseType);
                            var menuItem3 = new MenuItem
                            {
                                Header = isDbLayer ? "配置数据库过滤" : "配置表过滤",
                                Icon = IconHelp.CreateImageIcon(IconRoot + "filter.png", 16, 16)
                            };
                            menuItem3.Click += (s, args) =>
                            {
                                var cfg = new FilterConfig
                                {
                                    IsFilter = l.IsFilter,
                                    IncludingList = l.IncludingList,
                                    ExcludeList = l.ExcludeList
                                };
                                var f = new BiaoGuolv(cfg, isDbLayer ? "数据库" : "表名", l.Name)
                                {
                                    Owner = this
                                };
                                if (f.ShowDialog() == true)
                                {
                                    l.IsFilter = cfg.IsFilter;
                                    l.IncludingList = cfg.IncludingList;
                                    l.ExcludeList = cfg.ExcludeList;
                                    //仅重算节点可见性并保存配置,不丢弃已加载的节点
                                    Shuaxin(3);
                                }
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItem3);
                        }

                        if (string.IsNullOrEmpty(l.NoDbType))
                        {
                            contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                            if (Db.CanCreateDataBase(l.DataBaseType))
                            {
                                var menuItem5 = new MenuItem
                                {
                                    Header = "创建数据库",
                                    Icon = IconHelp.CreateImageIcon(IconRoot + "database.png", 16, 16)
                                };
                                menuItem5.Click += (s, args) =>
                                {
                                    CreateDataBase(node);
                                    contextMenu.IsOpen = false;
                                };
                                contextMenu.Items.Add(menuItem5);
                            }
                            else
                            {
                                //无数据库层级的类型(Oracle/达梦/Sqlite):连接下直接是表,直接在连接上建表
                                var menuItem5 = new MenuItem
                                {
                                    Header = "新建表",
                                    Icon = IconHelp.CreateImageIcon(IconRoot + "table.png", 16, 16)
                                };
                                menuItem5.Click += (s, args) =>
                                {
                                    CreateTable(node);
                                    contextMenu.IsOpen = false;
                                };
                                contextMenu.Items.Add(menuItem5);
                                //无数据库层级时连接下就是表,导入导出作用在连接上
                                var menuItem6 = new MenuItem
                                {
                                    Header = "导出数据",
                                    Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/export.png", 16, 16)
                                };
                                menuItem6.Click += (s, args) =>
                                {
                                    ExportSql(node);
                                    contextMenu.IsOpen = false;
                                };
                                contextMenu.Items.Add(menuItem6);
                                var menuItem7 = new MenuItem
                                {
                                    Header = "导入数据",
                                    Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/import.png", 16, 16)
                                };
                                menuItem7.Click += (s, args) =>
                                {
                                    ImportSql(node);
                                    contextMenu.IsOpen = false;
                                };
                                contextMenu.Items.Add(menuItem7);
                            }
                            var menuItem4 = new MenuItem
                            {
                                Header = "新建查询",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/sql_script_create.png", 16, 16)
                            };
                            menuItem4.Click += (s, args) =>
                            {
                                Liulan(l, null);
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItem4);
                        }
                        else
                        {
                            contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                            var menuItemCmd = new MenuItem
                            {
                                Header = "新建查询",
                                Icon = IconHelp.CreateImageIcon($"{IconRoot}sql_script_create.png", 16, 16)
                            };
                            menuItemCmd.Click += (s, args) =>
                            {
                                if (IsMongo(l))
                                {
                                    OpenMongoQuery(node, l.DataBaseName);
                                }
                                else
                                {
                                    OpenRedisConsole(node);
                                }
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItemCmd);
                            var menuItem4 = new MenuItem
                            {
                                Header = $"查看{l.NoDbType}属性",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/table.png", 16, 16)
                            };
                            menuItem4.Click += (s, args) =>
                            {
                                LoadingG.Visibility = Visibility.Visible;
                                _ = Task.Factory.StartNew(
                                   new Action(() =>
                                   {
                                       try
                                       {
                                           var info = IsMongo(l) ? Db.GetMongoInfo(l) : Db.GetRedisInfo(l);
                                           Dispatcher.Invoke(new Action(() =>
                                           {
                                               LoadingG.Visibility = Visibility.Collapsed;
                                               OpenNoDbInfoTab(l, info);
                                           }));
                                       }
                                       catch (Exception ex)
                                       {
                                           Log.Error(ex.Message);
                                           Dispatcher.Invoke(new Action(() =>
                                           {
                                               LoadingG.Visibility = Visibility.Collapsed;
                                               Msg.Warning(ex.Message);
                                           }));
                                       }
                                   }));
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItem4);
                        }
                    }
                    else if (tvLianjie.SelectedItem is DbNode node2 && (node2.NodeType == DbNodeType.Table || node2.NodeType == DbNodeType.View))
                    {
                        var lj = node2.LianJie;
                        var b = node2.Biao;

                        if (string.IsNullOrEmpty(lj.NoDbType))
                        {
                            #region 表相关右键菜单
                            var menuItem0 = new MenuItem
                            {
                                Header = "查看数据",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/search.png", 16, 16)
                            };
                            menuItem0.Click += (s, args) =>
                            {
                                OpenDataView(node2);
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItem0);
                            var menuItem1 = new MenuItem
                            {
                                Header = "打开表结构",
                                Icon = IconHelp.CreateImageIcon(IconRoot + "table.png", 16, 16)
                            };
                            menuItem1.Click += (s, args) =>
                            {
                                if (b.IsView)
                                {
                                    //视图不支持编辑,只查看
                                    LoadingG.Visibility = Visibility.Visible;
                                    _ = Task.Factory.StartNew(
                                       new Action(() =>
                                       {
                                           b.ZiDuanList = Db.GetZiDuans(b, lj);
                                           Dispatcher.Invoke(new Action(() =>
                                           {
                                               LoadingG.Visibility = Visibility.Collapsed;
                                               OpenBiaoJieGouTab(lj, b);
                                           }));
                                       }));
                                }
                                else
                                {
                                    //表:直接打开可编辑的结构窗口
                                    EditBiaoJieGou(node2);
                                }
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItem1);
                            contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                            var menuItemExport = new MenuItem
                            {
                                Header = "导出数据",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/export.png", 16, 16)
                            };
                            menuItemExport.Click += (s, args) =>
                            {
                                ExportSqlBiao(node2);
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItemExport);
                            contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                            if (b.IsView)
                            {
                                var menuItem4 = new MenuItem
                                {
                                    Header = "删除视图",
                                    Foreground = Brushes.DarkRed,
                                    Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/delete.png", 16, 16)
                                };
                                menuItem4.Click += (s, args) =>
                                {
                                    if (Msg.Question($"是否删除视图[{b.Name})]?,此操作不可恢复,请谨慎操作!"))
                                    {
                                        LoadingG.Visibility = Visibility.Visible;
                                        _ = Task.Factory.StartNew(
                                           new Action(() =>
                                           {
                                               _ = Db.GetDataTable(lj, $"DROP VIEW {b.Name};", out _, out _);
                                               Dispatcher.Invoke(new Action(() =>
                                               {
                                                   ReloadParent(node2);
                                                   LoadingG.Visibility = Visibility.Collapsed;
                                               }));
                                           }));
                                    }
                                    contextMenu.IsOpen = false;
                                };
                                contextMenu.Items.Add(menuItem4);
                            }
                            else
                            {
                                var menuItem2 = new MenuItem
                                {
                                    Header = "清空表数据",
                                    Foreground = Brushes.DarkRed,
                                    Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/delete_table.png", 16, 16)
                                };
                                menuItem2.Click += (s, args) =>
                                {
                                    if (Msg.Question($"是否清空表[{b.Name})]?,此操作不可恢复,请谨慎操作!"))
                                    {
                                        LoadingG.Visibility = Visibility.Visible;
                                        _ = Task.Factory.StartNew(
                                           new Action(() =>
                                           {
                                               _ = Db.GetDataTable(lj, $"DELETE FROM {b.Name} WHERE 1=1;", out _, out _);
                                               Dispatcher.Invoke(new Action(() =>
                                               {
                                                   LoadingG.Visibility = Visibility.Collapsed;
                                               }));
                                           }));
                                    }
                                    contextMenu.IsOpen = false;
                                };
                                contextMenu.Items.Add(menuItem2);
                                if (lj.DataBaseType != ORM.DbTypeEnum.SqlIte)
                                {
                                    var menuItem3 = new MenuItem
                                    {
                                        Header = "截断表",
                                        Foreground = Brushes.DarkRed,
                                        Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/delete_table.png", 16, 16)
                                    };
                                    menuItem3.Click += (s, args) =>
                                    {
                                        if (Msg.Question($"是否截断表[{b.Name})]?,此操作不可恢复,请谨慎操作!"))
                                        {
                                            LoadingG.Visibility = Visibility.Visible;
                                            _ = Task.Factory.StartNew(
                                               new Action(() =>
                                               {
                                                   _ = Db.GetDataTable(lj, $"TRUNCATE TABLE {b.Name};", out _, out _);
                                                   Dispatcher.Invoke(new Action(() =>
                                                   {
                                                       LoadingG.Visibility = Visibility.Collapsed;
                                                   }));
                                               }));
                                        }
                                        contextMenu.IsOpen = false;
                                    };
                                    contextMenu.Items.Add(menuItem3);
                                }
                                var menuItem4 = new MenuItem
                                {
                                    Header = "删除表",
                                    Foreground = Brushes.DarkRed,
                                    Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/delete.png", 16, 16)
                                };
                                menuItem4.Click += (s, args) =>
                                {
                                    if (Msg.Question($"是否删除表[{b.Name})]?,此操作不可恢复,请谨慎操作!"))
                                    {
                                        LoadingG.Visibility = Visibility.Visible;
                                        _ = Task.Factory.StartNew(
                                           new Action(() =>
                                           {
                                               _ = Db.GetDataTable(lj, $"DROP TABLE {b.Name};", out _, out _);
                                               Dispatcher.Invoke(new Action(() =>
                                               {
                                                   ReloadParent(node2);
                                                   LoadingG.Visibility = Visibility.Collapsed;
                                               }));
                                           }));
                                    }
                                    contextMenu.IsOpen = false;
                                };
                                contextMenu.Items.Add(menuItem4);
                            }

                            contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                            var menuItem5 = new MenuItem
                            {
                                Header = "创建SQL",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/sql_script_create.png", 16, 16)
                            };
                            var menuItem51 = new MenuItem
                            {
                                Header = "SELECT",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/sql_text.png", 16, 16)
                            };
                            menuItem51.Click += (s, args) =>
                            {
                                _ = new SqlWindow(Db.GetSelect(b, lj))
                                {
                                    Owner = this
                                }.ShowDialog();
                                contextMenu.IsOpen = false;
                            };
                            menuItem5.Items.Add(menuItem51);
                            var menuItem52 = new MenuItem
                            {
                                Header = "INSERT",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/sql_text.png", 16, 16)
                            };
                            menuItem52.Click += (s, args) =>
                            {
                                _ = new SqlWindow(Db.GetInsert(b, lj))
                                {
                                    Owner = this
                                }.ShowDialog();
                                contextMenu.IsOpen = false;
                            };
                            menuItem5.Items.Add(menuItem52);
                            var menuItem53 = new MenuItem
                            {
                                Header = "UPDATE",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/sql_text.png", 16, 16)
                            };
                            menuItem53.Click += (s, args) =>
                            {
                                _ = new SqlWindow(Db.GetUpdate(b, lj))
                                {
                                    Owner = this
                                }.ShowDialog();
                                contextMenu.IsOpen = false;
                            };
                            menuItem5.Items.Add(menuItem53);
                            var menuItem54 = new MenuItem
                            {
                                Header = "DELETE",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/sql_text.png", 16, 16)
                            };
                            menuItem54.Click += (s, args) =>
                            {
                                _ = new SqlWindow(Db.GetDelete(b, lj))
                                {
                                    Owner = this
                                }.ShowDialog();
                                contextMenu.IsOpen = false;
                            };
                            menuItem5.Items.Add(menuItem54);
                            var menuItem55 = new MenuItem
                            {
                                Header = "DDL",
                                Icon = IconHelp.CreateImageIcon(IconRoot + "sql_script_create.png", 16, 16)
                            };
                            menuItem55.Click += (s, args) =>
                            {
                                //查询当前表(视图)的建表语句
                                LoadingG.Visibility = Visibility.Visible;
                                _ = Task.Factory.StartNew(
                                   new Action(() =>
                                   {
                                       //视图直接取已加载的定义SQL,表则实时查询DDL
                                       var ddl = b.IsView ? b.TableSql : Db.GetDDL(b, lj);
                                       Dispatcher.Invoke(new Action(() =>
                                       {
                                           LoadingG.Visibility = Visibility.Collapsed;
                                           if (string.IsNullOrEmpty(ddl))
                                           {
                                               Msg.Warning("未查询到该表的DDL");
                                               return;
                                           }
                                           _ = new SqlWindow(ddl.SqlFormat())
                                           {
                                               Owner = this
                                           }.ShowDialog();
                                       }));
                                   }));
                                contextMenu.IsOpen = false;
                            };
                            menuItem5.Items.Add(menuItem55);
                            contextMenu.Items.Add(menuItem5);
                            contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                            var menuItem6 = new MenuItem
                            {
                                Header = "复制表名",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/copy.png", 16, 16)
                            };
                            menuItem6.Click += (s, args) =>
                            {
                                SafeClipboard.SetText(b.Name);
                                Oops.Ok("表名已复制到剪切板");
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItem6);
                            #endregion
                        }
                        else
                        {
                            //nodb相关右键菜单
                            var menuItem0 = new MenuItem
                            {
                                Header = "查看数据",
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/search.png", 16, 16)
                            };
                            menuItem0.Click += (s, args) =>
                            {
                                Liulan(lj, b);
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItem0);
                        }
                    }

                    else if (tvLianjie.SelectedItem is DbNode node3 &&
                             (node3.NodeType == DbNodeType.Database || node3.NodeType == DbNodeType.Schema))
                    {
                        //数据库/模式节点菜单
                        var menuItemSql = new MenuItem
                        {
                            Header = $"新建查询({node3.Name})",
                            Icon = IconHelp.CreateImageIcon(IconRoot + "sql_script_create.png", 16, 16)
                        };
                        menuItemSql.Click += (s, args) =>
                        {
                            Liulan(node3.LianJie, null);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemSql);

                        var menuItemDdl = new MenuItem
                        {
                            Header = "新建表",
                            Icon = IconHelp.CreateImageIcon(IconRoot + "table.png", 16, 16)
                        };
                        menuItemDdl.Click += (s, args) =>
                        {
                            CreateTable(node3);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemDdl);

                        //有模式层级的库(数据库节点)支持新建模式;模式节点本身不再嵌套
                        if (node3.NodeType == DbNodeType.Database && Db.CanCreateSchema(node3.LianJie.DataBaseType))
                        {
                            var menuItemNewSchema = new MenuItem
                            {
                                Header = "新建模式",
                                Icon = IconHelp.CreateImageIcon(IconRoot + "schema.png", 16, 16)
                            };
                            menuItemNewSchema.Click += (s, args) =>
                            {
                                CreateSchema(node3);
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItemNewSchema);
                        }

                        var menuItemExport = new MenuItem
                        {
                            Header = "导出数据",
                            Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/export.png", 16, 16)
                        };
                        menuItemExport.Click += (s, args) =>
                        {
                            ExportSql(node3);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemExport);

                        var menuItemImport = new MenuItem
                        {
                            Header = "导入数据",
                            Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/import.png", 16, 16)
                        };
                        menuItemImport.Click += (s, args) =>
                        {
                            ImportSql(node3);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemImport);

                        var menuItemRefresh = new MenuItem
                        {
                            Header = "刷新",
                            Icon = IconHelp.CreateImageIcon(IconRoot + "reconnect.png", 16, 16)
                        };
                        menuItemRefresh.Click += (s, args) =>
                        {
                            ReloadNode(node3);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemRefresh);

                        if (node3.NodeType == DbNodeType.Database)
                        {
                            var menuItemDropDb = new MenuItem
                            {
                                Header = "删除数据库",
                                Foreground = Brushes.DarkRed,
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/delete.png", 16, 16)
                            };
                            menuItemDropDb.Click += (s, args) =>
                            {
                                if (Msg.Question($"是否删除数据库[{node3.Name}]?,此操作不可恢复,请谨慎操作!"))
                                {
                                    DropDataBaseOrSchema(node3, true);
                                }
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItemDropDb);
                        }
                        else if (node3.NodeType == DbNodeType.Schema)
                        {
                            var menuItemDropSchema = new MenuItem
                            {
                                Header = "删除模式",
                                Foreground = Brushes.DarkRed,
                                Icon = IconHelp.CreateImageIcon($"pack://application:,,,/WDB;component/Resources/delete.png", 16, 16)
                            };
                            menuItemDropSchema.Click += (s, args) =>
                            {
                                if (Msg.Question($"是否删除模式[{node3.Name}]?,此操作不可恢复,请谨慎操作!"))
                                {
                                    DropDataBaseOrSchema(node3, false);
                                }
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItemDropSchema);
                        }
                        contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });

                        if (node3.NodeType == DbNodeType.Database || node3.NodeType == DbNodeType.Schema)
                        {
                            var rootLj = node3.RootLianJie ?? node3.LianJie;
                            var hasSchema = Db.HasSchemaLayer(rootLj.DataBaseType);
                            var isSchemaNode = node3.NodeType == DbNodeType.Schema;
                            //模式节点过滤表;数据库节点:有模式层级时过滤模式,否则过滤该库的表
                            var filterTarget = isSchemaNode || !hasSchema ? "表名" : "模式";
                            var menuItemFilter = new MenuItem
                            {
                                Header = $"配置{filterTarget}过滤",
                                Icon = IconHelp.CreateImageIcon(IconRoot + "filter.png", 16, 16)
                            };
                            menuItemFilter.Click += (s, args) =>
                            {
                                FilterConfig cfg;
                                string filterKey;
                                if (isSchemaNode)
                                {
                                    filterKey = SchemaFilterKey(node3.DatabaseName, node3.Name);
                                    cfg = GetOrCreateTableFilter(rootLj, filterKey);
                                }
                                else
                                {
                                    filterKey = node3.Name;
                                    cfg = GetOrCreateDbFilter(rootLj, filterKey);
                                }
                                var f = new BiaoGuolv(cfg, filterTarget, node3.Name)
                                {
                                    Owner = this
                                };
                                if (f.ShowDialog() == true)
                                {
                                    //写回根连接配置并保存
                                    if (isSchemaNode)
                                    {
                                        rootLj.TableFilterDict[filterKey] = cfg;
                                    }
                                    else
                                    {
                                        rootLj.DbFilterDict[filterKey] = cfg;
                                    }
                                    Shuaxin(3);
                                }
                                contextMenu.IsOpen = false;
                            };
                            contextMenu.Items.Add(menuItemFilter);
                            contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                        }
                        contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                        var menuItemCopy = new MenuItem
                        {
                            Header = "复制名称",
                            Icon = IconHelp.CreateImageIcon(IconRoot + "copy.png", 16, 16)
                        };
                        menuItemCopy.Click += (s, args) =>
                        {
                            SafeClipboard.SetText(node3.Name);
                            Oops.Ok("名称已复制到剪切板");
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemCopy);
                    }
                    else if (tvLianjie.SelectedItem is DbNode node4 && node4.NodeType == DbNodeType.RedisDb)
                    {
                        //Redis库节点菜单
                        var menuItemOpen = new MenuItem
                        {
                            Header = "查看数据",
                            Icon = IconHelp.CreateImageIcon(IconRoot + "search.png", 16, 16)
                        };
                        menuItemOpen.Click += (s, args) =>
                        {
                            Liulan(node4.LianJie, node4.Biao);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemOpen);

                        var menuItemCmd = new MenuItem
                        {
                            Header = $"新建查询({node4.Name})",
                            Icon = IconHelp.CreateImageIcon(IconRoot + "sql_script_create.png", 16, 16)
                        };
                        menuItemCmd.Click += (s, args) =>
                        {
                            _ = long.TryParse(node4.Name, out var db);
                            OpenRedisConsole(node4, db);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemCmd);

                        var menuItemRefresh = new MenuItem
                        {
                            Header = "刷新",
                            Icon = IconHelp.CreateImageIcon(IconRoot + "reconnect.png", 16, 16)
                        };
                        menuItemRefresh.Click += (s, args) =>
                        {
                            ReloadNode(node4.Parent);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemRefresh);
                    }
                    else if (tvLianjie.SelectedItem is DbNode node6 && node6.NodeType == DbNodeType.MongoDb)
                    {
                        //MongoDB 数据库节点菜单
                        var menuItemCmd = new MenuItem
                        {
                            Header = $"新建查询({node6.Name})",
                            Icon = IconHelp.CreateImageIcon(IconRoot + "sql_script_create.png", 16, 16)
                        };
                        menuItemCmd.Click += (s, args) =>
                        {
                            OpenMongoQuery(node6, node6.Name);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemCmd);

                        var menuItemRefresh = new MenuItem
                        {
                            Header = "刷新",
                            Icon = IconHelp.CreateImageIcon(IconRoot + "reconnect.png", 16, 16)
                        };
                        menuItemRefresh.Click += (s, args) =>
                        {
                            ReloadNode(node6);
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemRefresh);

                        contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
                        var menuItemCopy = new MenuItem
                        {
                            Header = "复制名称",
                            Icon = IconHelp.CreateImageIcon(IconRoot + "copy.png", 16, 16)
                        };
                        menuItemCopy.Click += (s, args) =>
                        {
                            SafeClipboard.SetText(node6.Name);
                            Oops.Ok("名称已复制到剪切板");
                            contextMenu.IsOpen = false;
                        };
                        contextMenu.Items.Add(menuItemCopy);
                    }

                    contextMenu.PlacementTarget = treeViewItem; // 菜单绑定到当前节点
                    contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint; // 菜单显示在鼠标位置
                    contextMenu.IsOpen = true; // 手动弹出菜单


                    // 3. 阻止事件继续冒泡/处理
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 通用方法：向上遍历视觉树查找父级TreeViewItem
        /// </summary>
        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
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

        /// <summary>
        /// 查找指定容器内的所有指定类型控件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static T FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild)
                {
                    return tChild;
                }
                var ret = FindVisualChildren<T>(child);
                if (ret != null)
                {
                    return ret;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取指定TreeViewItem的父级TreeViewItem（UI层面）
        /// </summary>
        /// <param name="childItem">当前子节点</param>
        /// <returns>父级TreeViewItem（无则返回null）</returns>
        public static TreeViewItem GetParentTreeViewItem(TreeViewItem childItem)
        {
            if (childItem == null) return null;
            DependencyObject parent = VisualTreeHelper.GetParent(childItem);
            while (parent != null && !(parent is TreeViewItem))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as TreeViewItem;
        }

        /// <summary>
        /// 表双击浏览数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvLianjie_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(tvLianjie.SelectedItem is DbNode node))
            {
                return;
            }
            switch (node.NodeType)
            {
                case DbNodeType.Table:
                case DbNodeType.View:
                    //无库类型连接的集合(MongoDB):打开文档浏览页
                    if (!string.IsNullOrEmpty(node.LianJie?.NoDbType))
                    {
                        Liulan(node.LianJie, node.Biao);
                    }
                    else
                    {
                        OpenDataView(node);
                    }
                    break;
                case DbNodeType.RedisDb:
                    Liulan(node.LianJie, node.Biao);
                    break;
                case DbNodeType.Connection:
                case DbNodeType.Database:
                case DbNodeType.Schema:
                case DbNodeType.MongoDb:
                    //容器节点:双击加载下一层
                    LoadNodeChildren(node);
                    node.IsExpanded = true;
                    break;
            }
        }

        /// <summary>
        /// 表/视图标签的唯一标识(连接 + 服务器 + 库 + 模式 + 对象名 + 表/视图)
        /// </summary>
        private static string BuildTableTabKey(LianJie lj, Biao b)
        {
            return string.Join("|",
                lj?.Id ?? string.Empty,
                lj?.Host ?? string.Empty,
                lj?.Port ?? string.Empty,
                lj?.DataBaseName ?? string.Empty,
                lj?.ServiceName ?? string.Empty,
                b?.Name ?? string.Empty,
                b != null && b.IsView ? "V" : "T");
        }

        /// <summary>
        /// 无库类型连接(Redis/MongoDB)数据标签的唯一标识(连接 + 服务器 + 无库类型 + 库名/集合名)
        /// </summary>
        private static string BuildNoDbTabKey(LianJie lj, Biao b)
        {
            return string.Join("|",
                (lj?.NoDbType ?? "nodb").ToLowerInvariant(),
                lj?.Id ?? string.Empty,
                lj?.Host ?? string.Empty,
                lj?.Port ?? string.Empty,
                lj?.DataBaseName ?? string.Empty,
                b?.Name ?? string.Empty);
        }

        /// <summary>
        /// 命中已存在同标识的标签时直接切过去并返回 true,未命中返回 false(由调用方新建标签)
        /// </summary>
        private bool ActivateTab(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            var exist = tabList.FirstOrDefault(p => p.Key == key);
            if (exist == null)
            {
                return false;
            }
            tc.ItemsSource = tabList;
            tc.SelectedItem = exist;
            return true;
        }

        #region 首页

        /// <summary>
        /// 打开首页标签(开机后默认选中)
        /// </summary>
        private void ShowWelcomeTab()
        {
            if (_welcomeView != null)
            {
                return;
            }
            _welcomeView = new WelcomeView
            {
                OpenConnectionAction = FocusConnection,
                NewQueryAction = OpenQueryFromConnection,
                LicenseClickAction = ShowGuanYu
            };
            _welcomeView.Load(LianJieList);
            _welcomeView.SetLicenseText(BuildLicenseText(), BuildLicenseDetail());
            tabList.Insert(0, new TabModel
            {
                Header = "首页",
                Key = "welcome",
                CanClose = false,
                Content = _welcomeView
            });
            tc.ItemsSource = tabList;
            tc.SelectedIndex = 0;
        }

        /// <summary>
        /// 在左侧连接树中定位连接:展开并把滚动条滚到该连接(首页点连接行时调用)
        /// </summary>
        private void FocusConnection(LianJie lj)
        {
            if (lj == null)
            {
                return;
            }
            var node = _rootNodes.FirstOrDefault(p => p.LianJie != null && p.LianJie.Id == lj.Id);
            if (node == null)
            {
                return;
            }
            _pendingRevealNode = node;
            LoadNodeChildren(node);
            node.IsExpanded = true;
            RevealTreeNodeLater(node);
        }

        /// <summary>
        /// 首页连接行的[新建查询]:按连接类型直接开对应的查询页(SQL编辑器/MongoDB查询/Redis命令)
        /// </summary>
        private void OpenQueryFromConnection(LianJie lj)
        {
            if (lj == null)
            {
                return;
            }
            if (!string.IsNullOrEmpty(lj.NoDbType))
            {
                if (IsMongo(lj))
                {
                    OpenMongoQuery(lj, lj.DataBaseName);
                    return;
                }
                if (lj.NoDbType.Equals("Redis", StringComparison.OrdinalIgnoreCase))
                {
                    OpenRedisConsole(lj, 0);
                    return;
                }
                Msg.Warning($"{lj.NoDbType} 暂不支持新建查询");
                return;
            }
            Liulan(lj, null);
        }

        /// <summary>
        /// 延后到布局跑完再滚动(此刻容器才生成),期间若已换定位目标则放弃本次
        /// </summary>
        private void RevealTreeNodeLater(DbNode node)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (!ReferenceEquals(_pendingRevealNode, node))
                    {
                        return;
                    }
                    ScrollTreeNodeIntoView(node);
                    //展开会异步加载子节点并重建绑定:加载完成前不撤销,由加载回调再滚一次
                    if (node.IsLoaded)
                    {
                        _pendingRevealNode = null;
                    }
                }),
                DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 滚动左侧连接树到指定根节点并选中
        /// </summary>
        private void ScrollTreeNodeIntoView(DbNode node)
        {
            if (node == null || !tvLianjie.Items.Contains(node))
            {
                return;
            }
            tvLianjie.UpdateLayout();
            var item = tvLianjie.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
            if (item == null)
            {
                //虚拟化下目标在可视区外时容器不会生成,先把它滚进可视区
                item = ScrollUntilContainerGenerated(node);
            }
            if (item == null)
            {
                return;
            }
            item.IsSelected = true;
            item.BringIntoView();
        }

        /// <summary>
        /// 把可视区外的节点滚进可视区并返回其容器(滚不进去返回 null)。
        /// 比例与步长都按"可滚动范围/可视区高度"换算,不依赖滚动单位到底是"项"还是"像素"
        /// </summary>
        private TreeViewItem ScrollUntilContainerGenerated(DbNode node)
        {
            var scrollViewer = FindVisualChildren<ScrollViewer>(tvLianjie);
            var count = tvLianjie.Items.Count;
            var index = tvLianjie.Items.IndexOf(node);
            if (scrollViewer == null || index < 0 || count <= 1)
            {
                return null;
            }
            //按位置比例粗定位:目标在列表中的比例 × 可滚动范围
            scrollViewer.ScrollToVerticalOffset((double)index / (count - 1) * scrollViewer.ScrollableHeight);
            tvLianjie.UpdateLayout();
            var item = tvLianjie.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
            //子树展开程度不同会让粗定位落空,这时按目标在已生成节点的上方/下方分步逼近
            var step = scrollViewer.ViewportHeight * 0.6;
            for (var i = 0; item == null && i < 12 && step > 1; i++)
            {
                var down = index > FirstGeneratedIndex();
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + (down ? step : -step));
                tvLianjie.UpdateLayout();
                item = tvLianjie.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
            }
            return item;
        }

        /// <summary>
        /// 第一个已生成容器的节点序号(一个都没生成返回 -1)
        /// </summary>
        private int FirstGeneratedIndex()
        {
            for (var i = 0; i < tvLianjie.Items.Count; i++)
            {
                if (tvLianjie.ItemContainerGenerator.ContainerFromIndex(i) != null)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 软件状态文案:试用中(剩余天数)/已激活/已过期
        /// </summary>
        private string BuildLicenseText()
        {
            var reg = RegHelp.Reg;
            switch (reg.Jhzt)
            {
                case 1:
                    return "已激活";
                case 2:
                    return "已过期";
                default:
                    //到期日当天仍可使用,按自然日算剩余天数
                    var days = (reg.Gqsj.Date - DateTime.Today).Days;
                    return days > 0 ? $"试用中(剩余{days}天)" : "试用中(今天到期)";
            }
        }

        /// <summary>
        /// 授权状态悬停详情:状态/到期日/注册时间/机器码(首页右上角徽标悬停显示)
        /// </summary>
        private string BuildLicenseDetail()
        {
            var reg = RegHelp.Reg;
            var lines = new List<string>
            {
                "软件状态: " + BuildLicenseText(),
                $"到期时间: {reg.Gqsj:yyyy年MM月dd日}"
            };
            if (reg.Jhzt == 1)
            {
                lines.Add($"激活时间: {reg.Zcsj:yyyy年MM月dd日}");
            }
            lines.Add("机器码: " + RegHelp.GetJqm());
            lines.Add("点击查看激活详情");
            return string.Join(Environment.NewLine, lines);
        }

        #endregion

        /// <summary>
        /// 新建查询:根据连接树当前选中的库打开SQL编辑器标签
        /// </summary>
        private void XinJianChaXun_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(tvLianjie.SelectedItem is DbNode node) || node.NodeType == DbNodeType.Loading)
            {
                Msg.Warning("请先在左侧连接树中选择一个库");
                return;
            }
            var lj = node.LianJie;
            if (lj == null)
            {
                Msg.Warning("未找到当前节点对应的连接信息");
                return;
            }
            if (!string.IsNullOrEmpty(lj.NoDbType))
            {
                //MongoDB:打开命令窗口(从数据库节点打开时直接进该库)
                if (IsMongo(lj))
                {
                    OpenMongoQuery(node, node.NodeType == DbNodeType.MongoDb ? node.Name : lj.DataBaseName);
                    return;
                }
                //Redis:打开命令窗口(其它无库类型暂不支持)
                if (lj.NoDbType.Equals("Redis", StringComparison.OrdinalIgnoreCase))
                {
                    long db = 0;
                    if (node.NodeType == DbNodeType.RedisDb)
                    {
                        _ = long.TryParse(node.Name, out db);
                    }
                    OpenRedisConsole(node, db);
                    return;
                }
                Msg.Warning($"{lj.NoDbType} 暂不支持新建查询");
                return;
            }
            Liulan(lj, null);
        }

        /// <summary>
        /// 打开 Redis 命令窗口(同一连接只保留一个页签,从逻辑库节点打开时直接进该库)
        /// </summary>
        private void OpenRedisConsole(DbNode node, long db = 0)
        {
            OpenRedisConsole(node?.LianJie, db);
        }

        /// <summary>
        /// 打开 Redis 命令窗口(按连接打开:首页[新建查询]用)
        /// </summary>
        private void OpenRedisConsole(LianJie lj, long db = 0)
        {
            if (lj == null)
            {
                return;
            }
            var tabKey = $"redis-console|{lj.Id}|{lj.Host}|{lj.Port}";
            if (ActivateTab(tabKey))
            {
                return;
            }
            var tab = new RedisConsoleViewModel
            {
                Lj = lj,
                Db = db
            };
            var view = new RedisConsoleView
            {
                DataContext = tab
            };
            TabModel tabItem = new TabModel
            {
                Header = $"Redis命令({lj.Name})",
                Key = tabKey,
                Content = view
            };
            tabList.Add(tabItem);
            tc.ItemsSource = tabList;
            tc.SelectedIndex = tc.Items.Count - 1;
        }

        /// <summary>
        /// 打开 MongoDB 查询框(同一连接只保留一个页签)
        /// </summary>
        private void OpenMongoQuery(DbNode node, string db)
        {
            OpenMongoQuery(node?.LianJie, db);
        }

        /// <summary>
        /// 打开 MongoDB 查询框(按连接打开:首页[新建查询]用)
        /// </summary>
        private void OpenMongoQuery(LianJie lj, string db)
        {
            if (lj == null)
            {
                return;
            }
            var tabKey = $"mongo-query|{lj.Id}|{lj.Host}|{lj.Port}";
            if (ActivateTab(tabKey))
            {
                return;
            }
            var tab = new TabViewModel
            {
                Lj = string.IsNullOrEmpty(db) ? lj : Db.CreateContext(lj, db),
                //执行上下文下拉里只列 MongoDB 连接
                LjList = LianJieList.Where(p => IsMongo(p)).ToList()
            };
            var view = new TabMongoQueryView
            {
                DataContext = tab
            };
            TabModel tabItem = new TabModel
            {
                Header = $"MongoDB查询({lj.Name})",
                Key = tabKey,
                Content = view
            };
            tabList.Add(tabItem);
            tc.ItemsSource = tabList;
            tc.SelectedIndex = tc.Items.Count - 1;
        }

        /// <summary>
        /// 打开"查看数据"页(表/视图数据:单个表格 + 分页默认100条 + 筛选&排序/筛选列)
        /// </summary>
        private void OpenDataView(DbNode node)
        {
            var lj = node?.LianJie;
            var b = node?.Biao;
            if (lj == null || b == null)
            {
                return;
            }
            //同一连接下的同一表/视图已打开时直接切回
            var tabKey = BuildTableTabKey(lj, b);
            if (ActivateTab(tabKey))
            {
                return;
            }
            var tab = new TableDataViewModel
            {
                Lj = lj,
                B = b,
                SchemaName = node.SchemaName
            };
            var view = new TableDataView
            {
                DataContext = tab
            };
            var tabTitle = string.IsNullOrEmpty(lj.DataBaseName) ? lj.Name : $"{lj.Name}.{lj.DataBaseName}";
            TabModel tabItem = new TabModel
            {
                Header = $"{b.Name}({tabTitle})",
                Key = tabKey,
                Content = view
            };
            tabList.Add(tabItem);
            tc.ItemsSource = tabList;
            tc.SelectedIndex = tc.Items.Count - 1;
        }

        /// <summary>
        /// 打开"表结构"页签(只读:字段 + 建表SQL)
        /// </summary>
        public void OpenBiaoJieGouTab(LianJie lj, Biao b)
        {
            if (lj == null || b == null)
            {
                return;
            }
            var tabKey = "bjg|" + BuildTableTabKey(lj, b);
            if (ActivateTab(tabKey))
            {
                return;
            }
            var view = new BiaoJieGou(b, lj);
            var tabTitle = string.IsNullOrEmpty(lj.DataBaseName) ? lj.Name : $"{lj.Name}.{lj.DataBaseName}";
            tabList.Add(new TabModel
            {
                Header = $"{b.Name} 结构({tabTitle})",
                Key = tabKey,
                Content = view
            });
            tc.ItemsSource = tabList;
            tc.SelectedIndex = tc.Items.Count - 1;
        }

        /// <summary>
        /// 打开"表结构编辑"页签(isNew=true 为新建表;onSaved 在保存成功后回调,用于刷新数据页/树节点)
        /// </summary>
        public void OpenBiaoBianJiTab(LianJie lj, Biao b, bool isNew, Action onSaved = null)
        {
            if (lj == null || b == null)
            {
                return;
            }
            var tabKey = (isNew ? "bje-new|" : "bje|") + BuildTableTabKey(lj, b);
            if (ActivateTab(tabKey))
            {
                return;
            }
            var view = new BiaoBianJi(lj, b, isNew);
            view.Saved += () => onSaved?.Invoke();
            view.CloseRequested += () => CloseTabByContent(view);
            var tabTitle = string.IsNullOrEmpty(lj.DataBaseName) ? lj.Name : $"{lj.Name}.{lj.DataBaseName}";
            tabList.Add(new TabModel
            {
                Header = isNew ? $"新建表({tabTitle})" : $"{b.Name} 结构编辑({tabTitle})",
                Key = tabKey,
                Content = view
            });
            tc.ItemsSource = tabList;
            tc.SelectedIndex = tc.Items.Count - 1;
        }

        /// <summary>
        /// 打开 Redis/MongoDB 连接的属性页签
        /// </summary>
        public void OpenNoDbInfoTab(LianJie lj, Dictionary<string, string> info)
        {
            if (lj == null)
            {
                return;
            }
            var tabKey = "nodb-info|" + BuildNoDbTabKey(lj, null);
            if (ActivateTab(tabKey))
            {
                return;
            }
            var view = new BiaoJieGouNoDb(lj, info);
            tabList.Add(new TabModel
            {
                Header = $"{lj.Host}({lj.NoDbType}) 信息",
                Key = tabKey,
                Content = view
            });
            tc.ItemsSource = tabList;
            tc.SelectedIndex = tc.Items.Count - 1;
        }

        /// <summary>
        /// 关闭承载指定内容的页签(页签内的"取消/关闭"按钮用)
        /// </summary>
        private void CloseTabByContent(object content)
        {
            var tab = tabList.FirstOrDefault(p => ReferenceEquals(p.Content, content));
            if (tab == null)
            {
                return;
            }
            CloseTabs(tabList.IndexOf(tab), TabCloseType.Current);
        }

        /// <summary>
        /// 浏览数据
        /// </summary>
        private void Liulan(LianJie lj, Biao b)
        {
            //过期后不再限制数据库类型,仅每次访问弹窗提醒(连接数限制见 RegCheck)
            if (RegHelp.Reg.Jhzt == 2)
            {
                Msg.Warning("当前软件已过期,如需继续使用请注册!");
            }
            if (string.IsNullOrEmpty(lj.NoDbType))
            {
                if (b?.Name == null)
                {
                    var tab = new TabViewModel
                    {
                        Lj = lj,
                        //供查询页顶部的连接下拉选择:只列普通 SQL 连接(Redis/MongoDB 走各自的查询页)
                        LjList = LianJieList.Where(p => string.IsNullOrEmpty(p.NoDbType)).ToList(),
                        SqlStr = b?.TableSql
                    };
                    var tv = new TabView
                    {
                        DataContext = tab
                    };
                    var tabTitle = string.IsNullOrEmpty(lj.DataBaseName) ? lj.Name : $"{lj.Name}.{lj.DataBaseName}";
                    TabModel tabItem = new TabModel
                    {
                        Header = $"({tabTitle})",
                        Content = tv
                    };
                    tabList.Add(tabItem);
                    tc.ItemsSource = tabList;
                    tc.SelectedIndex = tc.Items.Count - 1;
                    return;
                }
                //同一连接下的同一表/视图已打开时直接切回,不再重复查询数据库
                var tabKey = BuildTableTabKey(lj, b);
                if (ActivateTab(tabKey))
                {
                    return;
                }
                Task.Factory.StartNew(
                   new Action(() =>
                   {
                       var sqlStr = Db.GetData100(lj, b.Name);
                       var ds = Db.GetDataTable(lj, sqlStr, out bool isRet, out _);
                       //字段清单:优先用已加载的(含主键信息),否则静默取一次
                       var zdList = b.ZiDuanList != null && b.ZiDuanList.Count > 0
                           ? b.ZiDuanList
                           : Db.GetZiDuans(b, lj, true);
                       Dispatcher.Invoke(new Action(() =>
                       {
                           //查询失败或结果集为空时不再处理,避免访问空表集合导致异常
                           if (!isRet || ds == null || ds.Tables.Count == 0)
                           {
                               return;
                           }
                           if (zdList == null || zdList.Count == 0)
                           {
                               //兜底:拿不到字段清单时按结果集列名构造,保证SQL补全可用(无主键信息)
                               zdList = new List<ZiDuan>();
                               foreach (DataColumn dc in ds.Tables[0].Columns)
                               {
                                   zdList.Add(new ZiDuan { Name = dc.ColumnName });
                               }
                           }
                           b.ZiDuanList = zdList;
                           var tabVm = new TabViewModel
                           {
                               Lj = lj,
                               SqlStr = sqlStr,
                               B = b
                           };
                           var tv = new TabView
                           {
                               DataContext = tabVm
                           };
                           if (isRet)
                           {
                               tv.tabList = new ObservableCollection<TabModel>();
                               foreach (DataTable t in ds.Tables)
                               {
                                   //结果集列上附字段类型/注释/主键,供结果表头显示,以及"预览/保存"生成UPDATE/DELETE的WHERE条件
                                   DataTableEditDataGrid.AttachFieldMeta(t, zdList);
                                   var tab = new DataTableEditDataGridViewModel
                                   {
                                       Lj = lj,
                                       Dt = t,
                                       //查询结果只读(与查询画面一致):改数据走"查看数据"页
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
                                   tv.tabList.Add(tabItem);
                               }
                               tv.tc.ItemsSource = tv.tabList;
                               if (tv.tabList.Count > 0)
                               {
                                   tv.tc.SelectedIndex = 0;
                               }
                           }
                           //同一连接下的同一表/视图只保留一个标签(重复双击时兜底)
                           if (ActivateTab(tabKey))
                           {
                               return;
                           }
                           TabModel tm = new TabModel
                           {
                               Header = b.Name + $"({lj.Name})",
                               Key = tabKey,
                               Content = tv
                           };
                           tabList.Add(tm);
                           tc.ItemsSource = tabList;
                           tc.SelectedIndex = tc.Items.Count - 1;
                       }));
                   }));
            }
            else
            {
                if (b == null)
                {
                    return;
                }
                //MongoDB:集合文档浏览
                if (IsMongo(lj))
                {
                    LiulanMongo(lj, b);
                    return;
                }
                //同一连接下的同一Redis逻辑库已打开时直接切回,不再重复拉取
                var redisTabKey = BuildNoDbTabKey(lj, b);
                if (ActivateTab(redisTabKey))
                {
                    return;
                }
                Task.Factory.StartNew(
                   new Action(() =>
                   {
                       try
                       {
                           var list = Db.GetRedisKey(lj, b.Name);
                           Dispatcher.Invoke(new Action(() =>
                           {
                               var tab = new TabViewModel
                               {
                                   Lj = lj,
                                   B = b,
                                   NoDbDataList = CoreConvert.Map<List<NoDbData>>(list),
                                   NoDbDataBindList = CoreConvert.Map<List<NoDbData>>(list)
                               };
                               var tv = new TabNoDbView
                               {
                                   DataContext = tab
                               };
                               //同一连接下的同一Redis逻辑库只保留一个标签(重复双击时兜底)
                               if (ActivateTab(redisTabKey))
                               {
                                   return;
                               }
                               TabModel tabItem = new TabModel
                               {
                                   Header = b.Name + $"({lj.Name})",
                                   Key = redisTabKey,
                                   Content = tv
                               };
                               tabList.Add(tabItem);
                               tc.ItemsSource = tabList;
                               tc.SelectedIndex = tc.Items.Count - 1;
                           }));
                       }
                       catch (Exception ex)
                       {
                           Log.Error(ex.Message);
                       }

                   }));

            }
        }

        /// <summary>
        /// 浏览 MongoDB 集合数据(数据表形式,同一连接下的同一集合已打开时直接切回)
        /// </summary>
        private void LiulanMongo(LianJie lj, Biao b)
        {
            var tabKey = BuildNoDbTabKey(lj, b);
            if (ActivateTab(tabKey))
            {
                return;
            }
            var tab = new MongoTableDataViewModel
            {
                Lj = lj,
                B = b
            };
            var view = new MongoTableDataView
            {
                DataContext = tab
            };
            var prefix = string.IsNullOrEmpty(lj.DataBaseName) ? string.Empty : lj.DataBaseName + ".";
            TabModel tabItem = new TabModel
            {
                Header = $"{prefix}{b.Name}({lj.Name})",
                Key = tabKey,
                Content = view
            };
            tabList.Add(tabItem);
            tc.ItemsSource = tabList;
            tc.SelectedIndex = tc.Items.Count - 1;
        }

        /// <summary>
        /// 导入连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DaoRu_OnClick(object sender, RoutedEventArgs e)
        {
            var open = new System.Windows.Forms.OpenFileDialog
            {
                Filter = @"WDB连接文件(*.connect)|*.connect"
            };
            if (open.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var list = Config.GetLianjie(open.FileName);
                if (list == null)
                {
                    Msg.Warning("连接文件为空或内容错误!");
                    return;
                }
                var d = new DaoruDaochu(list)
                {
                    Title = "导入连接",
                    Owner = this
                };
                if (d.ShowDialog() == true)
                {
                    if (d._lianjieList.Count > 0)
                    {
                        Daoru(d._lianjieList);
                    }
                    else
                    {
                        Oops.Warning("没有选择任何连接");
                    }
                }
            }
        }

        /// <summary>
        /// 导出连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DaoChu_OnClick(object sender, RoutedEventArgs e)
        {
            var d = new DaoruDaochu(LianJieList)
            {
                Title = "导出连接",
                Owner = this
            };
            if (d.ShowDialog() == true)
            {
                var save = new System.Windows.Forms.SaveFileDialog
                {
                    Filter = @"WDB连接文件(*.connect)|*.connect",
                    FileName = "WDB连接文件_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")
                };
                if (save.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (d._lianjieList.Count > 0)
                    {
                        Config.SetLianjie(d._lianjieList, save.FileName);
                        Msg.Ok(@"导出完成!");
                    }
                    else
                    {
                        Oops.Warning("没有选择任何连接");
                    }
                }
            }

        }

        /// <summary>
        /// 删除连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Shanchu_OnClick(object sender, RoutedEventArgs e)
        {
            var sc = new DaoruDaochu(LianJieList)
            {
                Title = "删除连接",
                Owner = this
            };
            sc.BtnBc.Visibility = Visibility.Collapsed;
            sc.BtnSc.Visibility = Visibility.Visible;
            if (sc.ShowDialog() == true)
            {
                foreach (var l in sc._lianjieList)
                {
                    Shuaxin(2, new LianJie { Id = l.Id });
                }
            }
        }

        /// <summary>
        /// 查看sql记录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SqlLog_Click(object sender, RoutedEventArgs e)
        {
            new SqlLogWindow { Owner = this }.Show();
        }

        private void tvLianjie_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (tvLianjie.SelectedItem != null)
                {
                    string text = tvLianjie.SelectedItem.ToString();
                    SafeClipboard.SetText(text);
                    Oops.Ok("表名已复制到剪切板");
                    e.Handled = true;
                }
            }
        }

        private enum TabCloseType
        {
            Current,
            Other,
            Left,
            Right,
            All
        }

        /// <summary>
        /// tab页右键菜单(自带菜单在非当前页上无法正常工作,这里自建)
        /// </summary>
        private void Tc_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var tabItem = FindVisualParent<TabItem>((DependencyObject)e.OriginalSource);
            if (tabItem == null)
            {
                return;
            }
            //右键时先选中该页,否则菜单操作的是别的页
            tabItem.IsSelected = true;
            var index = tc.ItemContainerGenerator.IndexFromContainer(tabItem);
            if (index < 0)
            {
                return;
            }
            var contextMenu = new ContextMenu();
            //不可关闭的标签(首页)不显示"关闭"项,避免点了没反应
            if (index < tabList.Count && tabList[index].CanClose)
            {
                contextMenu.Items.Add(CreateTabMenuItem("关闭", () => CloseTabs(index, TabCloseType.Current)));
            }
            contextMenu.Items.Add(CreateTabMenuItem("关闭其它", () => CloseTabs(index, TabCloseType.Other)));
            contextMenu.Items.Add(CreateTabMenuItem("关闭左侧", () => CloseTabs(index, TabCloseType.Left)));
            contextMenu.Items.Add(CreateTabMenuItem("关闭右侧", () => CloseTabs(index, TabCloseType.Right)));
            contextMenu.Items.Add(new Separator { Margin = new Thickness { Right = 10 } });
            contextMenu.Items.Add(CreateTabMenuItem("关闭全部", () => CloseTabs(index, TabCloseType.All)));
            contextMenu.PlacementTarget = tabItem;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }

        private MenuItem CreateTabMenuItem(string header, Action action)
        {
            var item = new MenuItem
            {
                Header = header,
                Icon = IconHelp.CreateImageIcon(IconRoot + "delete.png", 16, 16)
            };
            item.Click += (s, args) => action();
            return item;
        }

        /// <summary>
        /// 关闭 tab 页
        /// </summary>
        private void CloseTabs(int index, TabCloseType closeType)
        {
            if (index < 0 || index >= tabList.Count)
            {
                return;
            }
            var removeList = new List<TabModel>();
            switch (closeType)
            {
                case TabCloseType.Current:
                    removeList.Add(tabList[index]);
                    break;
                case TabCloseType.Other:
                    removeList.AddRange(tabList.Where((_, i) => i != index));
                    break;
                case TabCloseType.Left:
                    removeList.AddRange(tabList.Take(index));
                    break;
                case TabCloseType.Right:
                    removeList.AddRange(tabList.Skip(index + 1));
                    break;
                case TabCloseType.All:
                    removeList.AddRange(tabList);
                    break;
            }
            //首页等固定标签不参与关闭(所有关闭方式最终都走这里)
            removeList = removeList.Where(p => p.CanClose).Distinct().ToList();
            foreach (var t in removeList)
            {
                tabList.Remove(t);
            }
            if (tabList.Count > 0)
            {
                tc.SelectedIndex = Math.Min(index, tabList.Count - 1);
            }
        }

        private void tvLianjie_DragEnter(object sender, DragEventArgs e)
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

        private void tvLianjie_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // 获取拖放的文件路径数组
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null && files.Length > 0)
                {
                    // 取第一个文件（支持多文件拖放，这里只读取第一个）
                    string filePath = files[0];

                    // 读取连接
                    var list = Config.GetLianjie(filePath);
                    if (list == null)
                    {
                        Msg.Warning("连接文件为空或内容错误!");
                        return;
                    }
                    Daoru(list);
                }
            }
            catch (Exception ex)
            {
                Oops.Error(ex.Message);
            }
        }

        private void Daoru(List<LianJie> list)
        {
            var listTmp = Config.GetLianjie();

            if (listTmp == null)
            {
                listTmp = new List<LianJie>();
            }
            foreach (var l in list)
            {
                while (listTmp.FirstOrDefault(p => p.Name == l.Name && p.DataBaseType == l.DataBaseType) != null)
                {
                    l.Name += "_副本";
                }
                l.Id = StrHelp.Id;
                if (RegCheck(listTmp.Count))
                {
                    return;
                }
                listTmp.Add(l);
            }
            LianJieList = listTmp;
            InitNodes();
            Shuaxin(3);
        }

        private void GlowWindow_StateChanged(object sender, EventArgs e)
        {
            Config.IsMax = WindowState != WindowState.Normal;
        }

        private void GuanYu_OnClick(object sender, RoutedEventArgs e)
        {
            ShowGuanYu();
        }

        /// <summary>
        /// 打开关于/激活画面,回来后同步一次授权状态(首页徽标点击也走这里)
        /// </summary>
        private void ShowGuanYu()
        {
            new GuanYu { Owner = this }.ShowDialog();
            RefreshTitle();
        }

        /// <summary>
        /// 连接数限制:仅试用期已过时才只允许3个连接,试用中与已激活都不限制
        /// </summary>
        private bool RegCheck(int count)
        {
            var reg = RegHelp.Reg;
            if (reg.Jhzt != 2 || count < 3)
            {
                return false;
            }
            Msg.Warning("当前软件已过期,仅支持三个连接");
            new GuanYu { Owner = this }.ShowDialog();
            RefreshTitle();
            return true;
        }

        /// <summary>
        /// 刷新标题栏与首页上的软件状态提示 试用中(剩余天数)/已激活/已过期
        /// </summary>
        private void RefreshTitle()
        {
            var text = BuildLicenseText();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string lastTwo = $"V{version.Build}.{version.Revision} ";
            txtJhzt.Text = lastTwo + text;
            Title = $"WDB - {text}";
            _welcomeView?.SetLicenseText(text, BuildLicenseDetail());
        }
    }
}
