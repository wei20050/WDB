using System;
using System.Windows;
using System.Windows.Controls;
using WDB.Common;

namespace WDB.Window.Tools
{
    public partial class UcBase64 : UserControl
    {
        public UcBase64()
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
            var inputText = txtInput.Text;
            if (inputText == string.Empty)
            {
                return;
            }
            var rText = StrHelp.Base46_Encode(inputText);
            if (ChkUrlCodec.IsChecked == true)
            {
                rText = System.Net.WebUtility.UrlEncode(rText);
            }
            TextOutput.Text = rText;
        }

        private void BtnDecode_Click(object sender, RoutedEventArgs e)
        {
            var inputText = txtInput.Text;
            if (inputText == string.Empty)
            {
                return;
            }
            try
            {
                if (ChkUrlCodec.IsChecked == true)
                {
                    inputText = System.Net.WebUtility.UrlDecode(inputText);
                }
                var rText = StrHelp.Base46_Decode(inputText);
                TextOutput.Text = rText;
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
