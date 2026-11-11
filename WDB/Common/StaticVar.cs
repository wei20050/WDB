using System.ComponentModel;

namespace WDB.Common
{
    public class StaticVar
    {
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        private static void OnStaticPropertyChanged(string propertyName)
        {
            // 发送者必须是 null，PropertyName 必须精准匹配属性名
            StaticPropertyChanged?.Invoke(
                sender: null,
                e: new PropertyChangedEventArgs(propertyName)
            );
        }

        private static double _textFontSize = 16;

        /// <summary>
        /// 编辑器字体大小
        /// </summary>
        public static double TextFontSize
        {
            get => _textFontSize;
            set
            {
                if (_textFontSize != value)
                {
                    _textFontSize = value;
                    // 加断点/日志，确认此处是否执行
                    System.Console.WriteLine($"触发通知：TextFontSize = {value}");
                    OnStaticPropertyChanged(nameof(TextFontSize));
                }
            }
        }

        /// <summary>
        /// 文件版本号
        /// </summary>
        public static string Ver
        {
            get
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                string fileVersion = fvi.FileVersion;
                return "V" + fileVersion;
            }
        }


    }
}
