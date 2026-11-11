using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WDB.Common;

namespace WDB.Window.Tools
{
    public partial class UcUUIDGen : UserControl
    {
        UcUUIDGenModel _model;
        public UcUUIDGen()
        {
            InitializeComponent();
            _model = new UcUUIDGenModel();
            _model.UUIDList = new List<UUIDResultDTO>();
            DataContext = _model;
            TableGrid.PreviewMouseWheel += (sender, e) =>
            {
                var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = MouseWheelEvent;
                eventArg.Source = sender;
                this.TableGrid.RaiseEvent(eventArg);
            };
        }

        private void BtnReturn_Click(object sender, RoutedEventArgs e)
        {
            var parentWindow = (ToolBox)System.Windows.Window.GetWindow(this);
            parentWindow.UcBox.Content = new MainTools();
        }

        private void BtnGen_Click(object sender, RoutedEventArgs e)
        {
            var uuidNums = Convert.ToInt32(TextUUIDNums.Value);
            var uuidIsCaseUpper = CheckUpper.IsChecked;
            var uuidIsRemoveHLine = CheckRemoveHLine.IsChecked;

            var uuids = new List<UUIDResultDTO>();
            var uuidFotmat = uuidIsRemoveHLine.Value ? "N" : "D";
            for (int i = 0; i < uuidNums; i++)
            {
                var uuid = Guid.NewGuid().ToString(uuidFotmat);
                uuids.Add(new UUIDResultDTO
                {
                    UUID = uuidIsCaseUpper.Value ? uuid.ToUpper() : uuid.ToLower()
                });
            }
            _model.UUIDList = uuids;
        }

        /// <summary>
        /// 复制行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCopyRow_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedItem = (UUIDResultDTO)TableGrid.CurrentItem;
            if (selectedItem != null)
            {
                SafeClipboard.SetText(selectedItem.UUID);
                Oops.Ok("UUID已复制到剪切板");
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var itemsStr = string.Empty;
            foreach (UUIDResultDTO selectedItem in TableGrid.Items)
            {
                if (selectedItem != null)
                {
                    itemsStr += selectedItem.UUID + Environment.NewLine;
                }
            }
            SafeClipboard.SetText(itemsStr);
            Oops.Ok("全部UUID已复制到剪切板");
        }
    }
}
