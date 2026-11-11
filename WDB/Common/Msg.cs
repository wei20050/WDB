using System;
using System.Windows;
using System.Windows.Threading;
using WDB.Window;

namespace WDB.Common
{
    /// <summary>
    /// 统一提示框。
    /// 已由 HandyControl.MessageBox 换成 WDB.Window.MsgBox:
    /// 前者每次弹出都要新建窗口、并在 OnApplyTemplate 之后才装配按钮,窗口框架先出现、按钮卡一下才显示;
    /// 后者复用同一个窗口实例,按钮常驻在 XAML 中,弹出时不会再有这个延迟。
    /// 对外方法签名保持不变。
    /// </summary>
    public class Msg
    {
        /// <summary>
        /// 在 UI 线程执行操作(后台线程调用时自动切换,避免在非 UI 线程弹窗导致异常)
        /// </summary>
        private static void Invoke(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }

        public static void Ok(string msg)
        {
            Invoke(() => MsgBox.Show(msg, MsgBox.MsgIcon.Success, true, "提示"));
        }

        public static void Warning(string msg)
        {
            Invoke(() => MsgBox.Show(msg, MsgBox.MsgIcon.Warning, true, "警告"));
        }

        public static void Error(string msg)
        {
            Invoke(() => MsgBox.Show(msg, MsgBox.MsgIcon.Error, true, "错误"));
        }

        public static MessageBoxResult Question(string msg, string caption = "操作提示")
        {
            var result = false;
            Invoke(() => result = MsgBox.Show(msg, MsgBox.MsgIcon.Question, false, caption));
            return result ? MessageBoxResult.OK : MessageBoxResult.Cancel;
        }

        public static bool Question(string msg)
        {
            var result = false;
            Invoke(() => result = MsgBox.Show(msg, MsgBox.MsgIcon.Question, false, "操作提示"));
            return result;
        }

    }
}
