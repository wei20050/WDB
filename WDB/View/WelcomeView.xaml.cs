using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WDB.Entity;

namespace WDB.View
{
    /// <summary>
    /// 首页(欢迎页):连接列表 + 授权状态(快捷入口与顶部菜单重叠,已移除)
    /// </summary>
    public partial class WelcomeView : UserControl
    {
        private const string IconRoot = "pack://application:,,,/WDB;component/Resources/";

        /// <summary>
        /// 点连接行时回调(在左侧连接树中定位并展开,由主窗口注入)
        /// </summary>
        public Action<LianJie> OpenConnectionAction { get; set; }

        /// <summary>
        /// 点连接行右侧[新建查询]时回调(按连接类型直接开对应查询页,由主窗口注入)
        /// </summary>
        public Action<LianJie> NewQueryAction { get; set; }

        /// <summary>
        /// 点右上角授权状态徽标时回调(打开关于/激活画面,由主窗口注入)
        /// </summary>
        public Action LicenseClickAction { get; set; }

        public ObservableCollection<WelcomeConnection> Connections { get; } =
            new ObservableCollection<WelcomeConnection>();

        public WelcomeView()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>
        /// 载入连接列表(按名称排序,与左侧连接树一致)
        /// </summary>
        public void Load(IEnumerable<LianJie> list)
        {
            Connections.Clear();
            foreach (var l in (list ?? Enumerable.Empty<LianJie>()).OrderBy(p => p.Name))
            {
                var typeName = string.IsNullOrEmpty(l.NoDbType) ? l.DataBaseType.ToString() : l.NoDbType;
                Connections.Add(new WelcomeConnection
                {
                    Lj = l,
                    Name = l.Name,
                    Detail = BuildDetail(l),
                    TypeName = typeName,
                    Tip = BuildTip(l, typeName),
                    Icon = IconRoot + "connect/" + (string.IsNullOrEmpty(l.NoDbType)
                        ? l.DataBaseType.ToString().ToLower()
                        : l.NoDbType.ToLower()) + ".png"
                });
            }
            TxtEmptyConn.Visibility = Connections.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 更新授权状态文案与悬停详情(激活/过期后主窗口会回调)
        /// </summary>
        public void SetLicenseText(string text, string detail = null)
        {
            TxtLicense.Text = text;
            BdLicense.ToolTip = string.IsNullOrWhiteSpace(detail) ? text : detail;
        }

        /// <summary>
        /// 连接行副标题:主机:端口 / 初始库
        /// </summary>
        private static string BuildDetail(LianJie l)
        {
            var host = string.IsNullOrEmpty(l.Host)
                ? string.Empty
                : l.Host + (string.IsNullOrEmpty(l.Port) ? string.Empty : ":" + l.Port);
            var db = string.IsNullOrEmpty(l.DataBaseName) ? string.Empty : " / " + l.DataBaseName;
            return host + db;
        }

        /// <summary>
        /// 连接行悬停详情:名称/类型/服务/初始库/用户(首页行内的名称与连接信息都会截断,悬停看全)
        /// </summary>
        private static string BuildTip(LianJie l, string typeName)
        {
            return string.Join(Environment.NewLine,
                "连接名: " + l.Name,
                "类型: " + typeName,
                "服务: " + (string.IsNullOrEmpty(l.Host) ? string.Empty : l.Host + ":" + l.Port),
                "初始数据库: " + l.DataBaseName,
                "用户: " + l.UserName);
        }

        private void Connection_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is WelcomeConnection conn)
            {
                OpenConnectionAction?.Invoke(conn.Lj);
            }
        }

        /// <summary>
        /// 连接行右侧[新建查询]
        /// </summary>
        private void NewQuery_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is WelcomeConnection conn)
            {
                //别让点击再冒泡给整行的"定位连接"
                e.Handled = true;
                NewQueryAction?.Invoke(conn.Lj);
            }
        }

        /// <summary>
        /// 授权状态徽标:点击打开关于/激活画面
        /// </summary>
        private void License_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            LicenseClickAction?.Invoke();
        }
    }

    /// <summary>
    /// 首页连接行
    /// </summary>
    public class WelcomeConnection
    {
        public LianJie Lj { get; set; }
        public string Name { get; set; }
        public string Detail { get; set; }
        public string TypeName { get; set; }
        public string Icon { get; set; }

        /// <summary>整行悬停详情(名称/类型/服务/初始库/用户)</summary>
        public string Tip { get; set; }
    }
}
