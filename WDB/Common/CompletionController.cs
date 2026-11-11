using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WDB.Common
{
    /// <summary>
    /// AvalonEdit 智能提示的通用外壳:负责触发时机、取词、弹框与关闭
    /// 候选项由外部委托按"光标前正在输入的词"提供,便于 SQL / MongoDB 等不同语言复用
    /// </summary>
    public class CompletionController
    {
        private readonly TextEditor _editor;
        private CompletionWindow _window;

        /// <summary>
        /// 生成候选项:入参为光标前正在输入的整词(可能含点号,如 db.users),返回候选项(返回空则不弹框)
        /// </summary>
        public Func<string, IList<ICompletionData>> Provider { get; set; }

        public CompletionController(TextEditor editor)
        {
            _editor = editor;
            _editor.TextArea.TextEntered += TextArea_TextEntered;
        }

        /// <summary>
        /// 光标前正在输入的词(由字母/数字/下划线/$/. 组成)
        /// </summary>
        public string GetWordBeforeCaret()
        {
            var doc = _editor?.Document;
            if (doc == null)
            {
                return string.Empty;
            }
            var end = _editor.CaretOffset;
            var start = end;
            while (start > 0 && IsWordChar(doc.GetCharAt(start - 1)))
            {
                start--;
            }
            return doc.GetText(start, end - start);
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '.';
        }

        private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
            {
                return;
            }
            //先关掉旧提示框:否则旧列表被过滤空之后会留一个空白框
            Close();
            var c = e.Text[0];
            var isTypingWord = char.IsLetterOrDigit(c) || c == '_' || c == '$';
            if (!isTypingWord && c != '.' && c != ' ')
            {
                return;
            }
            //延后一帧:确保刚输入的字符已经进入文档,再按光标前的文本取词
            _ = _editor.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    //智能提示出问题不影响编辑,单独兜住并记日志
                    try
                    {
                        Show();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"智能提示异常:{ex}");
                    }
                }),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void Show()
        {
            var data = Provider?.Invoke(GetWordBeforeCaret() ?? string.Empty);
            if (data == null || data.Count == 0)
            {
                Close();
                return;
            }
            ShowWindow(data);
        }

        /// <summary>
        /// 关闭当前提示框
        /// </summary>
        public void Close()
        {
            var win = _window;
            _window = null;
            win?.Close();
        }

        private void ShowWindow(IList<ICompletionData> data)
        {
            //同一帧内可能排了多次重建,打开前先关掉旧的,保证同一时间只有一个提示框
            Close();
            _window = new CompletionWindow(_editor.TextArea)
            {
                BorderThickness = new Thickness(0)
            };
            var list = _window.CompletionList.CompletionData;
            foreach (var item in data)
            {
                list.Add(item);
            }
            _window.Show();
            _window.Closed += delegate
            {
                _window = null;
            };
            var editor = _editor;
            _window.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Space && !editor.IsFocused)
                {
                    //空格结束当前提示框,并为"show 空格"这类位置重新给出候选
                    Close();
                }
            };
            //默认选择第一项
            _window.CompletionList.SelectedItem = data[0];
            _window.Width = 550;
            var listBox = _window.CompletionList.ListBox;
            listBox.Margin = new Thickness(0);
            listBox.BorderThickness = new Thickness(0);
            var style = new Style(typeof(ListBoxItem))
            {
                BasedOn = listBox.ItemContainerStyle
            };
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 25.0));
            var trigger = new Trigger
            {
                Property = ListBoxItem.IsSelectedProperty,
                Value = true
            };
            trigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#aee1ff"))));
            trigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000"))));
            style.Triggers.Add(trigger);
            listBox.ItemContainerStyle = style;
        }
    }
}
