using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit;

namespace WDB.Common
{
    /// <summary>
    /// 全局文本编辑右键菜单。
    ///
    /// TextBoxBase(含 HandyControl 的 hc:TextBox)统一挂中文"复制 / 粘贴 / 删除",
    /// 替掉 WPF 自带那套英文 Cut / Copy / Paste;
    /// AvalonEdit 编辑器只统一其中"删除"的可用性(各页面自己的菜单项原样保留)。
    ///
    /// 为什么用类处理器而不是隐式 Style:
    /// 隐式样式一旦存在就会整体接管控件外观,要保留 HandyControl 的外观必须 BasedOn 回它自己的样式;
    /// 而 hc:TextBox 的 DefaultStyleKey 是它自己,按 TextBox 写的隐式样式对它根本不生效,
    /// 得给 TextBox 和 hc:TextBox 各写一份 BasedOn 才能覆盖,风险大且容易踩到资源解析顺序。
    /// 类处理器对所有派生类自动生效,且不碰样式体系。
    /// </summary>
    public static class TextEditContextMenu
    {
        /// <summary>
        /// 菜单项文案。刷新可用性时靠它匹配各页面已有的菜单项,
        /// 所以这几个词必须与项目里现有的 Header 完全一致。
        /// </summary>
        private const string CopyHeader = "复制";
        private const string PasteHeader = "粘贴";
        private const string DeleteHeader = "删除";

        private static bool _attached;

        public static void Attach()
        {
            if (_attached)
            {
                return;
            }
            _attached = true;

            // 加载时就把菜单挂上,保证第一次右键拿到的就是中文菜单,
            // 不依赖下面那两条 ContextMenuOpening 的执行时机
            EventManager.RegisterClassHandler(
                typeof(TextBoxBase),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnTextBoxLoaded));
            EventManager.RegisterClassHandler(
                typeof(TextEditor),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnEditorLoaded));

