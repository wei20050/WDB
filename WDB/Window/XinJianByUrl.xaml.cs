using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    /// <summary>
    /// URL / JDBC 自动识别窗口:
    /// 粘贴连接串 → 实时识别库类型 → 确定后由调用方按类型打开对应的新建窗口。
    /// 识别失败只在输入框下方就地红字提示,不弹窗。
    /// </summary>
    public partial class XinJianByUrl
    {
        /// <summary>
        /// 解析结果(识别成功时非空)
        /// </summary>
        public LianJie ParsedLianJie { get; private set; }

        private static readonly Brush _normalBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
        private static readonly Brush _errorBrush = Brushes.OrangeRed;

        public XinJianByUrl()
        {
            InitializeComponent();
            Loaded += (_, __) => txtUrl.Focus();
        }

        private void TxtUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshPreview();
        }

        /// <summary>
        /// 实时识别:成功显示库图标与类型名并放开"确定",失败就地红字
        /// </summary>
        private void RefreshPreview()
        {
            var text = (txtUrl.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                ParsedLianJie = null;
                imgType.Source = null;
                tbResult.Text = string.Empty;
                btnOk.IsEnabled = false;
                return;
            }

            if (DbUrlParser.TryParse(text, out var lj, out var error))
            {
                ParsedLianJie = lj;
                imgType.Source = LoadIcon(DbUrlParser.GetIconName(lj));
                tbResult.Text = "已识别:" + DbUrlParser.GetDisplayName(lj);
                tbResult.Foreground = _normalBrush;
                btnOk.IsEnabled = true;
            }
            else
            {
                ParsedLianJie = null;
                imgType.Source = null;
                tbResult.Text = error;
                tbResult.Foreground = _errorBrush;
                btnOk.IsEnabled = false;
            }
        }

        private static ImageSource LoadIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }
            try
            {
                return new BitmapImage(new Uri($"pack://application:,,,/WDB;component/Resources/connect/{iconName}.png"));
            }
            catch
            {
                return null;
            }
        }

        private void BtnQueding(object sender, RoutedEventArgs e)
        {
            RefreshPreview();
            if (ParsedLianJie == null)
            {
                return;
            }
            DialogResult = true;
            Close();
        }

        private void BtnQuxiao(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // 输入框支持多行,Enter 留给换行,这里只处理 Esc
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
