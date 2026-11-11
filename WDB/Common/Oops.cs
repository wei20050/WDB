using HandyControl.Controls;
using HandyControl.Data;

namespace WDB.Common
{
    public static class Oops
    {

        /// <summary>
        /// 成功提示信息
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="showDateTime"></param>
        public static void Ok(string msg, bool showDateTime = false)
        {
            var growInfo = new GrowlInfo
            {
                Message = msg,
                ShowDateTime = showDateTime,
                WaitTime = 1
            };
            Growl.Success(growInfo);
        }

        /// <summary>
        /// 成功全局提示信息
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="showDateTime"></param>
        public static void OkGlobal(string msg, bool showDateTime = false)
        {
            var growInfo = new GrowlInfo
            {
                Message = msg,
                ShowDateTime = showDateTime,
                WaitTime = 1
            };
            Growl.SuccessGlobal(growInfo);
        }

        /// <summary>
        /// 警示、提醒、提示信息
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="showDateTime"></param>
        public static void Warning(string msg, bool showDateTime = false)
        {
            var growInfo = new GrowlInfo
            {
                Message = msg,
                ShowDateTime = showDateTime,
                WaitTime = 1
            };
            Growl.Warning(growInfo);
        }

        /// <summary>
        /// 警示、提醒、提示全局信息
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="showDateTime"></param>
        public static void WarningGlobal(string msg, bool showDateTime = false)
        {
            var growInfo = new GrowlInfo
            {
                Message = msg,
                ShowDateTime = showDateTime,
                WaitTime = 1
            };
            Growl.WarningGlobal(growInfo);
        }

        /// <summary>
        /// 错误、异常提示信息
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="showDateTime"></param>
        public static void Error(string msg, bool showDateTime = false)
        {
            var growInfo = new GrowlInfo
            {
                Message = msg,
                ShowDateTime = showDateTime,
                WaitTime = 1
            };
            Growl.Error(growInfo);
        }

        /// <summary>
        /// 错误、异常全局提示信息
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="showDateTime"></param>
        public static void ErrorGlobal(string msg, bool showDateTime = false)
        {
            var growInfo = new GrowlInfo
            {
                Message = msg,
                ShowDateTime = showDateTime,
                WaitTime = 1
            };
            Growl.ErrorGlobal(growInfo);
        }
    }
}