            // 每次弹出前刷新"删除"的可用性。
            // 类处理器是先注册后执行,这里比 TextBoxBase 自带的默认菜单逻辑注册得晚,
            // 所以轮到它时 TextBoxBase.ContextMenu 已经被 Ensure 填好了。
            EventManager.RegisterClassHandler(
                typeof(TextBoxBase),
                FrameworkElement.ContextMenuOpeningEvent,
                new ContextMenuEventHandler(OnTextBoxMenuOpening));
            EventManager.RegisterClassHandler(
                typeof(TextEditor),
                FrameworkElement.ContextMenuOpeningEvent,
                new ContextMenuEventHandler(OnEditorMenuOpening));
        }

        #region TextBoxBase(含 hc:TextBox)

        private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            EnsureTextBoxMenu(sender as TextBoxBase);
        }

        private static void OnTextBoxMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var textBox = sender as TextBoxBase;
            if (textBox == null)
            {
                return;
            }

            EnsureTextBoxMenu(textBox);
            RefreshDeleteItem(textBox.ContextMenu, HasSelection(textBox), textBox.IsReadOnly);
        }

        /// <summary>
        /// 是否有选中内容。
        /// TextBoxBase 上没有 SelectedText / SelectionLength / SelectionStart(已查文档确认),
        /// 这几个成员由 TextBox、RichTextBox 各自实现,只能按具体类型取。
        /// 写法与 MainWindow 里处理 Ctrl+C / Ctrl+X 的一致。
        /// </summary>
        private static bool HasSelection(TextBoxBase textBox)
        {
            var plain = textBox as TextBox;
            if (plain != null)
            {
                return plain.SelectionLength > 0;
            }

            var rich = textBox as RichTextBox;
            if (rich != null)
            {
                // 用 Selection.Text 判断,而不是 SelectionLength / Selection.IsEmpty:
                // 后两者没查到确证,Selection.Text 项目里已经在用(MainWindow 的 Ctrl+C)
                return !string.IsNullOrEmpty(rich.Selection.Text);
            }

            return false;
        }

        /// <summary>取选中文本(同 HasSelection,TextBoxBase 上没有 SelectedText)</summary>
        private static string GetSelectedText(TextBoxBase textBox)
        {
            var plain = textBox as TextBox;
            if (plain != null)
            {
                return plain.SelectedText;
            }

            var rich = textBox as RichTextBox;
            if (rich != null)
            {
                return rich.Selection.Text;
            }

            return string.Empty;
        }

        /// <summary>清空选中内容(同 HasSelection,TextBoxBase 上没有 SelectedText)</summary>
        private static void ClearSelection(TextBoxBase textBox)
        {
            var plain = textBox as TextBox;
            if (plain != null)
            {
                plain.SelectedText = string.Empty;
                return;
            }

            var rich = textBox as RichTextBox;
            if (rich != null)
            {
                rich.Selection.Text = string.Empty;
            }
        }

        /// <summary>
        /// 挂上中文菜单。已有自定义菜单时不动 —— 这里为 null 说明脚下是
        /// WPF 自带那套英文 Cut / Copy / Paste。
        /// </summary>
        private static void EnsureTextBoxMenu(TextBoxBase textBox)
        {
            if (textBox == null || textBox.ContextMenu != null)
            {
                return;
            }

            textBox.ContextMenu = BuildMenu(
                () => SafeClipboard.SetText(GetSelectedText(textBox)),
                () => textBox.Paste(),
                () => ClearSelection(textBox));
        }

        #endregion

        #region AvalonEdit

        private static void OnEditorLoaded(object sender, RoutedEventArgs e)
        {
            EnsureEditorMenu(sender as TextEditor);
        }

        private static void OnEditorMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var editor = sender as TextEditor;
            if (editor == null)
            {
                return;
            }

            EnsureEditorMenu(editor);
            RefreshDeleteItem(editor.ContextMenu, !string.IsNullOrEmpty(editor.SelectedText), editor.IsReadOnly);
        }

        /// <summary>
        /// 只给没自带菜单的编辑器兜底;各页面 XAML 里已有的菜单原样保留,
        /// 仅由 RefreshDeleteItem 统一其中"删除"的可用性。
        /// </summary>
        private static void EnsureEditorMenu(TextEditor editor)
        {
            if (editor == null || editor.ContextMenu != null)
            {
                return;
            }

            editor.ContextMenu = BuildMenu(
                () => SafeClipboard.SetText(editor.SelectedText),
                () => editor.Paste(),
                () => editor.Delete());
        }

        #endregion

        #region 菜单构建

        private static ContextMenu BuildMenu(Action copy, Action paste, Action delete)
        {
            var menu = new ContextMenu();

            var itemCopy = CreateItem(CopyHeader, "copy.png");
            itemCopy.Click += (_, __) => copy();
            menu.Items.Add(itemCopy);

            var itemPaste = CreateItem(PasteHeader, "paste.png");
            itemPaste.Click += (_, __) => paste();
            menu.Items.Add(itemPaste);

            var itemDelete = CreateItem(DeleteHeader, "delete.png");
            itemDelete.Click += (_, __) => delete();
            menu.Items.Add(itemDelete);

            return menu;
        }

        /// <summary>
        /// 与各页面现有菜单项保持一致的观感:16x16 图标 + 手型光标
        /// </summary>
        private static MenuItem CreateItem(string header, string iconFile)
        {
            var item = new MenuItem
            {
                Header = header,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            var source = GetIcon(iconFile);
            if (source != null)
            {
                item.Icon = new Image
                {
                    Width = 16,
                    Height = 16,
                    Source = source
                };
            }

            return item;
        }

        /// <summary>
        /// 图标缓存:BitmapImage 默认按需解码,这里改成立即解码并冻结后复用,
        /// 避免每次弹出菜单时都在渲染帧里同步解码图片(菜单弹出会跟着卡一下)
        /// </summary>
        private static readonly Dictionary<string, BitmapImage> _iconCache = new Dictionary<string, BitmapImage>();

        private static BitmapImage GetIcon(string fileName)
        {
            var uri = $"pack://application:,,,/WDB;component/Resources/{fileName}";
            lock (_iconCache)
            {
                if (_iconCache.TryGetValue(uri, out var cached))
                {
                    return cached;
                }

                BitmapImage image = null;
                try
                {
                    image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(uri);
                    image.EndInit();
                    image.Freeze();
                }
                catch (Exception ex)
                {
                    // 图标缺失不致命,退化成纯文字菜单项
                    Log.Error(ex.Message);
                    image = null;
                }

                _iconCache[uri] = image;
                return image;
            }
        }

        #endregion

        #region 可用性刷新

        /// <summary>
        /// 按当前编辑状态刷新"删除"菜单项:无选中或只读时不可点。
        ///
        /// 只按 Header 精确匹配"删除",认不出的菜单项一律不动。
        /// 刻意不碰"复制":各页面 Header 同为"复制"但语义并不一致 ——
        /// UcJWT 里 Header/Payload 两个编辑器的"复制"是"复制全部内容",
        /// 不依赖选区,统一按选区禁用会把它一起误伤。
        /// </summary>
        private static void RefreshDeleteItem(ContextMenu menu, bool hasSelection, bool isReadOnly)
        {
            if (menu == null)
            {
                return;
            }

            foreach (var obj in menu.Items)
            {
                var item = obj as MenuItem;
                if (item == null)
                {
                    continue;
                }

                if ((item.Header as string) == DeleteHeader)
                {
                    item.IsEnabled = hasSelection && !isReadOnly;
                }
            }
        }

        #endregion
    }
}
