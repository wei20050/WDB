using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using WDB.Common;

namespace WDB
{
    public partial class App : Application
    {

        static App()
        {
            StartupTrace.Mark("App 类型初始化(入口 Main 刚开始执行)");
        }

        public App()
        {
            StartupTrace.Mark("App 构造开始(CLR/JIT/依赖项加载已完成)");
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            //全局文本右键菜单:TextBox / HandyControl 文本框 / AvalonEdit 统一中文菜单与可用性
            TextEditContextMenu.Attach();
            StartupTrace.Mark("App 构造完成");
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            //记完整异常(含堆栈),否则界面异常被吞掉后无从排查
            Log.Error("系统级错误:" + e.Exception);
            e.Handled = true;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {

                // 你的 privatePath
                string probingPath = Core.GetAppPath;

                // 要加载的 dll 名称
                string assemblyName = new AssemblyName(args.Name).Name + ".dll";

                // 拼接路径
                string dllPath = Path.Combine(probingPath, "dll", assemblyName);

                // 存在就加载
                if (File.Exists(dllPath))
                {
                    return Assembly.LoadFrom(dllPath);
                }

                return null;
            }
            finally
            {
                StartupTrace.AssemblyResolveCount++;
                StartupTrace.AssemblyResolveMs += watch.ElapsedMilliseconds;
            }
        }

        private static Mutex _mutex;
        private const string PipeName = "WDB_HealthSearchApp_Pipe_Instance";

        protected override void OnStartup(StartupEventArgs e)
        {
            foreach (var arg in e.Args)
            {
                if (string.Equals(arg, "-startup-trace", StringComparison.OrdinalIgnoreCase))
                {
                    StartupTrace.Enabled = true;
                }
            }
            StartupTrace.Mark("App.OnStartup 开始(应用资源字典 App.xaml 已加载)");

            bool isNew;
            _mutex = new Mutex(true, "WDB_HealthSearch_Unique_App", out isNew);

            if (!isNew)
            {
                // 发消息给旧程序：【显示窗口】
                SendShowMessage();
                Environment.Exit(0);
                return;
            }

            // 启动监听消息（用来接收“显示窗口”命令）
            StartListenPipe();

            StartupTrace.Mark("单实例检查/管道监听线程已启动");
            base.OnStartup(e);
        }

        // 发送消息让旧程序显示窗口
        private void SendShowMessage()
        {
            try
            {
                using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    pipe.Connect(100);
                    using (var sw = new StreamWriter(pipe))
                    {
                        sw.WriteLine("SHOW");
                        sw.Flush();
                    }
                }
            }
            catch { }
        }

        // 监听消息 → 收到就显示主窗口
        private void StartListenPipe()
        {
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using (var pipe = new NamedPipeServerStream(PipeName))
                        {
                            pipe.WaitForConnection();
                            using (var sr = new StreamReader(pipe))
                            {
                                string msg = sr.ReadLine();
                                if (msg == "SHOW")
                                {
                                    // 这里让主窗口显示
                                    Dispatcher.Invoke(() =>
                                    {
                                        if (MainWindow != null)
                                        {
                                            MainWindow.WindowState = Config.IsMax ? WindowState.Maximized : WindowState.Normal;
                                            MainWindow.Show();
                                            MainWindow.Activate();
                                        }
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }
            })
            { IsBackground = true }.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Close();
            base.OnExit(e);
        }

    }
}
