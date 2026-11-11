using System.Windows;
using System.Windows.Controls;
using WDB.Common;

namespace WDB.Window.Tools
{
    /// <summary>
    /// MainContent.xaml 的交互逻辑
    /// </summary>
    public partial class UcMD5 : UserControl
    {
        public UcMD5()
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

        private void BtnEncrypt_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtInput.Text))
            {
                Oops.Warning("请输入加密文本");
                return;
            }
            var textU32 = CoreEncrypt.Md5(txtInput.Text);
            var textU16 = CoreEncrypt.Md516(txtInput.Text);
            TextOutputU16.Text = textU16;
            TextOutputL16.Text = textU16.ToLower();
            TextOutputU32.Text = textU32;
            TextOutputL32.Text = textU32.ToLower();
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var btnCopy = (Button)sender;
            var copyText = string.Empty;
            switch (btnCopy.Tag)
            {
                case "L16":
                    copyText = TextOutputL16.Text; break;
                case "U16":
                    copyText = TextOutputU16.Text; break;
                case "L32":
                    copyText = TextOutputL32.Text; break;
                case "U32":
                    copyText = TextOutputU32.Text; break;
            }
            if (copyText != string.Empty)
            {
                SafeClipboard.SetText(copyText);
                Oops.Ok("文本已复制到剪切板");
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtInput.Text = string.Empty;
        }
    }
}
