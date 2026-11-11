using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace WDB.Common
{
    /// <summary>
    /// 启动耗时跟踪:记录各阶段的时间点,用于定位启动慢的环节。
    /// 默认关闭(Enabled=false),只有带 -startup-trace 命令行参数启动时才写日志,正常使用时无额外开销。
    /// 各阶段先缓存在内存,最后统一写入日志,避免日志写盘本身影响测量结果。
    /// </summary>
    public static class StartupTrace
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();
        private static readonly DateTime ProcessStart = GetProcessStartTime();
        private static readonly List<string> Buffer = new List<string>();
        private static long _last;

        /// <summary>
        /// 是否启用跟踪(命令行参数 -startup-trace)
        /// </summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// AssemblyResolve 被触发的次数(依赖 DLL 不在 exe 同级目录时,每次加载都会走托管回调)
        /// </summary>
        public static int AssemblyResolveCount;

        /// <summary>
        /// AssemblyResolve 里 File.Exists + Assembly.LoadFrom 的累计耗时
        /// </summary>
        public static long AssemblyResolveMs;

        public static void Mark(string stage)
        {
            Buffer.Add($"[startup] {DateTime.Now:HH:mm:ss.fff} {stage}: 进程启动后 {SinceProcessStart()}ms, 距上一阶段 {Watch.ElapsedMilliseconds - _last}ms");
            _last = Watch.ElapsedMilliseconds;
        }

        /// <summary>
        /// 把缓存的阶段耗时统一写入日志(此时日志目录已确定)
        /// </summary>
        public static void Flush()
        {
            if (!Enabled || Buffer.Count == 0)
            {
                return;
            }
            try
            {
                Log.Info("========== 启动耗时跟踪 ==========");
                foreach (var line in Buffer)
                {
                    Log.Info(line);
                }
                Log.Info($"[startup] 依赖程序集解析(AssemblyResolve): {AssemblyResolveCount} 次, 累计 {AssemblyResolveMs}ms");
                Log.Info($"========== 启动完成,共 {Watch.ElapsedMilliseconds}ms ==========");
            }
            catch
            {
                //跟踪日志失败不影响启动
            }
            finally
            {
                Buffer.Clear();
            }
        }

        private static long SinceProcessStart()
        {
            try
            {
                return (long)(DateTime.Now - ProcessStart).TotalMilliseconds;
            }
            catch
            {
                return Watch.ElapsedMilliseconds;
            }
        }

        private static DateTime GetProcessStartTime()
        {
            try
            {
                return Process.GetCurrentProcess().StartTime;
            }
            catch
            {
                return DateTime.Now;
            }
        }
    }
}
