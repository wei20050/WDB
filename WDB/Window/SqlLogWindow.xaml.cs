using ICSharpCode.AvalonEdit.CodeCompletion;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WDB.Common;

namespace WDB.Window
{
    public partial class SqlLogWindow
    {

        SqlLogWindowModel _model;
        public CompletionWindow completionWindow;
        public SqlLogWindow()
        {
            InitializeComponent();
            _model = (SqlLogWindowModel)DataContext;
            _model.SqlLogList = Config.GetSqlLog().OrderByDescending(p => p.Time).ToList();
            shuaxin();
        }

        private void BtnGuanbi(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 复制内容
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Fz_Click(object sender, RoutedEventArgs e)
        {
            var cellsText = UIHelp.GetSelectedCellsPrecise(dg);
            if (string.IsNullOrEmpty(cellsText))
            {
                Oops.Ok("没有可复制的内容");
            }
            else
            {
                SafeClipboard.SetText(cellsText);
                Oops.Ok("单元格内容已复制");
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void txtKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            shuaxin();
        }

        private void shuaxin()
        {
            var s = _model.Str ?? string.Empty;
            _model.SqlLogBindList = _model.SqlLogList.FindAll(
                p => p.Info?.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0
                || p.Sql?.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0
                || p.Time.ToString("yyyy-MM-dd HH:mm:ss")?.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void BtnChaxun_Click(object sender, RoutedEventArgs e)
        {
            _model.SqlLogList = Config.GetSqlLog().OrderByDescending(p => p.Time).ToList();
            shuaxin();
        }

        private void Chakan_Click(object sender, RoutedEventArgs e)
        {
            if (!dg.SelectedCells.Any()) return;

            var lastCell = dg.SelectedCells.Last();

            dg.SelectedCells.Clear();
            dg.SelectedCells.Add(lastCell);
            dg.CurrentCell = lastCell;

            var content = lastCell.Column.GetCellContent(lastCell.Item);
            var tb = content as TextBlock;
            if (tb != null)
            {
                _ = new SqlWindow(tb.Text.SqlFormat()).ShowDialog();
            }

        }
    }
}
