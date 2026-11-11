using System;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using WDB.Common;
using WDB.Entity;
using System.IO;

namespace WDB.Window
{
    public partial class XinJianLocal
    {
        public LianJie _lj;
        public XinJianLocal(LianJie lj)
        {
            InitializeComponent();
            var iconUri = new Uri($"pack://application:,,,/WDB;component/Resources/connect/{lj.DataBaseType.ToString().ToLower()}.png");
            Icon = BitmapFrame.Create(iconUri);
            _lj = lj;
            if (lj.Id == null)
            {
                var head = "新建 " + lj.DataBaseType;
                GroupBoxMain.Header = head;
                Title = head;
                txtLianjiemingchen.Text = lj.Name ?? head;
                txtZhuji.Text = lj.Host;
                txtMima.Password = lj.PassWord;
            }
            else
            {
                var head = "编辑 " + lj.DataBaseType;
                GroupBoxMain.Header = head;
                Title = head;
                txtLianjiemingchen.Text = lj.Name;
                txtZhuji.Text = lj.Host;
                txtMima.Password = lj.PassWord;
            }
        }

        private void SetLianJie()
        {
            _lj.DataBaseType = _lj.DataBaseType;
            _lj.Name = txtLianjiemingchen.Text;
            _lj.Host = txtZhuji.Text;
            _lj.PassWord = txtMima.Password;
        }

        private void BtnLiulan(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = @"db文件(*.db)|*.db"
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtZhuji.Text = dialog.FileName;
            }
        }

        private void BtnChuangjian(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = @"db文件(*.db)|*.db",
                FileName = "new_db_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtZhuji.Text = dialog.FileName;
                File.WriteAllText(dialog.FileName,string.Empty);
            }
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
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        }
    }
}
