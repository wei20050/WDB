using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WDB.Common;
using WDB.Entity;

namespace WDB.View
{
    /// <summary>
    /// AI 助手面板:对话式提问,展开在编辑器右侧。
    /// 顶部是"相关表(集合)"多选下拉,勾选的结构随问题一起发给 AI;
    /// 底部是输入框 + 模型下拉,两者都会记进配置,下次打开面板按配置回显。
    /// 一律走"生成语句"模式(名称由宿主定,如"SQL技能"/"MongoDB技能"):带上下文与表结构,只出语句,并把回复里的语句回填给宿主的编辑器。
    /// 对话历史留在面板里,可以连续追问。
    /// </summary>
    public partial class AiChatView : UserControl
    {
        /// <summary>发给模型的历史条数上限:太长既慢又容易被网关截断</summary>
        private const int MaxHistory = 20;

        private bool _busy;

        /// <summary>对话内容(界面绑它)</summary>
        private readonly ObservableCollection<AiChatItem> _items = new ObservableCollection<AiChatItem>();

        /// <summary>发给模型的历史(含本次提问;本次提问失败时撤掉)</summary>
        private readonly List<AIHelp.message> _history = new List<AIHelp.message>();

        #region 宿主注入的钩子

        /// <summary>生成语句技能:主线程回调,宿主在这里抓当前库/表上下文的快照(提问前调用)</summary>
        public Action CaptureContext { get; set; }

        /// <summary>
        /// 后台线程回调:宿主按提问文本(与顶部勾选的相关表名)拼出本次请求。
        /// 每次提问都会调(面板已统一走"生成语句"模式),返回 null 时只发问题原文。
        /// </summary>
        public Func<string, List<string>, AiChatAsk> BuildAsk { get; set; }

        /// <summary>生成语句技能:AI 回复里的语句(主线程回调),由宿主写进自己的编辑器</summary>
        public Action<string> SqlReady { get; set; }

        /// <summary>标题栏[收起]点击</summary>
        public event Action CloseRequested;

