using ICSharpCode.AvalonEdit;
using System;
using System.Windows;

namespace WDB.Common
{
    public static class AvalonEditBindings
    {
        // 定义附加属性，用于绑定Text
        public static readonly DependencyProperty BoundTextProperty =
            DependencyProperty.RegisterAttached(
                "BoundText",
                typeof(string),
                typeof(AvalonEditBindings),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnBoundTextChanged,
                    CoerceBoundText
                )
            );

        // 获取附加属性值
        public static string GetBoundText(DependencyObject obj)
        {
            return (string)obj.GetValue(BoundTextProperty);
        }

        // 设置附加属性值
        public static void SetBoundText(DependencyObject obj, string value)
        {
            obj.SetValue(BoundTextProperty, value);
        }

        // 附加属性值变化时，同步到TextEditor的Text
        private static void OnBoundTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextEditor editor)
            {
                // 避免循环触发（当TextEditor自身Text变化时，不重复赋值）
                if (editor.Text != e.NewValue?.ToString())
                {
                    editor.Text = e.NewValue?.ToString() ?? string.Empty;
                }

                // 订阅TextEditor的TextChanged事件，反向同步到附加属性
                editor.TextChanged -= OnEditorTextChanged;
                editor.TextChanged += OnEditorTextChanged;
            }
        }

        // TextEditor的Text变化时，同步回附加属性
        private static void OnEditorTextChanged(object sender, EventArgs e)
        {
            if (sender is TextEditor editor)
            {
                SetBoundText(editor, editor.Text);
            }
        }

        // 强制转换值（可选，增强健壮性）
        private static object CoerceBoundText(DependencyObject d, object value)
        {
            return value ?? string.Empty;
        }
    }
}
