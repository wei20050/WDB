using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WDB.Common;

namespace WDB.Window.Tools
{
    public partial class UcHex : UserControl
    {
        public UcHex()
        {
            InitializeComponent();
            DataContext = this;
            cbCode.SelectedIndex = 0;
            cbUp.SelectedIndex = 0;
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
            var inputText = txtInput.Text;
            if (inputText == string.Empty)
            {
                return;
            }
            TextOutput.Text = StrHelp.EncodeStringToHex(inputText, Encoding.GetEncoding(cbCode.Text), cbUp.SelectedIndex == 0);
        }

        /// <summary>
        /// 解码
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnDecode_Click(object sender, RoutedEventArgs e)
        {
            var inputText = txtInput.Text;
            if (inputText == string.Empty)
            {
                return;
            }
            try
            {
                TextOutput.Text = StrHelp.DecodeHexToString(inputText, Encoding.GetEncoding(cbCode.Text));
            }
            catch (Exception ex)
            {
                TextOutput.Text = ex.Message;
            }
        }

        private void BtnExchange_Click(object sender, RoutedEventArgs e)
        {
            var inputText = txtInput.Text;
            var outputText = TextOutput.Text;
            if (inputText == string.Empty && outputText == string.Empty)
            {
                return;
            }
            txtInput.Text = outputText;
            TextOutput.Text = inputText;
        }

    }
}