        /// <summary>顶部下拉的标签(如"相关表:"/"相关集合:"),在构造后设置即可</summary>
        public string TableLabel
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    LbTable.Text = value.Trim();
                }
            }
        }

        /// <summary>当前勾选的相关表(集合)</summary>
        public List<string> CheckedTables
        {
            get
            {
                var list = new List<string>();
                foreach (var item in CcbTable.SelectedItems)
                {
                    var table = item as AiChatTable;
                    if (table != null && !string.IsNullOrEmpty(table.Name) && !list.Contains(table.Name))
                    {
                        list.Add(table.Name);
                    }
                }
                return list;
            }
        }

        /// <summary>
        /// 顶部"相关表(集合)"下拉:填入候选项。已经勾选的尽量保住(清单刷新不会把用户的勾选弄丢),
        /// 还没勾选时才用默认勾选(默认勾选一般给"打开该页时所在的那张表")
        /// </summary>
        public void SetTables(List<string> names, List<string> defaultChecked = null)
        {
            var keep = CheckedTables;
            var items = new List<AiChatTable>();
            var list = names ?? new List<string>();
            foreach (var name in list)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    items.Add(new AiChatTable { Name = name });
                }
            }
            //先清掉旧选中再换数据源,免得旧对象留在 SelectedItems 里(下面按名字重新勾上)
            CcbTable.SelectedItems.Clear();
            CcbTable.ItemsSource = items;
            foreach (var name in keep.Count > 0 ? keep : (defaultChecked ?? new List<string>()))
            {
                var hit = items.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (hit != null && !CcbTable.SelectedItems.Contains(hit))
                {
                    CcbTable.SelectedItems.Add(hit);
                }
            }
        }

        #endregion

        public AiChatView()
        {
            InitializeComponent();
            IcChat.ItemsSource = _items;
            var models = Config.AiModels ?? new List<AiModel>();
            CbModel.ItemsSource = models;
            //选中"最后一次使用的"模型(配置里记着),没有记录或记录已失效时用第一个
            var lastId = Config.AiModelId;
            CbModel.SelectedItem = models.Find(p => p.Id == lastId)
                                   ?? (models.Count > 0 ? models[0] : null);
            CbModel.SelectionChanged += CbModel_SelectionChanged;
        }

        /// <summary>
        /// 切换模型:记为"最后一次使用"(设置页与面板下次打开都载入它)
        /// </summary>
        private void CbModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbModel.SelectedItem is AiModel model)
            {
                Config.AiModelId = model.Id;
            }
        }

        /// <summary>
        /// 设置里改完模型配置后由外部调用:重读 Config.AiModels 刷新下拉,尽量保住当前选中项
        /// </summary>
        public void RefreshModels()
        {
            var models = Config.AiModels ?? new List<AiModel>();
            //优先保住当前选中的模型(改完名字后 Id 不变);当前为空时回退到"最后一次使用"的
            var keepId = (CbModel.SelectedItem as AiModel)?.Id ?? Config.AiModelId;
            CbModel.ItemsSource = models;
            CbModel.SelectedItem = models.Find(p => p.Id == keepId)
                ?? (models.Count > 0 ? models[0] : null);
        }

        /// <summary>展开面板后把焦点放进输入框</summary>
        public void FocusInput()
        {
            //刚展开时面板还没完成布局,直接 Focus 会落空
            _ = Dispatcher.BeginInvoke(new Action(() => TxtInput.Focus()), DispatcherPriority.Loaded);
        }

        #region 提问

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            Send();
        }

        private void Send()
        {
            if (_busy)
            {
                return;
            }
            var need = (TxtInput.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(need))
            {
                TxtInput.Focus();
                return;
            }
            var ai = CbModel.SelectedItem as AiModel ?? Config.AiModelCurrent();
            if (ai == null || string.IsNullOrWhiteSpace(ai.Url) || string.IsNullOrWhiteSpace(ai.Model) )
            {
                AddItem("请先在设置里配置 AI 模型(名称/接口地址/模型)", false, true);
                return;
            }
            //上下文快照与勾选的相关表都在主线程取好,后台线程只用快照
            var tables = CheckedTables;
            if (CaptureContext != null)
            {
                CaptureContext();
            }
            AddItem(need, true);
            TxtInput.Clear();
            _history.Add(new AIHelp.message { role = "user", content = need });
            //先放一个空的 AI 气泡,流式边收边显示
            var reply = AddStreamItem();
            SetBusy(true);
            _ = Task.Factory.StartNew(new Action(() =>
            {
                var sb = new StringBuilder();
                var lastFlush = DateTime.MinValue;
                try
                {
                    //一律走"生成语句":调宿主的 BuildAsk 拼库/表结构,并替换最后一条历史消息再发
                    var ask = BuildAsk != null ? BuildAsk(need, tables) : null;
                    var messages = new List<AIHelp.message>(_history);
                    if (ask != null && !string.IsNullOrWhiteSpace(ask.User) && messages.Count > 0)
                    {
                        //只把最后一条换成带库/表结构的提示词,历史仍按原文保留
                        messages[messages.Count - 1] = new AIHelp.message { role = "user", content = ask.User };
                    }
                    //生成语句用宿主给的系统提示词
                    var system = ask != null && !string.IsNullOrWhiteSpace(ask.System)
                        ? ask.System
                        : AIHelp.ChatSystem;
                    //生成语句温度给低值,回答更快更稳
                    AIHelp.ChatMixedStream(ai.Url, ai.Model, ai.ApiKey, system, messages, delta =>
                    {
                        sb.Append(delta);
                        //每 ~50ms 刷一次界面,避免逐字刷导致卡顿
                        var now = DateTime.UtcNow;
                        if ((now - lastFlush).TotalMilliseconds < 50)
                        {
                            return;
                        }
                        lastFlush = now;
                        var snapshot = sb.ToString();
                        Dispatcher.BeginInvoke(new Action(() => UpdateStreaming(reply, snapshot)));
                    }, ask?.Temperature ?? 0.2, ask?.MaxTokens ?? 1500);

                    var str = sb.ToString();
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetBusy(false);
                        if (string.IsNullOrWhiteSpace(str))
                        {
                            //一个字都没回来:这条气泡改成错误提示
                            reply.IsError = true;
                            reply.Content = "AI 没有返回内容";
                            DropLastUserMessage();
                            return;
                        }
                        reply.Content = str;
                        _history.Add(new AIHelp.message { role = "assistant", content = str });
                        TrimHistory();
                        //语句统一在结束时回填编辑器,避免半截 SQL 写进去
                        WriteSql(str);
                    }));
                }
                catch (Exception ex)
                {
                    Log.Error("AI访问异常:" + ex);
                    var partial = sb.ToString();
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetBusy(false);
                        DropLastUserMessage();
                        if (string.IsNullOrWhiteSpace(partial))
                        {
                            reply.IsError = true;
                            reply.Content = "AI 访问失败,请检查设置里的接口地址/模型/密钥:" + ex.Message;
                        }
                        else
                        {
                            //已经吐了一部分:保留内容并注明中断
                            reply.Content = partial + Environment.NewLine + Environment.NewLine + "(输出中断:" + ex.Message + ")";
                        }
                    }));
                }
            }));
        }

        /// <summary>请求失败时撤掉最后一条用户历史,下次追问不会带一个没有回答的问题</summary>
        private void DropLastUserMessage()
        {
            if (_history.Count > 0 && _history[_history.Count - 1].role == "user")
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }

        /// <summary>新增一条空的 AI 气泡(给流式输出用)</summary>
        private AiChatItem AddStreamItem()
        {
            var item = new AiChatItem { Content = string.Empty };
            _items.Add(item);
            ScrollToEnd();
            return item;
        }

        /// <summary>流式过程中的一次刷新:只改这条气泡的文字</summary>
        private void UpdateStreaming(AiChatItem item, string text)
        {
            item.Content = text;
            TxtThinking.Visibility = Visibility.Collapsed;
            ScrollToEnd();
        }

        /// <summary>
        /// SQL技能:把回复里的语句交给宿主写进编辑器(认不出语句就只留在对话里,不动编辑器)
        /// </summary>
        private void WriteSql(string reply)
        {
            var sql = AIHelp.ExtractSql(reply);
            if (SqlReady != null && !string.IsNullOrWhiteSpace(sql))
            {
                SqlReady(sql);
            }
        }

        /// <summary>
        /// 请求期间禁用发送,并在对话末尾显示等待提示
        /// </summary>
        private void SetBusy(bool busy)
        {
            _busy = busy;
            BtnSend.IsEnabled = !busy;
            TxtThinking.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            ScrollToEnd();
        }

        private void TrimHistory()
        {
            while (_history.Count > MaxHistory)
            {
                _history.RemoveAt(0);
            }
        }

        #endregion

        #region 对话内容

        private void AddItem(string content, bool isUser, bool isError = false)
        {
            _items.Add(new AiChatItem { Content = content ?? string.Empty, IsUser = isUser, IsError = isError });
            ScrollToEnd();
        }

        /// <summary>
        /// 滚到底部:等布局跑完再滚,否则新加的消息还没排出来,滚不到底
        /// </summary>
        private void ScrollToEnd()
        {
            _ = Dispatcher.BeginInvoke(new Action(() => Scroller.ScrollToEnd()), DispatcherPriority.Background);
        }

        /// <summary>
        /// 提取完整提示词:把本次会发送给 AI 的内容(系统提示词 + 历史 + 带表结构的提问 + 参数)
        /// 拼成纯文本复制到剪贴板,方便贴到网页版大模型里
        /// </summary>
        private void BtnExtractPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (_busy)
            {
                Msg.Warning("等这次回答结束再提取");
                return;
            }
            var need = (TxtInput.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(need))
            {
                //输入框为空时按"最后一次提问"提取,方便复盘已发过的内容;
                //一次都没提过也没关系:照样把当前库/表结构提取出来
                var lastUser = _history.LastOrDefault(p => p.role == "user");
                need = (lastUser?.content ?? string.Empty).Trim();
            }
            //上下文快照要在主线程抓,拼提示词放到后台(缺字段时可能要查库,别卡界面)
            var tables = CheckedTables;
            if (CaptureContext != null)
            {
                CaptureContext();
            }
            BtnExtract.IsEnabled = false;
            _ = Task.Factory.StartNew(new Action(() =>
            {
                string text = null;
                Exception err = null;
                try
                {
                    text = BuildPromptText(need, tables);
                }
                catch (Exception ex)
                {
                    err = ex;
                }
                Dispatcher.Invoke(new Action(() =>
                {
                    BtnExtract.IsEnabled = true;
                    if (err != null)
                    {
                        Log.Error("提取提示词失败:" + err);
                        Msg.Error("提取提示词失败:" + err.Message);
                        return;
                    }
                    try
                    {
                        //与项目其它复制处一致:走 Win32 的安全剪贴板,避免剪贴板被占用时抛异常
                        SafeClipboard.SetText(text);
                        Msg.Ok("完整提示词已复制到剪贴板,可直接粘贴到其它大模型");
                    }
                    catch (Exception ex)
                    {
                        Log.Error("复制提示词失败:" + ex);
                        Msg.Error("复制失败:" + ex.Message);
                    }
                }));
            }));
        }

        /// <summary>
        /// 拼"会发出去的全部内容"。与 Send 的请求口径保持一致:
        /// 最后一条用户历史换成宿主 BuildAsk 生成的带库/表结构的提示词。
        /// 后台线程调用,这里不能访问任何界面控件
        /// </summary>
        private string BuildPromptText(string need, List<string> tables)
        {
            var ask = BuildAsk != null ? BuildAsk(need, tables) : null;
            var system = ask != null && !string.IsNullOrWhiteSpace(ask.System) ? ask.System : AIHelp.ChatSystem;
            var sb = new StringBuilder();

            sb.AppendLine("【系统提示词】");
            sb.AppendLine(system);
            sb.AppendLine();

            //历史里最后一条若正是本轮提问,统一挪到"本次提问"里(那里是带表结构的版本)
            var history = _history.Where(p => p != null && !string.IsNullOrWhiteSpace(p.content)).ToList();
            if (history.Count > 0 && history[history.Count - 1].role == "user" &&
                string.Equals((history[history.Count - 1].content ?? string.Empty).Trim(), need, StringComparison.Ordinal))
            {
                history.RemoveAt(history.Count - 1);
            }
            if (history.Count > 0)
            {
                sb.AppendLine("【对话历史】");
                foreach (var m in history)
                {
                    sb.AppendLine(m.role == "assistant" ? "AI:" : "我:");
                    sb.AppendLine((m.content ?? string.Empty).Trim());
                    sb.AppendLine();
                }
            }

            sb.AppendLine("【本次提问】");
            var question = ask != null && !string.IsNullOrWhiteSpace(ask.User) ? ask.User.Trim() : need;
            if (string.IsNullOrWhiteSpace(question))
            {
                question = "(尚未输入问题:以上为当前数据库上下文,请在此补充需求)";
            }
            sb.AppendLine(question);
            return sb.ToString().TrimEnd() + Environment.NewLine;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0 || !Msg.Question("清空当前对话?"))
            {
                return;
            }
            _items.Clear();
            _history.Clear();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke();
        }

        private void TxtInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                //Ctrl+Enter 换行(AcceptsReturn 的默认行为,这里显式插入,和"Enter 发送"配对)
                var box = (TextBox)sender;
                box.SelectedText = Environment.NewLine;
            }
            else
            {
                Send();
            }
            e.Handled = true;
        }

        #endregion
    }

    /// <summary>
    /// 顶部"相关表(集合)"下拉的一项(CheckComboBox 按 Name 显示与取值,与旧AI窗口同一写法)
    /// </summary>
    public class AiChatTable
    {
        public string Name { get; set; }
    }

    /// <summary>
    /// 对话里的一条消息:自己问的在右,AI 答的在左;出错的那条用红字。
    /// Content/IsError 支持变更通知:流式输出时只改文字,不重建界面
    /// </summary>
    public class AiChatItem : INotifyPropertyChanged
    {
        private string _content;

        public string Content
        {
            get => _content;
            set
            {
                if (_content == value)
                {
                    return;
                }
                _content = value;
                OnPropertyChanged(nameof(Content));
            }
        }

        private bool _isError;

        public bool IsError
        {
            get => _isError;
            set
            {
                if (_isError == value)
                {
                    return;
                }
                _isError = value;
                OnPropertyChanged(nameof(IsError));
            }
        }

        public bool IsUser { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// SQL技能下要发给模型的一次请求(由宿主页面组装)
    /// </summary>
    public class AiChatAsk
    {
        /// <summary>系统提示词(只在"生成语句"技能下生效,普通对话固定用问答提示词)</summary>
        public string System { get; set; }

        /// <summary>用户提示词:已含方言、库/模式与表结构</summary>
        public string User { get; set; }

        /// <summary>采样温度:生成语句给低值</summary>
        public double? Temperature { get; set; } = 0.2;

        /// <summary>最大输出长度</summary>
        public int? MaxTokens { get; set; } = 1500;
    }
}
