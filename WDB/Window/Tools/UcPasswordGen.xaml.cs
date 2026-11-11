using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WDB.Common;

namespace WDB.Window.Tools
{
    public partial class UcPasswordGen : UserControl
    {
        UcPasswordGenModel _model;
        public UcPasswordGen()
        {
            InitializeComponent();
            _model = new UcPasswordGenModel();
            _model.PasswordList = new List<PasswordResultDTO>();
            DataContext = _model;
            TableGrid.PreviewMouseWheel += (sender, e) =>
            {
                var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = MouseWheelEvent;
                eventArg.Source = sender;
                TableGrid.RaiseEvent(eventArg);
            };
            TextPwdNums.Value = 2;
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
            var pwdNums = Convert.ToInt32(TextPwdNums.Value);
            var pwdLens = Convert.ToInt32(TextPwdLens.Value);
            var isNums = CheckNums.IsChecked;
            var isLowerCase = CheckLowerCase.IsChecked;
            var isUpperCase = CheckUpperCase.IsChecked;
            var isSymbol = CheckSymbol.IsChecked;
            var pwds = new List<PasswordResultDTO>();

            var strPwds = StrHelp.GeneratePassword(pwdLens, pwdNums, isNums.Value, isLowerCase.Value, isUpperCase.Value, isSymbol.Value);
            strPwds.ForEach(pwd =>
            {
                pwds.Add(new PasswordResultDTO
                {
                    Password = pwd,
                    PasswordStrength = StrHelp.PasswordStrength(pwd)
                });
            });
            _model.PasswordList = pwds;
        }

        /// <summary>
        /// 复制行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCopyRow_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedItem = (PasswordResultDTO)TableGrid.CurrentItem;
            if (selectedItem != null)
            {
                SafeClipboard.SetText(selectedItem.Password);
                Oops.Ok("密码已复制到剪切板");
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var itemsStr = string.Empty;
            foreach (PasswordResultDTO selectedItem in TableGrid.Items)
            {
                if (selectedItem != null)
                {
                    itemsStr += selectedItem.Password + Environment.NewLine;
                }
            }
            SafeClipboard.SetText(itemsStr);
            Oops.Ok("全部密码已复制到剪切板");

        }
    }
}
