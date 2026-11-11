using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WDB.Window.Tools
{
    public partial class UcLinuxCommand : UserControl
    {
        public UcLinuxCommand()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var linuxJson = Properties.Resources.linux;
            var linuxCommands = JsonConvert.DeserializeObject<List<LinuxCommand>>(linuxJson);
            TableGrid.ItemsSource = linuxCommands;
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

        /// <summary>
        /// 实时搜索
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchText_TextChanged(object sender, TextChangedEventArgs e)
        {
            NoDataText.Visibility = Visibility.Collapsed;
            var linuxJson = Properties.Resources.linux;
            var linuxCommands = JsonConvert.DeserializeObject<List<LinuxCommand>>(linuxJson);
            var searchText = SearchText.Text;
            if (string.IsNullOrEmpty(searchText))
            {
                TableGrid.ItemsSource = linuxCommands;
                return;
            }
            var mimeTypes = linuxCommands.Where(x => x.n.Contains(searchText) || x.d.Contains(searchText));
            TableGrid.ItemsSource = mimeTypes;
            if (!mimeTypes.Any())
            {
                NoDataText.Visibility = Visibility.Visible;
            }
        }
    }

    public class LinuxCommand
    {
        public string n { get; set; }
        public string p { get; set; }
        public string d { get; set; }
    }
}
