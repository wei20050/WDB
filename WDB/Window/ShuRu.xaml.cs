using System.Windows;
using System.Windows.Input;
using WDB.Common;

namespace WDB.Window
{
    /// <summary>
    /// 通用名称输入窗口(新建数据库/新建表等)
    /// </summary>
    public partial class ShuRu
    {
        /// <summary>
        /// 输入结果
        /// </summary>
        public string Result { get; private set; }

        public ShuRu(string title, string tip, string defaultValue = "")
        {
            InitializeComponent();
            Title = title;
            tbTip.Text = tip;
            txtName.Text = defaultValue;
            Loaded += (_, __) =>
            {
                txtName.Focus();
                txtName.SelectAll();
            };
        }

        private void BtnQueding(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                Msg.Warning("请输入名称");
                return;
            }
            Result = txtName.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void BtnQuxiao(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                BtnQueding(sender, e);
            }
        }
    }
}
