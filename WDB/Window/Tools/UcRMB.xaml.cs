using System.Windows;
using System.Windows.Controls;
using WDB.Common;

namespace WDB.Window.Tools
{
    public partial class UcRMB : UserControl
    {
        public UcRMB()
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
        /// 清理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtInput.Text = string.Empty;
            TextOutput.Text = string.Empty;
        }

        private void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            var inputText = txtInput.Text;
            if (string.IsNullOrEmpty(inputText))
            {
                return;
            }
            var inputMoney = 0d;
            if (!double.TryParse(inputText, out inputMoney))
            {
                Oops.Warning("包含无效的字符");
                return;
            }
            if (inputMoney > 9999999999999.99)
            {
                Oops.Warning("超出最大金额限制万亿级别");
                return;
            }
            TextOutput.Text = RMBHelper.ToChineseRMB(inputMoney);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            SafeClipboard.SetText(TextOutput.Text);
            Oops.Ok("大写金额已复制");
        }
    }
}
