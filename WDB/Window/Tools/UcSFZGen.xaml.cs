using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WDB.Common;

namespace WDB.Window.Tools
{
    public partial class UcSFZGen : UserControl
    {
        UcSFZGenModel _model;
        Dictionary<string, Dictionary<string, string>> _ssq;
        public UcSFZGen()
        {
            InitializeComponent();
            _model = new UcSFZGenModel();
            _model.SFZList = new List<string>();
            DataContext = _model;
            TableGrid.PreviewMouseWheel += (sender, e) =>
            {
                var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = MouseWheelEvent;
                eventArg.Source = sender;
                TableGrid.RaiseEvent(eventArg);
            };
            TextSfzNums.Value = 2;
            var ssq = Properties.Resources.ssq;
            _ssq = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(ssq);
            cbSheng.Items.Add("---- 省 ----");
            foreach (var item in _ssq["86"])
            {
                cbSheng.Items.Add(new ComboBoxItem { Content = item.Value, Tag = item.Key });
            }
            cbSheng.SelectedIndex = 9;
            cbShi.Items.Add("---- 市 ----");
            cbShi.SelectedIndex = 1;
            cbQu.Items.Add("---- 区 ----");
            cbQu.SelectedIndex = 8;

            cbN.SelectedIndex = 35;
            cbY.SelectedIndex = 10;
            cbR.SelectedIndex = 20;

            cbXB.SelectedIndex = 1;
        }

        /// <summary>
        /// 返回
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnReturn_Click(object sender, RoutedEventArgs e)
        {
            var parentWindow = (ToolBox)System.Windows.Window.GetWindow(this);
            parentWindow.UcBox.Content = new MainTools();
        }

        private void BtnGen_Click(object sender, RoutedEventArgs e)
        {
            if (cbSheng.SelectedItem is ComboBoxItem
                && cbShi.SelectedItem is ComboBoxItem
                && cbQu.SelectedItem is ComboBoxItem qu
                && cbN.SelectedItem.ToString() != "---- 年 ----"
                && cbY.SelectedItem.ToString() != "---- 月 ----"
                && cbR.SelectedItem.ToString() != "---- 日 ----"
                && cbXB.SelectedItem.ToString() != "---- 性别 ----"
                )
            {
                var nums = Convert.ToInt32(TextSfzNums.Value);

                var sfzs = new List<string>();
                for (int i = 0; i < nums; i++)
                {
                    var sfz = IdCardGenerator.Generate(qu.Tag.ToString(), $"{cbN.SelectionBoxItem}{cbY.SelectionBoxItem}{cbR.SelectionBoxItem}", cbXB.SelectedIndex.ToString());
                    sfzs.Add(sfz);
                }
                _model.SFZList = sfzs;
            }
        }

        /// <summary>
        /// 复制行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCopyRow_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedItem = TableGrid.CurrentItem.ToString();
            if (selectedItem != null)
            {
                SafeClipboard.SetText(selectedItem);
                Oops.Ok("身份证已复制到剪切板");
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var itemsStr = string.Empty;
            foreach (string selectedItem in TableGrid.Items)
            {
                if (selectedItem != null)
                {
                    itemsStr += selectedItem + Environment.NewLine;
                }
            }
            SafeClipboard.SetText(itemsStr);
            Oops.Ok("全部身份证已复制到剪切板");

        }

        private void cbSheng_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            cbShi.Items.Clear();
            cbShi.Items.Add("---- 市 ----");
            cbShi.SelectedIndex = 0;
            if (cbSheng.SelectedItem is ComboBoxItem item)
            {
                var s = item.Tag.ToString();
                foreach (var i in _ssq[s])
                {
                    cbShi.Items.Add(new ComboBoxItem { Content = i.Value, Tag = i.Key });
                }
            }
        }

        private void cbShi_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            cbQu.Items.Clear();
            cbQu.Items.Add("---- 区 ----");
            cbQu.SelectedIndex = 0;
            if (cbShi.SelectedItem is ComboBoxItem item)
            {
                var s = item.Tag.ToString();
                foreach (var i in _ssq[s])
                {
                    cbQu.Items.Add(new ComboBoxItem { Content = i.Value, Tag = i.Key });
                }
            }
        }
    }
}
