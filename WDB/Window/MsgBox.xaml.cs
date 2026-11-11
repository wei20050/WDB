using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WDB.Window
{
    /// <summary>
    /// 统一提示框(替代 HandyControl.MessageBox)。
    /// HandyControl 的消息框每次弹出都要新建窗口,并且要在 OnApplyTemplate 之后才装配按钮,
    /// 表现就是"窗口先出来、按钮卡一下才显示"。这里改为复用同一个窗口实例:
    /// 窗口、模板、按钮在首次弹出时就已经就绪,之后每次只更新内容再显示。
    /// </summary>
    public partial class MsgBox
    {
        /// <summary>
        /// 图标类型
        /// </summary>
        public enum MsgIcon
        {
            Success,
            Warning,
            Error,
            Question
        }

        private static readonly Brush SuccessBrush = CreateBrush(0x4C, 0xAF, 0x50);
        private static readonly Brush WarningBrush = CreateBrush(0xFF, 0x98, 0x00);
        private static readonly Brush ErrorBrush = CreateBrush(0xF4, 0x43, 0x36);
        private static readonly Brush QuestionBrush = CreateBrush(0x21, 0x96, 0xF3);

        /// <summary>
        /// 复用实例(只隐藏不销毁)
        /// </summary>
        private static MsgBox _shared;

        /// <summary>
        /// 复用实例是否正在显示,用于嵌套弹窗时改用临时实例
        /// </summary>
        private static bool _sharedBusy;

        /// <summary>
        /// 当前实例是否可复用
        /// </summary>
        private bool _reusable;

        /// <summary>
        /// 本次弹出的结果(true 表示点了"确定")
        /// </summary>
        private bool _result;

        private MsgBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 弹出提示框并返回是否点了"确定"
        /// </summary>
        /// <param name="msg">提示内容</param>
        /// <param name="icon">图标类型</param>
        /// <param name="onlyOk">true 只显示"确定",false 显示"确定/取消"</param>
        /// <param name="title">窗口标题</param>
        public static bool Show(string msg, MsgIcon icon, bool onlyOk, string title)
        {
            var box = TakeBox();
            try
            {
                box.Prepare(msg, icon, onlyOk, title);
                box.ShowDialog();
                return box._result;
            }
            finally
            {
                ReleaseBox(box);
            }
        }

        private static MsgBox TakeBox()
        {
            if (_sharedBusy)
            {
                //上一个提示框还没关就又弹一个:用临时实例,避免同一个窗口被重复 ShowDialog
                return new MsgBox();
            }
            if (_shared == null)
            {
                _shared = new MsgBox { _reusable = true };
                _shared.Closed += (_, __) => _shared = null;
            }
            _sharedBusy = true;
            return _shared;
        }

        private static void ReleaseBox(MsgBox box)
        {
            if (box._reusable)
            {
                _sharedBusy = false;
            }
        }

        /// <summary>
        /// 每次弹出前重置内容与按钮状态
        /// </summary>
        private void Prepare(string msg, MsgIcon icon, bool onlyOk, string title)
        {
            _result = false;
            Title = string.IsNullOrEmpty(title) ? "提示" : title;
            tbMsg.Text = msg ?? string.Empty;
            imgIcon.Data = TryFindResource(GetIconKey(icon)) as Geometry;
            imgIcon.Fill = GetIconBrush(icon);
            btnCancel.Visibility = onlyOk ? Visibility.Collapsed : Visibility.Visible;
            SetOwner();
            CenterOnScreen();
        }

        /// <summary>
        /// 挂在当前激活的窗口上,避免被父窗口挡住
        /// </summary>
        private void SetOwner()
        {
            var owner = GetActiveWindow();
            if (owner == null || ReferenceEquals(owner, this) || ReferenceEquals(owner, Owner))
            {
                return;
            }
            try
            {
                Owner = owner;
            }
            catch
            {
                //窗口正处于模态显示状态时不允许改 Owner,保持原样即可
            }
        }

        private static System.Windows.Window GetActiveWindow()
        {
            var app = Application.Current;
            if (app == null)
            {
                return null;
            }
            for (var i = app.Windows.Count - 1; i >= 0; i--)
            {
                var window = app.Windows[i];
                if (window.IsActive && window.IsVisible && !(window is MsgBox))
                {
                    return window;
                }
            }
            return app.MainWindow;
        }

        /// <summary>
        /// 复用实例只在首次显示时应用 WindowStartupLocation,这里每次手动居中
        /// </summary>
        private void CenterOnScreen()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }

        private static string GetIconKey(MsgIcon icon)
        {
            switch (icon)
            {
                case MsgIcon.Success:
                    return "SuccessGeometry";
                case MsgIcon.Warning:
                    return "WarningGeometry";
                case MsgIcon.Error:
                    return "ErrorGeometry";
                default:
                    return "AskGeometry";
            }
        }

        private static Brush GetIconBrush(MsgIcon icon)
        {
            switch (icon)
            {
                case MsgIcon.Success:
                    return SuccessBrush;
                case MsgIcon.Warning:
                    return WarningBrush;
                case MsgIcon.Error:
                    return ErrorBrush;
                default:
                    return QuestionBrush;
            }
        }

        private static Brush CreateBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// 关闭当前提示:复用实例只隐藏,下次弹出不再重建窗口
        /// </summary>
        private void Dismiss(bool result)
        {
            _result = result;
            if (_reusable)
            {
                Hide();
            }
            else
            {
                Close();
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Dismiss(true);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Dismiss(false);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_reusable)
            {
                return;
            }
            //点标题栏关闭按钮时不销毁复用实例,按"取消"处理
            e.Cancel = true;
            Dismiss(false);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Dismiss(false);
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Dismiss(true);
            }
        }
    }
}
