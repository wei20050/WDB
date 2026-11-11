using NetFrameWork.Database;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    public partial class XinJian
    {
        public LianJie _lj;
        public XinJian(LianJie lj)
        {
            InitializeComponent();
            _lj = lj;
            var iconUri = new Uri($"pack://application:,,,/WDB;component/Resources/connect/{lj.DataBaseType.ToString().ToLower()}.png");
            Icon = BitmapFrame.Create(iconUri);
            foreach (ORM.DbTypeEnum type in Enum.GetValues(typeof(ORM.DbTypeEnum)))
            {
                if (type == ORM.DbTypeEnum.SqlIte)
                {
                    continue;
                }
                AddComboItem(type.ToString());
            }
            var head = (lj.Id == null ? "新建 " : "编辑 ") + lj.DataBaseType;
            GroupBoxMain.Header = head;
            Title = head;
            txtLianjiemingchen.Text = lj.Name ?? head;
        }

        /// <summary>
        /// 图标缓存:BitmapImage 默认按需解码,改成立即解码并冻结后复用,
        /// 避免每次打开窗口时都在渲染阶段同步解码图片,导致按钮等内容晚一拍才出现
        /// </summary>
        private static readonly Dictionary<string, BitmapImage> _iconCache = new Dictionary<string, BitmapImage>();

        private static BitmapImage GetDbIcon(string text)
        {
            string url = $"pack://application:,,,/WDB;component/Resources/connect/{text.ToLower()}.png";
            lock (_iconCache)
            {
                if (_iconCache.TryGetValue(url, out var cached))
                {
                    return cached;
                }
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(url);
                img.EndInit();
                img.Freeze();
                _iconCache[url] = img;
                return img;
            }
        }

        /// <summary>
        /// 给 ComboBox 添加 自定义图标项
        /// </summary>
        private void AddComboItem(string text)
        {
            ComboBoxItem item = new ComboBoxItem
            {
                Content = text,
                ContentTemplate = new DataTemplate()
            };

            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanelFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

            var imgFactory = new FrameworkElementFactory(typeof(Image));
            imgFactory.SetValue(WidthProperty, 16.0);
            imgFactory.SetValue(HeightProperty, 16.0);
            imgFactory.SetValue(MarginProperty, new Thickness(0, 0, 8, 0));

            imgFactory.SetValue(Image.SourceProperty, GetDbIcon(text));

            stackPanelFactory.AppendChild(imgFactory);

            var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
            tbFactory.SetValue(TextBlock.TextProperty, text);
            tbFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            stackPanelFactory.AppendChild(tbFactory);

            item.ContentTemplate.VisualTree = stackPanelFactory;

            cbLeixin.Items.Add(item);

            if (text == _lj.DataBaseType.ToString())
            {
                cbLeixin.SelectedItem = item;
            }
        }

        private void SetLianJie()
        {
            string text = (cbLeixin.SelectedItem as ComboBoxItem).Content.ToString();
            _lj.DataBaseType = (ORM.DbTypeEnum)Enum.Parse(typeof(ORM.DbTypeEnum), text);
            _lj.Name = txtLianjiemingchen.Text;
            _lj.Host = txtZhuji.Text;
            _lj.Port = txtDuankou.Text;
            _lj.ServiceName = txtFuwuming.Text;
            _lj.DataBaseName = txtShujuku.Text;
            _lj.UserName = txtZhanghao.Text;
            _lj.PassWord = txtMima.Password;
        }

        private void BtnCeshi(object sender, RoutedEventArgs e)
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

        private void BtnQueding(object sender, RoutedEventArgs e)
        {
            SetLianJie();
            DialogResult = true;
            Close();
        }

        private void BtnQuxiao(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void cbLeixin_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            txtZhuji.Text = _lj.Host ?? "localhost";
            txtDuankou.Text = _lj.Port;
            txtFuwuming.Text = _lj.ServiceName;
            txtShujuku.Text = _lj.DataBaseName;
            txtZhanghao.Text = _lj.UserName;
            txtMima.Password = _lj.PassWord;

            string text = (cbLeixin.SelectedItem as ComboBoxItem).Content.ToString();
            var type = (ORM.DbTypeEnum)Enum.Parse(typeof(ORM.DbTypeEnum), text);


            labShujuku.Visibility = Visibility.Visible;
            txtShujuku.Visibility = Visibility.Visible;
            labFuwuming.Visibility = Visibility.Hidden;
            txtFuwuming.Visibility = Visibility.Hidden;

            switch (type)
            {
                case ORM.DbTypeEnum.MySql:
                    txtDuankou.Text = string.IsNullOrEmpty(txtDuankou.Text) ? "3306" : txtDuankou.Text;
                    txtShujuku.Text = string.IsNullOrEmpty(txtShujuku.Text) ? "mysql" : txtShujuku.Text;
                    txtZhanghao.Text = string.IsNullOrEmpty(txtZhanghao.Text) ? "root" : txtZhanghao.Text;
                    break;

                case ORM.DbTypeEnum.SqlServer:
                    txtDuankou.Text = string.IsNullOrEmpty(txtDuankou.Text) ? "1433" : txtDuankou.Text;
                    txtShujuku.Text = string.IsNullOrEmpty(txtShujuku.Text) ? "master" : txtShujuku.Text;
                    txtZhanghao.Text = string.IsNullOrEmpty(txtZhanghao.Text) ? "sa" : txtZhanghao.Text;
                    break;

                case ORM.DbTypeEnum.Oracle:
                    txtDuankou.Text = string.IsNullOrEmpty(txtDuankou.Text) ? "1521" : txtDuankou.Text;
                    txtFuwuming.Text = string.IsNullOrEmpty(txtFuwuming.Text) ? "orcl" : txtFuwuming.Text;
                    txtZhanghao.Text = string.IsNullOrEmpty(txtZhanghao.Text) ? "system" : txtZhanghao.Text;
                    break;

                case ORM.DbTypeEnum.Dameng:
                    txtDuankou.Text = string.IsNullOrEmpty(txtDuankou.Text) ? "5236" : txtDuankou.Text;
                    txtShujuku.Text = string.IsNullOrEmpty(txtShujuku.Text) ? "SYSDBA" : txtShujuku.Text;
                    txtZhanghao.Text = string.IsNullOrEmpty(txtZhanghao.Text) ? "SYSDBA" : txtZhanghao.Text;
                    break;

                case ORM.DbTypeEnum.Kingbase:
                    txtDuankou.Text = string.IsNullOrEmpty(txtDuankou.Text) ? "54321" : txtDuankou.Text;
                    txtFuwuming.Text = string.IsNullOrEmpty(txtFuwuming.Text) ? "public" : txtFuwuming.Text;
                    txtShujuku.Text = string.IsNullOrEmpty(txtShujuku.Text) ? "test" : txtShujuku.Text;
                    txtZhanghao.Text = string.IsNullOrEmpty(txtZhanghao.Text) ? "system" : txtZhanghao.Text;
                    break;

                case ORM.DbTypeEnum.Pgsql:
                    txtDuankou.Text = string.IsNullOrEmpty(txtDuankou.Text) ? "5432" : txtDuankou.Text;
                    txtFuwuming.Text = string.IsNullOrEmpty(txtFuwuming.Text) ? "public" : txtFuwuming.Text;
                    txtShujuku.Text = string.IsNullOrEmpty(txtShujuku.Text) ? "postgres" : txtShujuku.Text;
                    txtZhanghao.Text = string.IsNullOrEmpty(txtZhanghao.Text) ? "postgres" : txtZhanghao.Text;
                    break;

                case ORM.DbTypeEnum.GaussDB:
                    txtDuankou.Text = string.IsNullOrEmpty(txtDuankou.Text) ? "5432" : txtDuankou.Text;
                    txtFuwuming.Text = string.IsNullOrEmpty(txtFuwuming.Text) ? "public" : txtFuwuming.Text;
                    txtShujuku.Text = string.IsNullOrEmpty(txtShujuku.Text) ? "postgres" : txtShujuku.Text;
                    txtZhanghao.Text = string.IsNullOrEmpty(txtZhanghao.Text) ? "gaussdb" : txtZhanghao.Text;
                    break;
            }

            switch (type)
            {
                case ORM.DbTypeEnum.MySql:
                case ORM.DbTypeEnum.SqlServer:
                case ORM.DbTypeEnum.Dameng:
                    break;
                case ORM.DbTypeEnum.Oracle:
                    labShujuku.Visibility = Visibility.Hidden;
                    txtShujuku.Visibility = Visibility.Hidden;
                    labFuwuming.Visibility = Visibility.Visible;
                    labFuwuming.Content = "服务名:";
                    txtFuwuming.Visibility = Visibility.Visible;
                    break;
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.GaussDB:
                    labFuwuming.Visibility = Visibility.Visible;
                    labFuwuming.Content = "模式名:";
                    txtFuwuming.Visibility = Visibility.Visible;
                    break;
            }
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
