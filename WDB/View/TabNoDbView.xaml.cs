using HandyControl.Data;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WDB.Common;
using WDB.Entity;

namespace WDB.View
{
    public partial class TabNoDbView : UserControl
    {
        TabViewModel _model;
        private DispatcherTimer _timer = new DispatcherTimer();
        public CompletionWindow completionWindow;
        public TabNoDbView()
        {
            InitializeComponent();

            HighlightingProvider.Register(SkinType.Dark, new HighlightingProviderDark());
            TextEditor.TextArea.TextView.ElementGenerators.Add(new TruncateLongLines());
            TextEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "Json");
            TextEditor.TextArea.SelectionCornerRadius = 0;
            TextEditor.TextArea.SelectionBorder = null;
            TextEditor.TextArea.SelectionForeground = null;
            TextEditor.TextArea.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFADD6FF"));
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _model = DataContext as TabViewModel;
            CbZidong.IsChecked = false;
            if (_model == null)
            {
                return;
            }
            if (_model.NoDbDataEdit == null)
            {
                xinzeng();
            }
            else
            {
                TextEditor.Text = _model.NoDbDataEdit.Value;
                labShuaxin.Content = DateTime.Now.ToString("HH:mm:ss");
                if (_model.NoDbDataEdit.IsEdit)
                {
                    LabZhuangtai.Content = "修改";
                    LabZhuangtai.Foreground = Brushes.DarkOrange;
                    txtKeyEdit.IsEnabled = false;
                }
                else
                {
                    LabZhuangtai.Content = "新增";
                    LabZhuangtai.Foreground = Brushes.DarkGreen;
                    txtKeyEdit.IsEnabled = true;
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, ex) =>
            {
                if (CbZidong.IsChecked == true)
                {
                    shuaxin();
                }
            };
            _timer.Start();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
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
            saveFileDialog.Filter = "所有文件(*.*)|*.*|文本文件(*.txt)|*.txt";
            saveFileDialog.FileName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var fileName = saveFileDialog.FileName;
                TextEditor.Save(fileName);
            }
        }

        /// <summary>
        /// 新增数据
        /// </summary>
        private void xinzeng()
        {
            _model.NoDbDataEdit = new NoDbData { Ttl = -1, Type = "string" };
            TextEditor.Text = string.Empty;
            txtKeyEdit.IsEnabled = true;
            LabZhuangtai.Content = "新增";
            LabZhuangtai.Foreground = Brushes.DarkGreen;
            labShuaxin.Content = DateTime.Now.ToString("HH:mm:ss");
        }

        /// <summary>
        /// 查询数据
        /// </summary>
        private void chaxun()
        {
            if (_model == null)
            {
                return;
            }
            //数据尚未加载时置空列表,避免对 null 调用 FindAll 抛异常
            if (_model.NoDbDataList == null)
            {
                _model.NoDbDataBindList = new List<NoDbData>();
                return;
            }
            var tmpList = CoreConvert.Map<List<NoDbData>>(_model.NoDbDataList) ?? new List<NoDbData>();
            _model.NoDbDataBindList = tmpList.FindAll(p => p.Key != null && p.Key.Contains(_model.NoDbKey ?? string.Empty));
        }

        /// <summary>
        /// 刷新数据
        /// </summary>
        private void shuaxin()
        {
            labShuaxin.Content = DateTime.Now.ToString("HH:mm:ss");
            try
            {
                if (_model != null)
                {
                    if (_model.NoDbDataEdit != null && _model.NoDbDataEdit.IsEdit)
                    {
                        NoDbData noDbData = Db.GetRedisData(_model.Lj, _model.B.Name, _model.NoDbDataEdit?.Key);
                        _model.NoDbDataEdit = noDbData;
                        _model.NoDbDataEdit.IsEdit = true;
                        LabZhuangtai.Content = "修改";
                        LabZhuangtai.Foreground = Brushes.DarkOrange;
                        txtKeyEdit.IsEnabled = false;
                        TextEditor.Text = _model.NoDbDataEdit.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Oops.Warning(ex.Message);
            }
        }

        private void BtnChaxun_Click(object sender, RoutedEventArgs e)
        {
            _model.NoDbDataList = Db.GetRedisKey(_model.Lj, _model.B.Name);
            chaxun();
        }

        /// <summary>
        /// 选中redis键 加载数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dg_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (_model != null)
            {
                if (dg.SelectedItem is NoDbData data)
                {
                    _model.NoDbDataEdit = data;
                    _model.NoDbDataEdit.IsEdit = true;
                    shuaxin();
                }
            }
        }

        private void BtnShuaxin_Click(object sender, RoutedEventArgs e)
        {
            shuaxin();
        }

        private void BtnXinzen(object sender, RoutedEventArgs e)
        {
            xinzeng();
        }

        private void BtnBaocun(object sender, RoutedEventArgs e)
        {
            var data = _model.NoDbDataEdit;
            if (string.IsNullOrEmpty(data?.Key))
            {
                Oops.Warning("请输入 key");
                return;
            }
            if (data.Type != "string")
            {
                if (Msg.Question("非string类型数据仅支持修改TTL,确认修改将只改变TTL") == false)
                {
                    return;
                }
            }
            Db.SetRedisData(_model.Lj, _model.B.Name, data);
            xinzeng();
            _model.NoDbDataList = Db.GetRedisKey(_model.Lj, _model.B.Name);
            chaxun();
        }

        private void BtnShanchu(object sender, RoutedEventArgs e)
        {
            //按行去重(同一行选中多个单元格时避免重复键)
            var items = new List<NoDbData>();
            foreach (var item in dg.SelectedItems)
            {
                if (item is NoDbData data && !items.Any(p => p.Key == data.Key))
                {
                    items.Add(data);
                }
            }
            if (items.Count == 0)
            {
                Oops.Warning("请选择要删除的行");
                return;
            }
            if (Msg.Question("确定删除吗? 此操作无法恢复!"))
            {
                var keys = items.Select(p => p.Key).ToArray();
                Db.DeleteRedisData(_model.Lj, _model.B.Name, keys);
                xinzeng();
                _model.NoDbDataList = Db.GetRedisKey(_model.Lj, _model.B.Name);
                chaxun();
            }
        }

        private void txtKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            chaxun();
        }

        private void TextEditor_TextChanged(object sender, EventArgs e)
        {
            if (_model != null && _model.NoDbDataEdit != null)
            {
                _model.NoDbDataEdit.Value = TextEditor.Text;
            }
        }

        /// <summary>
        /// 复制内容
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Fz_Click(object sender, RoutedEventArgs e)
        {
            var cellsText = UIHelp.GetSelectedCellsPrecise(dg);
            if (string.IsNullOrEmpty(cellsText))
            {
                Oops.Ok("没有可复制的内容");
            }
            else
            {
                SafeClipboard.SetText(cellsText);
                Oops.Ok("单元格内容已复制");
            }
        }
    }
}
