using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    public partial class DaoruDaochu
    {
        public List<LianJie> _lianjieList;
        public DaoruDaochu(List<LianJie> lianjieList)
        {
            InitializeComponent();
            _lianjieList = CoreConvert.Map<List<LianJie>>(lianjieList);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dg.ItemsSource = _lianjieList.OrderBy(p => p.Name).ToList();
        }

        private void BtnBaocun(object sender, RoutedEventArgs e)
        {
            var tmpList = new List<LianJie>();
            foreach (LianJie lj in dg.SelectedItems)
            {
                tmpList.Add(lj);
            }
            _lianjieList = tmpList;
            DialogResult = true;
            Close();
        }

        private void BtnGuanbi(object sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}
