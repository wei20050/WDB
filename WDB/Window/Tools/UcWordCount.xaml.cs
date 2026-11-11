using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using HandyControl.Data;
using WDB.Common;

namespace WDB.Window.Tools
{
    public partial class UcWordCount : UserControl
    {
        public UcWordCount()
        {
            InitializeComponent();
            DataContext = this;
            HighlightingProvider.Register(SkinType.Dark, new HighlightingProviderDark());
            TextEditor.TextArea.TextView.ElementGenerators.Add(new TruncateLongLines());
            TextEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "Tex-Mode.xshd");
            TextEditor.TextArea.SelectionCornerRadius = 0;
            TextEditor.TextArea.SelectionBorder = null;
            TextEditor.TextArea.SelectionForeground = null;
            TextEditor.TextArea.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFADD6FF"));
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
            catch (Exception)
            {
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
        /// 复制文本
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TextEditor.Text))
            {
                TextEditor.SelectAll();
                SafeClipboard.SetText(TextEditor.Text);
                Oops.Ok("文本已复制到剪切板");
            }
        }

        /// <summary>
        /// 清空输入框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TextEditor.Text = string.Empty;
        }

        /// <summary>
        /// 编辑器获取焦点自动粘贴剪切板文本
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextEditor_GotFocus(object sender, RoutedEventArgs e)
        {
            var clipboardText = Clipboard.GetText();
            if (string.IsNullOrEmpty(TextEditor.Text) && !string.IsNullOrEmpty(clipboardText))
            {
                var isTryParse = false;
                try
                {
                    ///JsonDocument.Parse(clipboardText); 
                    isTryParse = true;
                }
                catch { }
                if (isTryParse)
                {
                    TextEditor.Text = clipboardText;
                }
            }
        }

        private void TextEditor_OnTextChanged(object sender, EventArgs e)
        {
            int iAllChr = 0; //字符总数：不计字符'\n'和'\r'
            int iChineseChr = 0; //中文字符计数
            int iChinesePnct = 0;//中文标点计数
            int iEnglishChr = 0; //英文字符计数
            int iEnglishPnct = 0;//中文标点计数
            int iNumber = 0;  //数字字符：0-9
            foreach (char ch in TextEditor.Text)
            {
                if (ch != '\n' && ch != '\r')
                {
                    iAllChr++;
                }
                if ("～！＠＃￥％…＆（）—＋－＝".IndexOf(ch) != -1 || "｛｝【】：“”；‘'《》，。、？｜＼".IndexOf(ch) != -1)
                {
                    iChinesePnct++;
                }
                if (ch >= 0x4e00 && ch <= 0x9fbb)
                {
                    iAllChr++;
                    iChineseChr++;
                }
                if ("`~!@#$%^&*()_+-={}[]:\";'<>,.?/\\|".IndexOf(ch) != -1)
                {
                    iEnglishPnct++;
                }
                if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
                {
                    iEnglishChr++;
                }
                if (ch >= '0' && ch <= '9')
                {
                    iNumber++;
                }
            }

            ShieldHanZi.Status = iChineseChr;
            ShieldZiMu.Status = iEnglishChr;
            ShieldShuZi.Status = iNumber;
            ShieldBiaoDian.Status = iChinesePnct + iEnglishPnct;
            ShieldTotalZiShu.Status = iChineseChr + iEnglishChr;
            ShieldTotalZiFu.Status = iAllChr;
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
    }
}
