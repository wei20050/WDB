using System;
using System.Diagnostics;
using System.IO;

namespace WDB.Common
{
    public class Core
    {

        /// <summary>
        /// 获取程序依赖路径
        /// </summary>
        /// <returns></returns>
        public static string GetAppPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName));


        /// <summary>
        /// 注册程序退出后删除DLL的CMD命令
        /// </summary>
        /// <param name="dirPath">要删除的DLL目录完整路径</param>
        public static void RegisterDeleteDllOnExit(string dirPath)
        {
            // 校验路径合法性
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
            {
                Console.WriteLine("DLL路径无效或文件夹不存在");
                return;
            }
            try
            {
                // 构造CMD命令：延迟2秒（等待程序退出）→ 删除DLL文件夹 → 自身退出
                // /c：执行命令后关闭窗口；/q：删除无提示；timeout /t 2：延迟2秒（可调整）
                string cmdCommand = $"/c timeout /t 2 /nobreak >nul && rmdir /q /s \"{dirPath}\"";

                // 启动CMD进程（隐藏窗口、不阻塞当前程序）
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdCommand,
                    WindowStyle = ProcessWindowStyle.Hidden, // 隐藏CMD窗口
                    CreateNoWindow = true, // 不创建窗口
                    UseShellExecute = false // 禁用Shell执行（避免弹窗）
                };

                // 启动CMD进程（程序退出后，CMD仍会执行）
                Process.Start(psi);
                Console.WriteLine("已注册CMD删除命令，程序退出后自动执行");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"注册删除命令失败：{ex.Message}");
            }
        }
    }
}
