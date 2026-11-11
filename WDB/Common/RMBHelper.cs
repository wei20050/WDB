using System;
using System.Text;

namespace WDB.Common
{

    /// <summary>
    /// 人民币大写转换工具类
    /// </summary>
    public static class RMBHelper
    {
        /// <summary>
        /// 将数字金额转换为人民币大写（支持整数/小数，最大处理到万亿级）
        /// </summary>
        /// <param name="amount">金额（支持正数，负数会自动取绝对值）</param>
        /// <returns>人民币大写字符串（如：壹佰贰拾叁元肆角伍分）</returns>
        /// <exception cref="ArgumentOutOfRangeException">金额超出处理范围时抛出</exception>
        public static string ToChineseRMB(decimal amount)
        {
            // 1. 处理负数（取绝对值）
            amount = Math.Abs(amount);

            // 2. 校验金额范围（最大支持 9999999999999.99，即万亿级）
            const decimal MaxAmount = 9999999999999.99m;
            if (amount > MaxAmount)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), $"金额超出处理范围（0 ~ {MaxAmount:F2}）");
            }

            // 3. 财务四舍五入保留2位小数
            decimal roundedAmount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

            // 拆分整数、小数部分，不使用long强转避免溢出风险
            string totalStr = roundedAmount.ToString("F2");
            string[] parts = totalStr.Split('.');
            string integerStr = parts[0];
            string decimalStr = parts[1];

            int jiao = int.Parse(decimalStr[0].ToString());
            int fen = int.Parse(decimalStr[1].ToString());
            int decimalPart = jiao * 10 + fen;

            // 4. 定义大写数字和单位
            char[] chineseNumbers = { '零', '壹', '贰', '叁', '肆', '伍', '陆', '柒', '捌', '玖' };
            string[] integerUnits = { "", "拾", "佰", "仟", "万", "拾", "佰", "仟", "亿", "拾", "佰", "仟", "万" };
            string[] decimalUnits = { "角", "分" };

            StringBuilder integerSb = new StringBuilder();
            bool lastZero = true;       // 上一位是否为0
            bool needInsertZero = false;// 是否需要插入零

            if (integerStr != "0")
            {
                int len = integerStr.Length;
                for (int i = 0; i < len; i++)
                {
                    int digit = int.Parse(integerStr[i].ToString());
                    int unitIdx = len - 1 - i;

                    if (digit == 0)
                    {
                        needInsertZero = true;
                        // 万、亿单位位保留单位，不写零
                        if (unitIdx == 4 || unitIdx == 8 || unitIdx == 12)
                        {
                            // 单位位，有内容才追加单位
                            if (integerSb.Length > 0)
                            {
                                integerSb.Append(integerUnits[unitIdx]);
                            }
                            needInsertZero = false;
                        }
                        lastZero = true;
                    }
                    else
                    {
                        // 需要补零并且上一位不是单位分隔，插入零
                        if (needInsertZero && !lastZero)
                        {
                            integerSb.Append(chineseNumbers[0]);
                        }
                        integerSb.Append(chineseNumbers[digit]);
                        integerSb.Append(integerUnits[unitIdx]);
                        needInsertZero = false;
                        lastZero = false;
                    }
                }
            }
            else
            {
                integerSb.Append(chineseNumbers[0]);
            }

            integerSb.Append("元");

            // 处理角分部分
            StringBuilder decimalSb = new StringBuilder();
            if (decimalPart == 0)
            {
                decimalSb.Append("整");
            }
            else
            {
                // 角
                if (jiao > 0)
                {
                    decimalSb.Append(chineseNumbers[jiao]);
                    decimalSb.Append(decimalUnits[0]);
                }
                else
                {
                    // 角为0，不管整数是否为0都要补零：0.05 → 零元零伍分
                    decimalSb.Append(chineseNumbers[0]);
                }

                // 分
                if (fen > 0)
                {
                    decimalSb.Append(chineseNumbers[fen]);
                    decimalSb.Append(decimalUnits[1]);
                }
            }

            string result = integerSb + decimalSb.ToString();

            // 特殊：零元零角场景，清理多余：零元零整 → 零元整
            result = result.Replace("零整", "整");

            return result;
        }

        /// <summary>
        /// 重载：支持double类型（不推荐业务金额使用，double存在浮点精度问题）
        /// </summary>
        /// <param name="amount">double金额</param>
        /// <returns>人民币大写字符串</returns>
        /// <exception cref="ArgumentOutOfRangeException">金额超出处理范围</exception>
        /// <remarks>double二进制浮点存在精度丢失，优先使用decimal版本</remarks>
        public static string ToChineseRMB(double amount)
        {
            // 先转字符串再解析decimal，规避double二进制浮点精度bug
            string tempStr = amount.ToString("F10");
            if (!decimal.TryParse(tempStr, out decimal decVal))
            {
                throw new ArgumentException($"double金额无法转换为decimal，value={amount}", nameof(amount));
            }
            return ToChineseRMB(decVal);
        }

        /// <summary>
        /// 重载：支持int整数金额
        /// </summary>
        /// <param name="amount">整数金额</param>
        /// <returns>人民币大写字符串</returns>
        /// <exception cref="ArgumentOutOfRangeException">金额超出处理范围</exception>
        public static string ToChineseRMB(int amount)
        {
            return ToChineseRMB((decimal)amount);
        }

        /// <summary>
        /// 重载：字符串金额入参，支持 "123" "123.45" "-100.00"
        /// </summary>
        /// <param name="amountStr">金额字符串</param>
        /// <returns>人民币大写字符串</returns>
        /// <exception cref="ArgumentNullException">输入null</exception>
        /// <exception cref="ArgumentException">格式非法、空白</exception>
        /// <exception cref="ArgumentOutOfRangeException">金额超出处理范围</exception>
        public static string ToChineseRMB(string amountStr)
        {
            if (amountStr == null)
                throw new ArgumentNullException(nameof(amountStr));

            string trimStr = amountStr.Trim();
            if (string.IsNullOrEmpty(trimStr))
                throw new ArgumentException("金额字符串不能为空或全空白字符", nameof(amountStr));

            if (!decimal.TryParse(trimStr, out decimal amount))
            {
                throw new ArgumentException($"金额字符串格式不正确：{amountStr}", nameof(amountStr));
            }

            return ToChineseRMB(amount);
        }

    }
}
