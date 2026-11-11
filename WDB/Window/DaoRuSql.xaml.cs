using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    /// <summary>
    /// 导入SQL到数据库(带进度与导入日志)
    /// </summary>
    public partial class DaoRuSql
    {
        /// <summary>
        /// 界面日志最多保留的条数(超过后只保留失败项,避免界面堆积)
        /// </summary>
        private const int MaxLog = 5000;

        /// <summary>
        /// 界面日志的硬上限(含失败项)
        /// </summary>
        private const int MaxLogHard = 8000;

        /// <summary>
        /// 单次刷新最多写入界面的条数(控制单次界面线程占用,避免进度条卡顿)
        /// </summary>
        private const int MaxPerFlush = 50;

        /// <summary>
        /// 后台产生的日志缓冲上限(界面来不及刷新时丢弃普通项,保留失败项)
        /// </summary>
        private const int MaxBuffer = 10000;

        private readonly LianJie _lj;
        private readonly ObservableCollection<SqlImportLogItem> _logs = new ObservableCollection<SqlImportLogItem>();
        private readonly List<SqlImportLogItem> _logBuffer = new List<SqlImportLogItem>();
        private readonly object _logLock = new object();
        private DispatcherTimer _logTimer;
        private CancellationTokenSource _cts;
        private bool _isBusy;
        private bool _logCapped;
        private int _lastPercent = -1;
        private DateTime _lastScroll = DateTime.MinValue;

        /// <summary>
        /// 导入SQL窗口
        /// </summary>
        /// <param name="lj">连接上下文(已定位到目标库/模式)</param>
        /// <param name="name">导入目标名称(库名/模式名/连接名)</param>
        public DaoRuSql(LianJie lj, string name)
        {
            InitializeComponent();
            _lj = lj;
            Title = $"导入SQL [{name}]";
            gbMain.Header = $"导入到 [{name}] ({lj?.DataBaseType})";
            lbLog.ItemsSource = _logs;
        }

        private void BtnXuanZe(object sender, RoutedEventArgs e)
        {
            var open = new System.Windows.Forms.OpenFileDialog
            {
                Filter = @"SQL文件(*.sql)|*.sql|所有文件(*.*)|*.*"
            };
            if (open.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            txtFile.Text = open.FileName;
            tbStatus.Text = "已选择文件,点击开始导入";
            pb.Value = 0;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
            {
                return;
            }
            var path = txtFile.Text;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Oops.Warning("请先选择要导入的SQL文件");
                return;
            }
            string sql;
            try
            {
                //自动识别 BOM,兼容 UTF8/GBK 等编码文件
                using (var sr = new StreamReader(path, Encoding.UTF8, true))
                {
                    sql = sr.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Msg.Warning($"读取文件失败:{Environment.NewLine}{ex.Message}");
                return;
            }
            var sqlList = SqlImport.Split(sql);
            if (sqlList.Count == 0)
            {
                Msg.Warning("未在文件中解析到可执行的SQL语句");
                return;
            }
            var continueOnError = cbContinue.IsChecked == true;
            StartImport(sqlList, continueOnError);
        }

        /// <summary>
        /// 后台导入并刷新日志与进度
        /// </summary>
        private void StartImport(List<string> sqlList, bool continueOnError)
        {
            _isBusy = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _logs.Clear();
            _logCapped = false;
            _lastPercent = -1;
            _lastScroll = DateTime.MinValue;
            lock (_logLock)
            {
                _logBuffer.Clear();
            }
            BtnStart.IsEnabled = false;
            BtnCancel.Visibility = Visibility.Visible;
            BtnCancel.IsEnabled = true;
            pb.Value = 0;
            tbSummary.Text = $"共 {sqlList.Count} 条语句";
            AddLog($"解析到 {sqlList.Count} 条语句,开始导入...",
                $"每个表最多记录 {SqlImport.PerObjectLogLimit} 条成功日志,失败语句全部记录", false);
            //日志按固定间隔批量刷新,避免每条语句都往界面线程投递造成积压
            if (_logTimer == null)
            {
                _logTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, LogTimer_Tick, Dispatcher);
            }
            _logTimer.Start();
            _ = Task.Factory.StartNew(
                new Action(() =>
                {
                    SqlImport.Result r = null;
                    string error = null;
                    try
                    {
                        r = SqlImport.Run(_lj, sqlList, continueOnError, token, Report, LogItem);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                        error = ex.Message;
                    }
                    Dispatcher.Invoke(
                        new Action(() =>
                        {
                            //先停掉定时器并补齐剩余日志,保证界面与统计一致
                            _logTimer?.Stop();
                            FlushLog(true);
                            _isBusy = false;
                            BtnStart.IsEnabled = true;
                            BtnCancel.Visibility = Visibility.Collapsed;
                            if (error != null)
                            {
                                tbStatus.Text = "导入失败";
                                AddLog("导入失败", error, true);
                                Msg.Warning($"导入失败:{Environment.NewLine}{error}");
                                return;
                            }
                            var msg = $"共 {r.Total} 条,成功 {r.Success} 条,失败 {r.Fail} 条" +
                                      $",影响 {r.Rows} 行,耗时 {StrHelp.ToTimeString(r.Milliseconds)}" +
                                      (r.Cancelled ? " (已取消)" : string.Empty);
                            pb.Value = 100;
                            tbStatus.Text = msg;
                            tbSummary.Text = msg;
                            AddLog(r.Cancelled ? "已取消导入" : "导入完成", msg, r.Fail > 0);
                            Msg.Ok($"导入完成:{Environment.NewLine}{msg}");
                        }));
                }));
        }

        /// <summary>
        /// 进度回调(后台线程调用):百分比未变化时直接跳过,整个导入最多投递100次
        /// </summary>
        private void Report(int percent, string msg)
        {
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

        /// <summary>
        /// 日志回调(后台线程调用):只入缓冲,由定时器批量刷新到界面
        /// </summary>
        private void LogItem(SqlImportLogItem item)
        {
            lock (_logLock)
            {
                //界面严重滞后时丢弃普通项,保留失败项
                if (_logBuffer.Count >= MaxBuffer && !item.IsError)
                {
                    return;
                }
                _logBuffer.Add(item);
            }
        }

        /// <summary>
        /// 定时批量刷新日志
        /// </summary>
        private void LogTimer_Tick(object sender, EventArgs e)
        {
            FlushLog();
        }

        /// <summary>
        /// 把缓冲中的日志批量写入界面
        /// </summary>
        /// <param name="all">true:一次全部写完(导入结束时用)</param>
        private void FlushLog(bool all = false)
        {
            List<SqlImportLogItem> batch;
            lock (_logLock)
            {
                if (_logBuffer.Count == 0)
                {
                    return;
                }
                //每次只取一部分,避免单次占用界面线程过久导致进度条卡顿
                var take = all ? _logBuffer.Count : Math.Min(_logBuffer.Count, MaxPerFlush);
                batch = _logBuffer.GetRange(0, take);
                _logBuffer.RemoveRange(0, take);
            }
            foreach (var item in batch)
            {
                if (_logs.Count >= MaxLog)
                {
                    if (!_logCapped)
                    {
                        _logCapped = true;
                        _logs.Add(new SqlImportLogItem
                        {
                            Time = DateTime.Now.ToString("HH:mm:ss"),
                            Text = $"日志已达上限 {MaxLog} 条,后续只在列表中记录失败项(统计仍包含全部语句)"
                        });
                    }
                    if (!item.IsError || _logs.Count >= MaxLogHard)
                    {
                        continue;
                    }
                }
                _logs.Add(item);
            }
            //ScrollIntoView 会触发布局计算,限流到 1 秒一次,避免每次刷新都卡一下进度条
            var now = DateTime.Now;
            if (_logs.Count > 0 && (all || now - _lastScroll > TimeSpan.FromSeconds(1)))
            {
                _lastScroll = now;
                lbLog.ScrollIntoView(_logs[_logs.Count - 1]);
            }
        }

        private void AddLog(string text, string detail, bool isError)
        {
            _logs.Add(new SqlImportLogItem
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Text = text,
                Detail = detail,
                IsError = isError
            });
            lbLog.ScrollIntoView(_logs[_logs.Count - 1]);
        }

        /// <summary>
        /// 双击日志:打开内容编辑画面查看完整内容
        /// </summary>
        private void LbLog_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(lbLog.SelectedItem is SqlImportLogItem item))
            {
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"时间: {item.Time}");
            if (!string.IsNullOrEmpty(item.Detail))
            {
                sb.AppendLine(item.Detail);
            }
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrEmpty(item.Sql) ? item.Text : item.Sql);
            var f = new WenBen(sb.ToString())
            {
                Owner = this
            };
            _ = f.ShowDialog();
        }

        private void BtnQuXiao(object sender, RoutedEventArgs e)
        {
            if (!_isBusy)
            {
                return;
            }
            _cts?.Cancel();
            tbStatus.Text = "正在取消,等待当前语句结束...";
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
            if (!Msg.Question("正在导入,确认要取消并关闭吗?"))
            {
                e.Cancel = true;
                return;
            }
            _logTimer?.Stop();
            _cts?.Cancel();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !_isBusy)
            {
                Close();
            }
        }
    }
}
