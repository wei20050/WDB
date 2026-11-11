using HandyControl.Data;
using System;
using System.Windows;
using System.Windows.Media;
using WDB.Common;

namespace WDB.Window
{
    public partial class WenBen
    {
        public string _nr;

        /// <summary>
        /// 查看模式(readOnly=true):只展示内容,隐藏保存按钮、换成"复制"(查询结果只读时右键"查看"用)
        /// </summary>
        public WenBen(string nr, bool readOnly = false)
        {
            InitializeComponent();
            TextEditor.Text = nr;
            if (readOnly)
            {
                Title = "内容查看";
                BtnSave.Visibility = Visibility.Collapsed;
                BtnCopy.Visibility = Visibility.Visible;
            }

            HighlightingProvider.Register(SkinType.Dark, new HighlightingProviderDark());
            TextEditor.TextArea.TextView.ElementGenerators.Add(new TruncateLongLines());
            TextEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "Json");
            TextEditor.TextArea.SelectionCornerRadius = 0;
            TextEditor.TextArea.SelectionBorder = null;
            TextEditor.TextArea.SelectionForeground = null;
            TextEditor.TextArea.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFADD6FF"));
            TextEditor.Focus();
            TextEditor.SelectionStart = TextEditor.Text.Length;
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
            saveFileDialog.Filter = "文本文件(*.txt)|*.txt|所有文件(*.*)|*.*"; // 设置保存文件的筛选器
            saveFileDialog.FileName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var fileName = saveFileDialog.FileName;
                TextEditor.Save(fileName);
            }
        }

        /// <summary>
        /// 格式化Json
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnFormatter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextEditor.Text))
                {
                    Oops.Warning("请输入Json文本");
                    return;
                }
                TextEditor.Text = StrHelp.JsonFormatter(TextEditor.Text);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Oops.Warning("Json解析失败，请检查");
            }
        }

        /// <summary>
        /// 压缩Json
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCompress_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextEditor.Text))
                {
                    Oops.Warning("请输入Json文本");
                    return;
                }
                TextEditor.Text = StrHelp.JsonCompress(TextEditor.Text);
            }
            catch (Exception)
            {
                Oops.Warning("Json解析失败，请检查");
            }
        }

        private void BtnClearScript_Click(object sender, RoutedEventArgs e)
        {
            TextEditor.Text = string.Empty;
        }

        /// <summary>
        /// 复制全部内容到剪切板(查看模式的按钮;右键"复制"仍是复制选中部分):
        /// 复制完关掉查看窗口,再提示已复制(提示跟着活动窗口走,延后一帧落在主窗口上)
        /// </summary>
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            SafeClipboard.SetText(TextEditor.Text);
            Close();
            Dispatcher.BeginInvoke(new Action(() => Oops.Ok("内容已复制到剪切板")), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _nr = TextEditor.Text;
            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        }
    }
}
