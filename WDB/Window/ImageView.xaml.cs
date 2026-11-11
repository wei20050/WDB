using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace WDB.Window
{
    /// <summary>
    /// 简单的大图查看窗:打开时按窗口大小等比缩放到刚好放得下,
    /// 滚轮放大/缩小(可配合滚动条拖动查看),单击图片或 Esc 关闭
    /// </summary>
    public partial class ImageView
    {
        private readonly BitmapSource _src;

        public ImageView(BitmapSource src)
        {
            InitializeComponent();
            _src = src;
            if (src == null)
            {
                return;
            }
            Img.Source = src;
            Title = $"查看图片 ({src.PixelWidth}×{src.PixelHeight})";
            //布局跑完才能拿到 ScrollViewer 的可视区域大小
            Loaded += (s, e) => Fit();
        }

        /// <summary>按视口等比缩放到刚好放得下(小图不放大)</summary>
        private void Fit()
        {
            if (_src == null || _src.PixelWidth <= 0 || _src.PixelHeight <= 0)
            {
                return;
            }
            var vw = Sv.ViewportWidth > 0 ? Sv.ViewportWidth : ActualWidth;
            var vh = Sv.ViewportHeight > 0 ? Sv.ViewportHeight : ActualHeight;
            if (vw <= 0 || vh <= 0)
            {
                return;
            }
            var scale = Math.Min(vw / _src.PixelWidth, vh / _src.PixelHeight);
            SetScale(Math.Min(1, scale));
        }

        private void SetScale(double scale)
        {
            var s = Math.Max(0.1, Math.Min(10, scale));
            St.ScaleX = s;
            St.ScaleY = s;
        }

        private void Img_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            SetScale(St.ScaleX * (e.Delta > 0 ? 1.15 : 1 / 1.15));
            e.Handled = true;
        }

        private void Img_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //单击图片即关闭
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
