using ICSharpCode.AvalonEdit.Editing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WDB.Common;

namespace WDB.Window.Tools
{
    public partial class ToolBox
    {
        public ToolBox()
        {
            InitializeComponent();
            DataContext = this;
            Keyboard.AddPreviewKeyDownHandler(this, (s, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {

                    if (e.Key == Key.C)
                    {
                        IInputElement t = FocusManager.GetFocusedElement(this);
                        if (t is TextBox tb)
                        {
                            SafeClipboard.SetText(tb.SelectedText);
                            e.Handled = true;
                        }
                        else if (t is RichTextBox rtb)
                        {
                            SafeClipboard.SetText(rtb.Selection.Text);
                            e.Handled = true;
                        }
                        else if (t is TextArea ta)
                        {
                            if (!ta.Selection.IsEmpty)
                            {
                                SafeClipboard.SetText(ta.Selection.GetText());
                            }
                            e.Handled = true;
                        }
                        else if (t is PasswordBox pb)
                        {
                            SafeClipboard.SetText(pb.Password);
                            e.Handled = true;
                        }
                        if (t is DataGridCell cell)
                        {
                            var text = UIHelp.GetSelectedCellsPrecise(UIHelp.GetOwnerDataGrid(cell));
                            SafeClipboard.SetText(text);
                            e.Handled = true;
                        }
                    }
                    if (e.Key == Key.X)
                    {
                        var t = FocusManager.GetFocusedElement(this);
                        if (t is TextBox tb)
                        {
                            SafeClipboard.SetText(tb.SelectedText);
                            tb.SelectedText = "";
                            e.Handled = true;
                        }
                        else if (t is RichTextBox rtb)
                        {
                            SafeClipboard.SetText(rtb.Selection.Text);
                            rtb.Selection.Text = "";
                            e.Handled = true;
                        }
                        else if (t is TextArea ta)
                        {
                            if (!ta.Selection.IsEmpty)
                            {
                                string text = ta.Selection.GetText();
                                SafeClipboard.SetText(text);
                                ta.Document.Replace(ta.Selection.SurroundingSegment, "");
                            }
                            e.Handled = true;
                        }
                    }
                }
            });
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        }
    }
}
