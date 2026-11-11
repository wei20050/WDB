using System;
using System.IO;
using System.Text;
using System.Threading;

namespace WDB.Common
{
    /// <summary>
    /// 配置核心类
    /// 配置默认路径 默认根目录Config.cfg
    /// </summary>
    public class CoreConfig
    {

        #region 配置读写

        /// <summary>
        /// 配置默认路径 默认根目录Config.cfg
        /// </summary>
        public static string FileName { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.cfg");

        /// <summary>
        /// 读取配置
        /// </summary>
        /// <param name="key">配置键</param>
        /// <param name="defValue"></param>
        /// <param name="fileName">配置文件路径</param>
        /// <returns>配置值</returns>
        public static string Read(string key, string defValue = "", string fileName = "")
        {
            if (fileName == "")
            {
                fileName = FileName;
            }
            var ret = defValue;
            var isRead = false;
            try
            {
                MutexExec(fileName, () =>
                {
                    if (File.Exists(fileName))
                    {
                        using (StreamReader reader = new StreamReader(fileName, Encoding.UTF8))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                if (string.IsNullOrEmpty(line) || line.Substring(0, 1) == "#" || !line.Contains("=")) continue;
                                var cfgKey = line.Substring(0, line.IndexOf("=", StringComparison.Ordinal));
                                if (cfgKey.Trim() == key)
                                {
                                    isRead = true;
                                    ret = line.Substring(line.IndexOf("=", StringComparison.Ordinal) + 1).Replace(@"{\r\n}", "\r\n").Replace(@"{\n}", "\n").Trim();
                                    break;
                                }
                            }
                        }
                    }
                });
                if (!isRead) Write(key, defValue, fileName);
            }
            catch (Exception e)
            {
                throw new Exception("CoreConfig.Read Err =>", e);
            }
            return ret;
        }

        /// <summary>
        /// 写入配置
        /// </summary>
        /// <param name="key">配置键</param>
        /// <param name="value">配置值</param>
        /// <param name="fileName">配置文件路径</param>
        /// <returns></returns>
        public static void Write(string key, string value, string fileName = "")
        {
            if (fileName == "")
            {
                fileName = FileName;
            }
            value = value?.Replace("\r\n", @"{\r\n}").Replace("\n", @"{\n}").Trim();
            try
            {
                MutexExec(fileName, () =>
                {
                    var dir = Path.GetDirectoryName(fileName);
                    if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    if (File.Exists(fileName))
                    {
                        var lines = File.ReadAllLines(fileName, Encoding.UTF8);
                        var isRead = false;
                        int i = 0;
                        for (; i < lines.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].Substring(0, 1) == "#" || !lines[i].Contains("=")) continue;
                            var cfgKey = lines[i].Substring(0, lines[i].IndexOf("=", StringComparison.Ordinal));
                            if (cfgKey.Trim() == key)
                            {
                                isRead = true;
                                break;
                            }
                        }
                        if (isRead)
                        {
                            lines[i] = $"{key}={value}";
                            File.WriteAllLines(fileName, lines);
                        }
                        else
                        {
                            File.AppendAllText(fileName, $"{Environment.NewLine}{key}={value}", Encoding.UTF8);
                        }
                    }
                    else
                    {
                        File.WriteAllText(fileName, key + @"=" + value, Encoding.UTF8);
                    }
                });
            }
            catch (Exception e)
            {
                throw new Exception("CoreConfig.Write Err =>", e);
            }
        }
        #endregion

        #region 进程与线程保证同步

        /// <summary>
        /// 多线程与多进程间同步操作文件
        /// </summary>
        /// <param name="filePath">文件路径
        /// (如果将 name 指定为 null 或空字符串，则创建一个局部互斥体。 
        /// 如果名称以前缀“Global\”开头，则 mutex 在所有终端服务器会话中均为可见。 
        /// 如果名称以前缀“Local\”开头，则 mutex 仅在创建它的终端服务器会话中可见。 
        /// 如果创建已命名 mutex 时不指定前缀，则它将采用前缀“Local\”。)</param>
        /// <param name="action">同步处理操作</param>
        /// <param name="recursive">指示当前调用是否为递归处理，递归处理时检测到异常则抛出异常，避免进入无限递归</param>
        private static void MutexExec(string filePath, Action action, bool recursive = false)
        {
            //生成文件对应的同步键，可自定义格式（互斥体名称对特殊字符支持不友好，遂转换为BASE64格式字符串）
            var fileKey = Convert.ToBase64String(Encoding.Default.GetBytes(Path.Combine("FILE", filePath)));
            //转换为操作系统级的同步键
            var mutexKey = Path.Combine("Global", fileKey);
            //声明一个已命名的互斥体，实现进程间同步；该命名互斥体不存在则自动创建，已存在则直接获取
            //initiallyOwned: false：默认当前线程并不拥有已存在互斥体的所属权，即默认本线程并非为首次创建该命名互斥体的线程
            //注意：并发声明同名的命名互斥体时，若间隔时间过短，则可能同时声明了多个名称相同的互斥体，并且同名的多个互斥体之间并不同步，高并发用户请另行处理
            using (var mut = new Mutex(false, mutexKey))
            {
                try
                {
                    //上锁，其他线程需等待释放锁之后才能执行处理；若其他线程已经上锁或优先上锁，则先等待其他线程执行完毕
                    mut.WaitOne();
                    //执行处理代码（在调用WaitHandle.WaitOne至WaitHandle.ReleaseMutex的时间段里，只有一个线程处理，其他线程都得等待释放锁后才能执行该代码段）
                    action();
                }
                //当其他进程已上锁且没有正常释放互斥锁时(譬如进程忽然关闭或退出)，则会抛出AbandonedMutexException异常
                catch (AbandonedMutexException)
                {
                    //避免进入无限递归
                    if (recursive) throw;

                    //非递归调用，由其他进程抛出互斥锁解锁异常时，重试执行
                    MutexExec(mutexKey, action, true);
                }
                finally
                {
                    //释放锁，让其他进程(或线程)得以继续执行
                    mut.ReleaseMutex();
                }
            }
        }

        #endregion

    }
}
