using System;
using System.Text;
using System.Runtime.InteropServices;
namespace WDB.Common
{
    public static class SafeClipboard
    {
        // 导入 Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;
        private const uint GMEM_ZEROINIT = 0x0040;

        /// <summary>
        /// 安全地设置剪贴板文本（直接调用 Win32 API）
        /// </summary>
        public static void SetText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // 尝试多次，因为有时剪贴板会被系统短暂锁定
            for (int i = 0; i < 5; i++)
            {
                // 1. 打开剪贴板 (传入 IntPtr.Zero 表示与当前任务关联)
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        // 2. 清空剪贴板
                        EmptyClipboard();
                        // 3. 分配内存并复制字符串
                        byte[] bytes = Encoding.Unicode.GetBytes(text + "\0"); // 记得加结束符
                        IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (UIntPtr)bytes.Length);
                        if (hGlobal == IntPtr.Zero) return;
                        IntPtr pointer = GlobalLock(hGlobal);
                        if (pointer != IntPtr.Zero)
                        {
                            Marshal.Copy(bytes, 0, pointer, bytes.Length);
                            GlobalUnlock(hGlobal);
                            // 4. 设置数据
                            if (SetClipboardData(CF_UNICODETEXT, hGlobal) != IntPtr.Zero)
                            {
                                // 成功！注意：SetClipboardData 成功后，系统会接管 hGlobal，不要释放它
                                return;
                            }
                        }
                        // 如果失败了，我们需要自己释放内存
                        GlobalFree(hGlobal);
                        return;
                    }
                    finally
                    {
                        // 5. 无论如何都要关闭剪贴板！
                        CloseClipboard();
                    }
                }
                // 如果没打开，稍微等一下再试
                System.Threading.Thread.Sleep(20);
            }
        }

        /// <summary>
        /// 安全读取剪贴板文本，重试机制，返回剪贴板字符串；无文本/失败返回空字符串
        /// </summary>
        /// <returns></returns>
        public static string GetText()
        {
            // 重试5次，应对剪贴板被其它进程占用
            for (int i = 0; i < 5; i++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
                        {
                            return string.Empty;
                        }

                        IntPtr hMem = GetClipboardData(CF_UNICODETEXT);
                        if (hMem == IntPtr.Zero)
                        {
                            continue;
                        }

                        IntPtr lpStr = GlobalLock(hMem);
                        if (lpStr == IntPtr.Zero)
                        {
                            continue;
                        }
                        try
                        {
                            // 直接从非托管指针读取 Unicode 字符串
                            return Marshal.PtrToStringUni(lpStr);
                        }
                        finally
                        {
                            GlobalUnlock(hMem);
                        }
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                System.Threading.Thread.Sleep(20);
            }
            return string.Empty;
        }
    }
}
