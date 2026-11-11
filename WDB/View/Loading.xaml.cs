using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WDB.View
{
    /// <summary>
    /// Loading.xaml 的交互逻辑
    /// </summary>
    public partial class Loading : UserControl
    {

        public static readonly DependencyProperty BackgroundCornerRadiusProperty = DependencyProperty.Register(
            "BackgroundCornerRadius", typeof(double), typeof(Loading), new PropertyMetadata(default(double)));

        /// <summary>
        /// 背景圆角度
        /// </summary>
        public double BackgroundCornerRadius
        {
            get => (double)GetValue(BackgroundCornerRadiusProperty);
            set => SetValue(BackgroundCornerRadiusProperty, value);
        }

        public Loading()
        {
            InitializeComponent();
            DataContext = this;
            animationTimer = new DispatcherTimer(
                DispatcherPriority.ContextIdle, Dispatcher);
            animationTimer.Interval = new TimeSpan(0, 0, 0, 0, 90);
            delayTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            delayTimer.Tick += DelayTimer_Tick;
        }

        #region Data  
        private readonly DispatcherTimer animationTimer;

        /// <summary>
        /// 延迟显示计时器:不足延迟时长的操作不显示覆盖层,避免闪一下
        /// </summary>
        private readonly DispatcherTimer delayTimer;
        #endregion

        #region Private Methods  
        private void Start()
        {
            animationTimer.Tick += HandleAnimationTick;
            animationTimer.Start();
        }

        private void Stop()
        {
            animationTimer.Stop();
            animationTimer.Tick -= HandleAnimationTick;
        }

        private void HandleAnimationTick(object sender, EventArgs e)
        {
            SpinnerRotate.Angle = (SpinnerRotate.Angle + 36) % 360;
        }

        private void HandleLoaded(object sender, RoutedEventArgs e)
        {
            const double offset = Math.PI;
            const double step = Math.PI * 2 / 10.0;

            SetPosition(C0, offset, 0.0, step);
            SetPosition(C1, offset, 1.0, step);
            SetPosition(C2, offset, 2.0, step);
            SetPosition(C3, offset, 3.0, step);
            SetPosition(C4, offset, 4.0, step);
            SetPosition(C5, offset, 5.0, step);
            SetPosition(C6, offset, 6.0, step);
            SetPosition(C7, offset, 7.0, step);
            SetPosition(C8, offset, 8.0, step);
        }

        private void SetPosition(Ellipse ellipse, double offset,
            double posOffSet, double step)
        {
            ellipse.SetValue(Canvas.LeftProperty, 50.0
                + Math.Sin(offset + posOffSet * step) * 50.0);

            ellipse.SetValue(Canvas.TopProperty, 50
                + Math.Cos(offset + posOffSet * step) * 50.0);
        }

        private void HandleUnloaded(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        /// <summary>
        /// 持续显示超过延迟时长后才真正把覆盖层显示出来
        /// </summary>
        private void DelayTimer_Tick(object sender, EventArgs e)
        {
            delayTimer.Stop();
            RootBorder.Visibility = Visibility.Visible;
            Start();
        }

        private void HandleVisibleChanged(object sender,
            DependencyPropertyChangedEventArgs e)
        {
            bool isVisible = (bool)e.NewValue;

            if (isVisible)
            {
                delayTimer.Start();
            }
            else
            {
                delayTimer.Stop();
                RootBorder.Visibility = Visibility.Collapsed;
                Stop();
            }
        }
        #endregion
    
}
}
