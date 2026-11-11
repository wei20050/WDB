using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace WDB.Common
{
    public class IconHelp
    {

        /// <summary>
        /// 通用方法：创建带图标的Image控件
        /// </summary>
        /// <param name="packUri">嵌入资源的Pack URI</param>
        /// <param name="width">图标宽度</param>
        /// <param name="height">图标高度</param>
        /// <returns>Image控件</returns>
        public static Image CreateImageIcon(string packUri, double width, double height)
        {
            try
            {
                return new Image
                {
                    Width = width,
                    Height = height,
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(packUri)),
                    Stretch = Stretch.Uniform // 保持图片比例
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                // 路径错误时返回空图标（避免崩溃）
                return new Image { Width = width, Height = height };
            }
        }
    }
}
