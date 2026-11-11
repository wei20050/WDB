using System.Collections.Generic;
using System.Windows;
using WDB.Entity;

namespace WDB.Window
{
    public partial class BiaoJieGouNoDb
    {
        BiaoJieGouNoDbModel _model;
        public BiaoJieGouNoDb(LianJie lj, Dictionary<string, string> info)
        {
            InitializeComponent();
            _model = (BiaoJieGouNoDbModel)DataContext;
            labLj.Content = lj.Name + $" ( host: {lj.Host} port: {lj.Port})";
            var tmpList = new List<NoDbData>();
            foreach (var i in info)
            {
                tmpList.Add(new NoDbData
                {
                    Key = i.Key,
                    Value = i.Value
                });
            }
            _model.InfoList = tmpList;
            shuaxin();
        }

        private void shuaxin()
        {
            _model.InfoBindList = _model.InfoList.FindAll(p => p.Key.Contains(TxtChaxun.Text));
        }

        private void TxtChaxun_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            shuaxin();
        }
    }
}
