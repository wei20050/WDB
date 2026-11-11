using HandyControl.Data;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    public partial class BiaoJieGou
    {

        BiaoJieGouModel _biaoJieGouModel;
        public CompletionWindow completionWindow;

        public BiaoJieGou(Biao b, LianJie lj)
        {
            InitializeComponent();
            _biaoJieGouModel = (BiaoJieGouModel)DataContext;
            _biaoJieGouModel.LianjieMing = lj.Name + $" ( host: {lj.Host} database: {lj.DataBaseName} username: {lj.UserName} )";
            _biaoJieGouModel.BiaoMing = b.Name;
            _biaoJieGouModel.BiaoZhuShi = b.Comment;
            _biaoJieGouModel.ZiDuanList = Db.GetZiDuans(b, lj);
            if (!b.IsView)
            {
                b.TableSql = Db.GetDDL(b, lj);
            }
            _biaoJieGouModel.SqlStr = b.TableSql.SqlFormat();
            b.ZiDuanList = _biaoJieGouModel.ZiDuanList;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TextEditor.TextArea.TextEntered += TextArea_TextEntered;
            HighlightingProvider.Register(SkinType.Dark, new HighlightingProviderDark());
            TextEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "SQL");
            TextEditor.TextArea.SelectionCornerRadius = 0;
            TextEditor.TextArea.SelectionBorder = null;
            TextEditor.TextArea.SelectionForeground = null;
            TextEditor.TextArea.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFADD6FF"));
        }
        private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            #region MyRegion
            if (completionWindow != null)
            {
                return;
            }
            var enterText = e.Text;
            // 这里可以加入更加智能的条件，例如判断前一个单词是不是SQL关键字。
            if (char.IsLetter(enterText[0]) || enterText == " ")
            {
                ShowCompletion(enterText);
            }
            #endregion
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="enteredText"></param>
        /// <param name="controlSpace"></param>
        private void ShowCompletion(string enteredText)
        {
            #region MyRegion
            var textArea = TextEditor.TextArea;
            var caret = textArea.Caret;
            var document = textArea.Document;

            // 获取光标前的文本
            var lineText = document.GetText(document.GetLineByNumber(caret.Line));
            var textUpToCaret = lineText.Substring(0, caret.Column - 1);

            // 最简单的上下文分析: 分析光标前的非空字符
            // 实践中这可能要复杂得多，可能涉及到语法和词法的分析。
            var lastWord = GetLastWord(textUpToCaret);

            completionWindow = new CompletionWindow(TextEditor.TextArea);
            completionWindow.BorderThickness = new Thickness(0);
            IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
            // 根据用户已经键入的文字，筛选出匹配的 SQL 关键字
            foreach (var keyword in GConst.Sys_SqlKeywords)
            {
                if (keyword.IndexOf(enteredText, StringComparison.OrdinalIgnoreCase) >= 0 || enteredText == " ")
                {
                    data.Add(new SqlCompletionData(keyword));
                }
            }
            foreach (var fun in GConst.Sys_SqlFuns)
            {
                if (fun.IndexOf(enteredText, StringComparison.OrdinalIgnoreCase) >= 0 || enteredText == " ")
                {
                    data.Add(new SqlCompletionData(fun, "Func"));
                }
            }

            if (data.Count == 0)
            {
                // 如果没有匹配项，则不需要显示无内容的智能提示窗口
                completionWindow.Close();
                return;
            }
            completionWindow.Show();
            completionWindow.Closed += delegate
            {
                completionWindow = null;
            };
            completionWindow.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Space && !IsFocused)
                {
                    completionWindow.Close();
                }
            };
            // 默认选择第一项
            completionWindow.CompletionList.SelectedItem = data[0];
            completionWindow.Width = 550;
            var listBox = completionWindow.CompletionList.ListBox;

            listBox.Margin = new Thickness(0);
            // 设置ListBox的边框样式
            listBox.BorderThickness = new Thickness(0); // 设置边框粗细
            // 创建新的样式以应用于每个ListBox项
            var style = new Style(typeof(ListBoxItem));
            style.BasedOn = listBox.ItemContainerStyle; // 从ListBox的ItemContainerStyle继承样式
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 25.0)); // 设置ListBoxItem的高度

            // 创建触发器，以便在项被选中时更改背景色
            Trigger trigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            trigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#aee1ff"))));
            trigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000"))));
            style.Triggers.Add(trigger);
            // 应用样式到ListBox的每个项
            listBox.ItemContainerStyle = style;
            #endregion
        }

        private string GetLastWord(string text)
        {
            var words = text.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return words.Length > 0 ? words[words.Length - 1] : string.Empty;
        }

        private void Meihua_Click(object sender, RoutedEventArgs e)
        {
            var sqlScript = TextEditor.Text;
            if (string.IsNullOrEmpty(sqlScript))
            {
                return;
            }
            if (string.IsNullOrEmpty(TextEditor.SelectedText))
            {
                TextEditor.Text = sqlScript.SqlFormat();
                return;
            }
            var oldSqlScript = TextEditor.SelectedText;
            sqlScript = TextEditor.SelectedText;
            TextEditor.Text = TextEditor.Text.Replace(oldSqlScript, sqlScript.SqlFormat());
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
            saveFileDialog.Filter = "SQL 文件(*.sql)|*.sql|所有文件(*.*)|*.*|文本文件(*.txt)|*.txt"; // 设置保存文件的筛选器
            saveFileDialog.FileName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var fileName = saveFileDialog.FileName;
                TextEditor.Save(fileName);
            }
        }

        /// <summary>
        /// 复制内容
        /// </summary>
        /// <param name="type">0原值 1小驼峰 2大驼峰</param>
        private void Fz(int type = 0)
        {
            var cellsText = UIHelp.GetSelectedCellsPrecise(dg);
            if (string.IsNullOrEmpty(cellsText))
            {
                Oops.Ok("没有可复制的内容");
            }
            else
            {
                if (type == 1)
                {
                    cellsText = cellsText.ToCamelCase();
                }
                else if (type == 2)
                {
                    cellsText = cellsText.ToPascalCase();
                }
                SafeClipboard.SetText(cellsText);
                Oops.Ok("单元格内容已复制");
            }
        }

        private void FzXtf_Click(object sender, RoutedEventArgs e)
        {
            Fz(1);
        }

        private void FzDtf_Click(object sender, RoutedEventArgs e)
        {
            Fz(2);
        }

        /// <summary>
        /// 复制内容
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Fz_Click(object sender, RoutedEventArgs e)
        {
            Fz();
        }
    }
}
