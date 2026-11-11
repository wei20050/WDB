using System.Windows;
using System.Windows.Controls;

namespace WDB.Window.Tools
{
    public partial class UcRgbToHex : UserControl
    {
        public UcRgbToHex()
        {
            InitializeComponent();
            DataContext = this;
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
    }
}
