using System;

namespace WDB.Common
{

    public static class IdCardGenerator
    {
        private static readonly Random _random = new Random();
        /// <summary>
        /// 生成18位模拟身份证号
        /// </summary>
        /// <param name="areaCode">省市区6位代码</param>
        /// <param name="birthday">出生日期</param>
        /// <param name="isMale">1=男，2=女</param>
        /// <returns>合法18位身份证号</returns>
        public static string Generate(string areaCode, string birthday, string isMale)
        {

            // 生成顺序码（3位）
            // 男：奇数，女：偶数
            int seqCode;
            if (isMale == "1")
            {
                // 生成 001~999 之间的奇数
                seqCode = _random.Next(0, 500) * 2 + 1;
            }
            else
            {
                // 生成 001~999 之间的偶数
                seqCode = _random.Next(0, 500) * 2;
            }
            string seqStr = seqCode.ToString("D3"); // 不足3位补0

            // 拼接前17位
            string body = $"{areaCode}{birthday}{seqStr}";

            // 计算第18位校验码
            char checkCode = CalcCheckCode(body);

            // 最终18位身份证
            return body + checkCode;
        }

        /// <summary>
        /// 计算身份证第18位校验码（国标算法）
        /// </summary>
        private static char CalcCheckCode(string body17)
        {
            // 加权因子
            int[] weights = { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };
            // 校验码对应表
            char[] checkCodes = { '1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2' };

            int sum = 0;
            for (int i = 0; i < 17; i++)
            {
                sum += (body17[i] - '0') * weights[i];
            }

            int mod = sum % 11;
            return checkCodes[mod];
        }
    }
}
