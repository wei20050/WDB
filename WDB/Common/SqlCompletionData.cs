using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls;

namespace WDB.Common
{
    public class SqlCompletionData : ICompletionData
    {
        private readonly double _priority;

        public SqlCompletionData(string text, string objType = null, string desc = null, double priority = 0)
        {
            Text = text;
            Desc = desc;
            _priority = priority;
            SetItem(objType);
        }
        // 智能提示的文本内容
        public string Text { get; private set; }
        //赋值默认图标
        public ImageSource Image { get; private set; }

        // 在提示列表中用于排序的属性(值越大越靠前)
        public double Priority => _priority;

        //设置智能提示的内容
        public object Content { get; private set; }
        public object Description { get; private set; }
        // 智能提示的描述文本
        public string Desc { get; set; }
        // 智能提示的文本颜色
        public SolidColorBrush Foreground { get; private set; }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            //替换光标前正在输入的整个单词(不含前面的限定符点号):
            //u.na -> u.name 这类带别名的补全、以及提示框中途重开时都不会错位
            var document = textArea.Document;
            var end = Math.Max(completionSegment.EndOffset, textArea.Caret.Offset);
            var start = end;
            while (start > 0 && IsWordChar(document.GetCharAt(start - 1)))
            {
                start--;
            }
            document.Replace(start, end - start, Text);
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$';
        }

        private void SetItem(string objType)
        {
            #region MyRegion
            var black = new SolidColorBrush(Colors.Black);
            var blue = new SolidColorBrush(Colors.Blue);
            var funColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fd03fd"));
            switch (objType)
            {
                case "Table":
                    Image = new BitmapImage(new Uri($"pack://application:,,,/WDB;component/Resources/table.png"));
                    Content = ItemGrid();
                    break;
                case "View":
                    Image = new BitmapImage(new Uri($"pack://application:,,,/WDB;component/Resources/view.png"));
                    Content = new TextBlock() { Text = Text, Foreground = black };
                    break;
                case "Col":
                case "Field":
                    Image = new BitmapImage(new Uri($"pack://application:,,,/WDB;component/Resources/col.png"));
                    Content = new TextBlock() { Text = Text, Foreground = black };
                    break;
                case "Collection":
                    Image = new BitmapImage(new Uri($"pack://application:,,,/WDB;component/Resources/table.png"));
                    Content = new TextBlock() { Text = Text, Foreground = black };
                    break;
                case "Func":
                    Content = new TextBlock() { Text = Text, Foreground = funColor };
                    break;
                default:
                    Content = new TextBlock() { Text = Text, Foreground = blue };
                    break;
            }
            #endregion
        }

        private Grid ItemGrid()
        {
            #region MyRegion
            var black = new SolidColorBrush(Colors.Black);
            var grid = new Grid();
            grid.Width = 500;
            grid.Children.Add(new TextBlock()
            {
                Text = Text,
                Foreground = black,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            });
            grid.Children.Add(new TextBlock()
            {
                Text = Desc,
                Margin = new System.Windows.Thickness(20, 0, 0, 0),
                Foreground = new SolidColorBrush(Colors.Gray),
                TextTrimming = System.Windows.TextTrimming.WordEllipsis,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            });
            return grid;
            #endregion
        }
    }
}
