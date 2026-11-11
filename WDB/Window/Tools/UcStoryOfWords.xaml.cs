using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using Brush = System.Windows.Media.Brush;
using WDB.Common;
using Newtonsoft.Json;

namespace WDB.Window.Tools
{
    public partial class UcStoryOfWords : UserControl
    {
        List<Hitokoto> WzwyList;
        public UcStoryOfWords()
        {
            InitializeComponent();
            DataContext = this;
            var wzwyJson = Properties.Resources.wzwy;
            WzwyList = JsonConvert.DeserializeObject<List<Hitokoto>>(wzwyJson);
        }

        private readonly List<string> Colors = new List<string>
        {
            // 深色系 → 白字
            "#3B4252","#2A3D52","#3D5068","#435969","#3A5063",
            "#4C566A","#3C4F62","#2D3D50","#354556","#2E3B4E",
            "#505A6B","#415063","#364558","#2B3B50","#324255",
            "#883939","#854439","#805239","#7C5F39","#736539",
            "#597339","#497039","#396B4A","#39606B","#394E73",
            "#4B3973","#5E3973","#73396C","#733956","#733942",

            // 浅色系 → 黑字
            "#E5E9F0","#D8DEE9","#ECEFF4","#D5DCE6","#DFE5ED",
            "#C8D0D9","#D1D9E0","#C3CCD6","#CBD3DC","#D6DEE5",
            "#E2D6D6","#E2DBD0","#E3E0D3","#D8E3D3","#D3E3E1",
            "#D3D9E3","#DAD3E3","#E2D3E0","#E3D3D8","#F0E6E6",
            "#F0EAE0","#F1F0E3","#E6F1E3","#E3F1EE","#E3E9F1",
            "#EAE3F1","#F0E3EE","#F1E3E6","#EFEFEF","#E8E8E8",

            // 中间混合色（随机触发深浅）
            "#726A6F","#6E727D","#68777D","#6F7D7A","#7D7A6F",
            "#7D746F","#7D6F75","#796F7D","#8E7A7A","#89837A",
            "#81897A","#7A8986","#7A8189","#7A7A89","#897A84",
            "#A98C8C","#A69485","#9FA685","#8CA698","#8C9CA6",
            "#948CA6","#A68CA1","#B8A3A3","#B5AA9A","#AAB59E",
            "#9EB5AE","#9EA5B5","#A69EB5","#B59EAE","#C9B4B4",
            "#C6BBAC","#BFC6AC","#AFC6BC","#AFBCC6","#B7AFC6",
            "#D1B9B9","#CCB8AA","#C3CCAA","#A9CCB9","#A9BCCC",
            "#D9C1C1","#D4C2B3","#CCD4A9","#A9D4C0","#A9BED4",
            "#BAA9D4","#D4A9CA","#E0C6C6","#DCC6B6","#D3D9B6"
        };

        private void CreateText()
        {
            try
            {
                Random random = new Random();

                // 随机句子
                int index = random.Next(WzwyList.Count);
                var hitokoto = WzwyList[index];
                TextContent.Text = hitokoto.text;
                TextAuthor.Text = $"—— {hitokoto.author} {hitokoto.source}";

                // 随机颜色
                string bgHex = Colors[random.Next(Colors.Count)];
                Color bgColor = (Color)ColorConverter.ConvertFromString(bgHex);
                Box.Background = new SolidColorBrush(bgColor);

                // 自动计算亮度 → 自动选文字颜色
                double brightness = bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114;

                if (brightness < 130) // 深色背景
                {
                    TextContent.Foreground = Brushes.White;
                    TextAuthor.Foreground = Brushes.LightGray;
                }
                else // 浅色背景（现在一定会触发！）
                {
                    TextContent.Foreground = Brushes.Black;
                    TextAuthor.Foreground = Brushes.DarkSlateGray;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
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

        private void UcStoryOfWords_OnLoaded(object sender, RoutedEventArgs e)
        {
            CreateText();
        }

        /// <summary>
        /// 换一句
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnReplace_OnClick(object sender, RoutedEventArgs e)
        {
            CreateText();
        }

        public class Hitokoto
        {
            public string text { get; set; }
            public string author { get; set; }
            public string source { get; set; }
        }
    }
}
