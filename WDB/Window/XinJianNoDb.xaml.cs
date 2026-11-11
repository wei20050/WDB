using System;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    public partial class XinJianNoDb
    {
        public LianJie _lj;

        /// <summary>
        /// 是否 MongoDB 连接(决定是否显示认证库/认证机制/URL参数)
        /// </summary>
        private readonly bool _isMongo;

        public XinJianNoDb(LianJie lj)
        {
            var iconUri = new Uri($"pack://application:,,,/WDB;component/Resources/connect/{lj.NoDbType.ToLower()}.png");
            Icon = BitmapFrame.Create(iconUri);
            InitializeComponent();
            _lj = lj;
            _isMongo = "MongoDB".Equals(lj.NoDbType, StringComparison.OrdinalIgnoreCase);
            PanelMongo.Visibility = _isMongo ? Visibility.Visible : Visibility.Collapsed;
            //MongoDB 多出参数区,窗口更高
            Height = _isMongo ? 660 : 500;
            if (lj.Id == null)
            {
                var head = "新建 " + lj.NoDbType;
                GroupBoxMain.Header = head;
                Title = head;
                txtLianjiemingchen.Text = lj.Name ?? head;
                txtZhuji.Text = lj.Host ?? "localhost";
                switch (lj.NoDbType)
                {
                    case "Redis":
                        txtDuankou.Text = lj.Port ?? "6379";
                        break;
                    case "MongoDB":
                        txtDuankou.Text = lj.Port ?? "27017";
                        break;
                }
                txtZhanghao.Text = lj.UserName;
                txtMima.Password = lj.PassWord;
                FillMongoParams(lj);
            }
            else
            {
                var head = "编辑 " + lj.NoDbType;
                GroupBoxMain.Header = head;
                Title = head;
                txtLianjiemingchen.Text = lj.Name;
                txtZhuji.Text = lj.Host;
                txtDuankou.Text = lj.Port;
                txtZhanghao.Text = lj.UserName;
                txtMima.Password = lj.PassWord;
                FillMongoParams(lj);
            }
        }

        /// <summary>
        /// 填充 MongoDB 认证参数(认证库默认 admin)
        /// </summary>
        private void FillMongoParams(LianJie lj)
        {
            if (!_isMongo)
            {
                return;
            }
            txtAuthSource.Text = string.IsNullOrEmpty(lj.AuthSource) ? "admin" : lj.AuthSource;
            SelectAuthMechanism(lj.AuthMechanism);
            txtUrlParams.Text = lj.UrlParams;
        }

        /// <summary>
        /// 按值选中认证机制下拉(未匹配到时选"默认")
        /// </summary>
        private void SelectAuthMechanism(string mechanism)
        {
            foreach (var item in cbAuthMechanism.Items)
            {
                if (item is ComboBoxItem ci &&
                    string.Equals(ci.Tag?.ToString() ?? string.Empty, mechanism ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    cbAuthMechanism.SelectedItem = ci;
                    return;
                }
            }
            cbAuthMechanism.SelectedIndex = 0;
        }

        /// <summary>
        /// 读取认证机制(选中"默认"时返回 null,交给驱动协商)
        /// </summary>
        private string GetAuthMechanism()
        {
            if (cbAuthMechanism.SelectedItem is ComboBoxItem ci)
            {
                var tag = ci.Tag?.ToString();
                return string.IsNullOrEmpty(tag) ? null : tag;
            }
            return null;
        }

        private void SetLianJie()
        {
            _lj.DataBaseType = _lj.DataBaseType;
            _lj.Name = txtLianjiemingchen.Text;
            _lj.Host = txtZhuji.Text;
            _lj.Port = txtDuankou.Text;
            _lj.UserName = txtZhanghao.Text;
            _lj.PassWord = txtMima.Password;
            if (_isMongo)
            {
                _lj.AuthSource = txtAuthSource.Text?.Trim();
                _lj.AuthMechanism = GetAuthMechanism();
                _lj.UrlParams = txtUrlParams.Text?.Trim();
            }
        }

        private void BtnCeshi(object sender, System.Windows.RoutedEventArgs e)
        {
            SetLianJie();
            LoadingG.Visibility = Visibility.Visible;
            _ = Task.Factory.StartNew(
               new Action(() =>
               {
                   Db.Test(_lj);
                   Dispatcher.Invoke(new Action(() =>
                   {
                       LoadingG.Visibility = Visibility.Collapsed;
                   }));
               }));
        }

        private void BtnQueding(object sender, System.Windows.RoutedEventArgs e)
        {
            SetLianJie();
            DialogResult = true;
            Close();
        }

        private void BtnQuxiao(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
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
