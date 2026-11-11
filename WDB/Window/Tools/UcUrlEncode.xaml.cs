using System.Windows;
using System.Windows.Controls;

namespace WDB.Window.Tools
{
    public partial class UcUrlEncode : UserControl
    {
        public UcUrlEncode()
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

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtInput.Text = string.Empty;
            TextOutput.Text = string.Empty;
        }

        /// <summary>
        /// 编码
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnEncode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtInput.Text))
            {
                return;
            }
            var rText = System.Net.WebUtility.UrlEncode(txtInput.Text);
            TextOutput.Text = rText;
        }

        /// <summary>
        /// 解码
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnDecode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtInput.Text))
            {
                return;
            }
            TextOutput.Text = System.Net.WebUtility.UrlDecode(txtInput.Text);
        }
    }
}
