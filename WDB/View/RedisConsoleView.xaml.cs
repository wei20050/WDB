using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WDB.Common;

namespace WDB.View
{
    /// <summary>
    /// Redis命令窗口:上面是执行结果,下面是命令输入,回车执行(多行时逐行执行)
    /// </summary>
    public partial class RedisConsoleView : UserControl
    {
        /// <summary>
        /// 输出区最多保留的字符数(超出后砍掉前半段,避免长时间使用占用过多内存)
        /// </summary>
        private const int MaxOutputLength = 200000;

        private RedisConsoleViewModel _model;
        private readonly List<string> _history = new List<string>();
        private int _historyIndex;
        private bool _inited;

        public RedisConsoleView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inited)
            {
                return;
            }
            _model = DataContext as RedisConsoleViewModel;
            if (_model?.Lj == null)
            {
                return;
            }
            _inited = true;
            //Redis默认有16个逻辑库
            for (long i = 0; i < 16; i++)
            {
                _model.DbList.Add(i);
            }
            if (!_model.DbList.Contains(_model.Db))
            {
                _model.Db = 0;
            }
            CbDb.ItemsSource = _model.DbList;
            CbDb.SelectedItem = _model.Db;
            //保险:确保输入/输出内容都从顶部开始(部分主题样式默认会把内容垂直居中)
            TxtOut.VerticalContentAlignment = VerticalAlignment.Top;
            TxtCmd.VerticalContentAlignment = VerticalAlignment.Top;
            TxtConn.Text = $"{_model.Lj.Host}:{_model.Lj.Port}";
            Append($"{Prompt()} 已连接 {_model.Lj.Host}:{_model.Lj.Port},{Environment.NewLine}输入 Redis 命令后回车执行(支持 KEYS/GET/SET/HGETALL/INFO 等任意命令)");
            TxtCmd.Focus();
        }

        /// <summary>
        /// 命令提示符(与 redis-cli 一致)
        /// </summary>
        private string Prompt()
        {
            return $"{_model?.Lj?.Host}:{_model?.Lj?.Port}[{_model?.Db}]>";
        }

        private void CbDb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_model != null && CbDb.SelectedItem is long db)
            {
                _model.Db = db;
            }
        }

        private void TxtCmd_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_model == null)
            {
                return;
            }
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                //回车执行(Shift+Enter 换行)
                e.Handled = true;
                Zhixing();
                return;
            }
            //单行输入时用上下键翻历史命令
            if (TxtCmd.Text.IndexOf('\n') >= 0 || Keyboard.Modifiers != ModifierKeys.None)
            {
                return;
            }
            if (e.Key == Key.Up)
            {
                e.Handled = true;
                MoveHistory(-1);
            }
            else if (e.Key == Key.Down)
            {
                e.Handled = true;
                MoveHistory(1);
            }
        }

        private void Zhixing_Click(object sender, RoutedEventArgs e)
        {
            Zhixing();
        }

        private void Qingkong_Click(object sender, RoutedEventArgs e)
        {
            TxtOut.Clear();
            TxtCmd.Clear();
            TxtCmd.Focus();
        }

        /// <summary>
        /// 执行输入框里的命令(多行时逐行执行,以 # 或 -- 开头的行当注释跳过)
        /// </summary>
        private void Zhixing()
        {
            var text = TxtCmd.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                TxtCmd.Focus();
                return;
            }
            var lines = text.Replace("\r\n", "\n").Split('\n')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
            if (lines.Count == 0)
            {
                return;
            }
            //记历史(同一条不重复记)
            if (_history.Count == 0 || _history[_history.Count - 1] != text.Trim())
            {
                _history.Add(text.Trim());
            }
            _historyIndex = _history.Count;
            TxtCmd.Clear();
            TxtCmd.Focus();

            var lj = _model.Lj;
            var db = _model.Db;
            SetLoading(true);
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    var sb = new StringBuilder();
                    var currentDb = db;
                    foreach (var cmd in lines)
                    {
                        if (cmd.StartsWith("#") || cmd.StartsWith("--"))
                        {
                            sb.AppendLine(cmd);
                            continue;
                        }
                        sb.AppendLine($"{lj.Host}:{lj.Port}[{currentDb}]> {cmd}");
                        var watch = Stopwatch.StartNew();
                        var ret = Db.ExecuteRedis(lj, currentDb, cmd, out var isError);
                        watch.Stop();
                        if (isError)
                        {
                            sb.AppendLine($"(error) {ret}");
                        }
                        else
                        {
                            sb.AppendLine(string.IsNullOrEmpty(ret) ? "(empty)" : ret);
                        }
                        sb.AppendLine($"  [{watch.ElapsedMilliseconds} ms]");
                        //执行 SELECT 后,后面的命令按新库执行
                        if (!isError && cmd.StartsWith("select", StringComparison.OrdinalIgnoreCase))
                        {
                            var arr = Db.SplitRedisCommand(cmd);
                            if (arr.Count > 1 && long.TryParse(arr[1], out var newDb))
                            {
                                currentDb = newDb;
                            }
                        }
                    }
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetLoading(false);
                        if (currentDb != _model.Db)
                        {
                            //用户执行了 SELECT,同步工具栏的库下拉
                            if (!_model.DbList.Contains(currentDb))
                            {
                                _model.DbList.Add(currentDb);
                            }
                            _model.Db = currentDb;
                            CbDb.SelectedItem = currentDb;
                        }
                        Append(sb.ToString().TrimEnd());
                    }));
                }));
        }

        private void MoveHistory(int step)
        {
            if (_history.Count == 0)
            {
                return;
            }
            _historyIndex = Math.Max(0, Math.Min(_history.Count, _historyIndex + step));
            TxtCmd.Text = _historyIndex >= _history.Count ? string.Empty : _history[_historyIndex];
            TxtCmd.CaretIndex = TxtCmd.Text.Length;
        }

        /// <summary>
        /// 追加输出并滚到底部
        /// </summary>
        private void Append(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (TxtOut.Text.Length > 0)
            {
                TxtOut.AppendText(Environment.NewLine);
            }
            TxtOut.AppendText(text);
            if (TxtOut.Text.Length > MaxOutputLength)
            {
                TxtOut.Text = TxtOut.Text.Substring(TxtOut.Text.Length - MaxOutputLength / 2);
            }
            TxtOut.ScrollToEnd();
        }

        private void SetLoading(bool visible)
        {
            LoadingG.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
