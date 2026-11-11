using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace WDB.Common
{
    public class RegHelp
    {
        private static string AppId => "12F65BC3-7DD0-4D11-97D6-55D613E137A3";
        private static string AesKey => "97D655D613E137A3";
        private static int Sy => 30;//试用天数

        public static RegData Reg => Check();

        /// <summary>
        /// 检查注册状态返回 机器码_注册时间_使用时间_过期时间_激活状态(0:未激活 1:激活 2:已过期)
        /// 激活状态每次都根据到期日重新推导:只有确实过了到期日才返回2(已过期),
        /// 不会因为一次系统时间异常/机器码变动就被永久标记成已过期
        /// </summary>
        /// <returns></returns>
        public static RegData Check()
        {
            var now = DateTime.Now;
            var jqm = GetJqm();
            RegData regData = null;
            if (now < DateTime.Parse("2026-01-01"))
            {
                Msg.Error("当前计算机时间异常");
                //电脑时间被修改 直接结束程序
                Environment.Exit(0);
            }
            try
            {
                regData = GetReg();
            }
            catch
            {
                Msg.Error("软件相关注册表异常");
                //注册表被修改 直接结束程序
                Environment.Exit(0);
            }
            //本机没有记录(首次运行)或记录属于其它机器(机器码不符):按本机重新开始试用,而不是判为已过期
            if (regData == null || jqm != regData.Jqm)
            {
                if (regData != null)
                {
                    Log.Info($"注册记录机器码不符({regData.Jqm}->{jqm}),已按本机重新开始试用");
                }
                regData = new RegData
                {
                    Jqm = jqm,
                    Zcsj = now,
                    Sysj = now,
                    Gqsj = now.AddDays(Sy).Date,
                    Jhzt = 0
                };
                SetReg(regData);
                return regData;
            }
            if (regData.Gqsj.Date < now.Date)
            {
                //过了到期日才算过期(到期日当天仍可正常使用)
                regData.Jhzt = 2;
            }
            else
            {
                //未过期:保留已激活状态,其余一律按未激活(试用中)处理,避免历史遗留的过期标记无法恢复
                regData.Jhzt = regData.Jhzt == 1 ? 1 : 0;
                //正常情况 更新使用时间
                regData.Sysj = now;
            }
            SetReg(regData);
            return regData;
        }

        public static void SetReg(RegData regData)
        {
            //过期时间只做日期取整;旧逻辑每次保存都再+1天,会把到期日不断往后推
            regData.Gqsj = regData.Gqsj.Date;
            RegistryKey key = Registry.CurrentUser;
            RegistryKey myreg = key.CreateSubKey("software\\" + AppId);
            var str = RegDataToStr(regData);
            myreg.SetValue(AppId, str);
            myreg.Close();
            key.Close();
        }

        private static RegData GetReg()
        {
            string str = string.Empty;
            RegistryKey key = Registry.CurrentUser;
            RegistryKey myreg = key.OpenSubKey("software\\" + AppId);
            if (myreg != null)
            {
                str = myreg.GetValue(AppId).ToString();
                myreg.Close();
            }
            key.Close();
            return StrToRegData(str);
        }

        public static RegData StrToRegData(string str, bool isZcm = false)
        {
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }
            str = AesDecrypt(str, AesKey);
            var strArr = str.Split('_');
            RegData ret = null;
            if (isZcm)
            {
                ret = new RegData
                {
                    Jqm = strArr[1],
                    Gqsj = DateTime.Parse(strArr[0]),
                    Jhzt = int.Parse(strArr[3])
                };
            }
            else
            {
                ret = new RegData
                {
                    Jqm = strArr[0],
                    Gqsj = DateTime.Parse(strArr[3]),
                    Jhzt = int.Parse(strArr[4]),
                    Zcsj = DateTime.Parse(strArr[1]),
                    Sysj = DateTime.Parse(strArr[2])
                };
            }
            return ret;
        }

        public static string RegDataToStr(RegData regData)
        {
            var ret = AesEncrypt($"{regData.Jqm}_{regData.Zcsj}_{regData.Sysj}_{regData.Gqsj}_{regData.Jhzt}", AesKey);
            return ret;
        }

        /// <summary>
        /// 获取机器码
        /// </summary>
        /// <returns></returns>
        public static string GetJqm()
        {
            var ypm = GetHardDiskID();
            return Core(ypm);
        }

        /// <summary>
        /// 获取硬盘ID代码(即C盘卷序列号)。
        /// 以前用 WMI(System.Management) 查询,首次调用要初始化 WMI 服务栈,冷启动会额外增加 100~300ms;
        /// 直接调用 Win32 GetVolumeInformation 拿到的值与 WMI 完全一致(8位大写十六进制),但基本不耗时。
        /// </summary>
        /// <returns></returns>
        private static string GetHardDiskID()
        {
            if (GetVolumeInformation(@"C:\", null, 0, out var serial, out _, out _, null, 0))
            {
                return serial.ToString("X8");
            }
            throw new InvalidOperationException($"读取C盘卷序列号失败,错误码:{Marshal.GetLastWin32Error()}");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeInformation(
            string rootPathName,
            StringBuilder volumeNameBuffer,
            int volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer,
            int fileSystemNameSize);

        /// <summary>
        /// 核心加密
        /// </summary>
        /// <param name="strText">待加密的文本</param>
        /// <returns></returns>
        private static string Core(string strText)
        {
            return Md5(AesEncrypt(Md5(strText), Md5("252819980E064F819D3C2860C65F7B05"))).Substring(8, 18);
        }

        /// <summary>
        /// 32位的MD5加密
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string Md5(string input)
        {
            var md5 = MD5.Create();
            var data = md5.ComputeHash(Encoding.Default.GetBytes(input));
            var sb = new StringBuilder();
            foreach (var t in data)
            {
                sb.AppendFormat("{0:X2}", t);
            }
            return sb.ToString();
        }

        /// <summary>
        /// AES加密(加密模式ECB,填充模式pk-cs5padding,数据块128位,偏移量无,输出16进制,字符集UTF8)
        /// </summary>
        /// <param name="text">加密字符</param>
        /// <param name="key">加密的key(必须是16的整数倍)</param>
        /// <returns></returns>
        private static string AesEncrypt(string text, string key)
        {
            var keyArray = Encoding.UTF8.GetBytes(key);
            var toEncryptArray = Encoding.UTF8.GetBytes(text);
            var rDel = new RijndaelManaged { Key = keyArray, Mode = CipherMode.ECB, Padding = PaddingMode.PKCS7 };
            var cTransform = rDel.CreateEncryptor();
            var resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return BytesToHex(resultArray);
        }

        /// <summary>
        /// AES解密(加密模式ECB,填充模式pk-cs5padding,数据块128位,偏移量无,输出16进制,字符集UTF8)
        /// </summary>
        /// <param name="text">解密字符</param>
        /// <param name="key">解密的key(必须是16的整数倍)</param>
        /// <returns></returns>
        private static string AesDecrypt(string text, string key)
        {
            var keyArray = Encoding.UTF8.GetBytes(key);
            var toEncryptArray = HexToBytes(text);
            var rDel = new RijndaelManaged { Key = keyArray, Mode = CipherMode.ECB, Padding = PaddingMode.PKCS7 };
            var cTransform = rDel.CreateDecryptor();
            var resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return Encoding.UTF8.GetString(resultArray);
        }

        /// <summary>
        /// 16进制字符串转Byte数组
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        private static byte[] HexToBytes(string hex)
        {
            var num = (int)Math.Round((double)hex.Length / 2);
            var buffer = new byte[num];
            var num3 = num - 1;
            for (var i = 0; i <= num3; i++)
            {
                var s = hex.Substring(i * 2, 2);
                buffer[i] = (byte)int.Parse(s, NumberStyles.HexNumber);
            }
            return buffer;
        }

        /// <summary>
        /// Byte数组转16进制字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static string BytesToHex(IList<byte> bytes)
        {
            var builder = new StringBuilder();
            var num2 = bytes.Count - 1;
            for (var i = 0; i <= num2; i++)
            {
                builder.AppendFormat("{0:X2}", bytes[i]);
            }
            return builder.ToString();
        }


    }

    public class RegData
    {
        /// <summary>
        /// 机器码
        /// </summary>
        public string Jqm { set; get; }
        /// <summary>
        /// 注册时间
        /// </summary>
        public DateTime Zcsj { set; get; }
        /// <summary>
        /// 使用时间
        /// </summary>
        public DateTime Sysj { set; get; }
        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTime Gqsj { set; get; }
        /// <summary>
        /// 激活状态(0:未激活 1:激活 2:试用期已过)
        /// </summary>
        public int Jhzt { set; get; }
    }
}
