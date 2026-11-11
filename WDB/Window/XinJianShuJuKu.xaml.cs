using NetFrameWork.Database;
using System.Windows;
using System.Windows.Input;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    /// <summary>
    /// 创建数据库(按数据库类型提供字符集/排序规则/所有者等选项)
    /// </summary>
    public partial class XinJianShuJuKu
    {
        public string DbName { get; private set; }
        public string CharSet { get; private set; }
        public string Collate { get; private set; }

        /// <summary>
        /// 所有者(不叫 Owner,避免与 Window.Owner 冲突)
        /// </summary>
        public string OwnerName { get; private set; }

        public XinJianShuJuKu(LianJie lj)
        {
            InitializeComponent();
            var type = lj.DataBaseType;
            var isPgLike = type == ORM.DbTypeEnum.Pgsql || type == ORM.DbTypeEnum.Kingbase || type == ORM.DbTypeEnum.GaussDB;
            var unit = type == ORM.DbTypeEnum.Dameng ? "模式" : "数据库";
            var head = "创建" + unit;
            GroupBoxMain.Header = head;
            Title = head;
            labName.Content = unit + "名:";

            //字符集(SqlServer 无此概念)
            var charSetList = Db.GetCharSetList(type);
            rowCharSet.Visibility = charSetList.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            cbCharSet.ItemsSource = charSetList;
            if (charSetList.Count > 0)
            {
                cbCharSet.SelectedIndex = 0;
            }

            //排序规则
            var collateList = Db.GetCollateList(type);
            rowCollate.Visibility = collateList.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            cbCollate.ItemsSource = collateList;
            if (collateList.Count > 0)
            {
                cbCollate.SelectedIndex = 0;
            }

            //所有者(Pg系列 与 达梦)
            rowOwner.Visibility = isPgLike || type == ORM.DbTypeEnum.Dameng ? Visibility.Visible : Visibility.Collapsed;
            txtOwner.Text = lj.UserName;
            txtName.Focus();
        }

        private void BtnQueding(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                Msg.Warning("请输入名称");
                return;
            }
            DbName = txtName.Text.Trim();
            CharSet = cbCharSet.Text;
            Collate = cbCollate.Text;
            OwnerName = txtOwner.Text?.Trim();
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
