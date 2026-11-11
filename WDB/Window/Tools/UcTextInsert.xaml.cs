using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WDB.Common;

namespace WDB.Window.Tools
{
    /// <summary>
    /// MainContent.xaml 的交互逻辑
    /// </summary>
    public partial class UcTextInsert : UserControl
    {
        public UcTextInsert()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>
        /// 返回
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnReturn_Click(object sender, RoutedEventArgs e)
        {
            var parentWindow = (ToolBox)System.Windows.Window.GetWindow(this);
            parentWindow.UcBox.Content = new MainTools();
        }

        /// <summary>
        /// 清除文本
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtInput.Text = string.Empty;
            TextOutput.Text = string.Empty;
            TextInputStart.Text = string.Empty;
            TextInputEnd.Text = string.Empty;
            TextSurplusNum.Text = "0";
            TextLine.Text = "0";
        }

        /// <summary>
        /// 文本变更事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextInput_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var surplusTextNums = txtInput.Text.Length;
            TextSurplusNum.Text = surplusTextNums.ToString();

            if (!string.IsNullOrEmpty(txtInput.Text))
            {
                var outText = new StringBuilder();
                var inputText = txtInput.Text.Trim();
                string[] lineSplit = inputText.Split(new[] { "\n" }, StringSplitOptions.None);
                TextLine.Text = lineSplit.Count().ToString();
                var count = 0;
                foreach (var item in lineSplit)
                {
                    var itemValue = item;
                    if (string.IsNullOrEmpty(item))
                    {
                        continue;
                    }
                    if (item.Contains("\r"))
                    {
                        itemValue = item.Replace("\r", "");
                    }
                    count++;
                    if (string.IsNullOrEmpty(TextInputStart.Text.Trim()) && string.IsNullOrEmpty(TextInputEnd.Text.Trim()))
                    {
                        outText.Append("'" + itemValue + "',\n");
                    }
                    else
                    {
                        outText.Append(TextInputStart.Text.Trim() + itemValue + TextInputEnd.Text.Trim() + "\n");
                    }
                }
                var result = lineSplit.Length > 1 ? outText.ToString().TrimEnd(new char[] { ',', '\n' }) : outText.ToString();
                TextOutput.Text = result;
                TextOutput.SelectAll();
            }
        }

        /// <summary>
        /// 复制
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCopy_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextOutput.Text))
            {
                return;
            }
            TextOutput.SelectAll();
            SafeClipboard.SetText(TextOutput.Text);
            Oops.Ok("文本已复制到剪切板");
        }

        /// <summary>
        /// 格式化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnFormat_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextOutput.Text))
            {
                return;
            }
            TextOutput.Text = TextOutput.Text.Replace("\n", "");
            TextOutput.SelectAll();
        }
    }
}
