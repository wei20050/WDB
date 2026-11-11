using System;
using WDB.Common;

namespace WDB.Window
{
    public partial class GuanYu
    {
        public GuanYu()
        {
            InitializeComponent();
            txtJqm.Text = RegHelp.GetJqm();
            var reg = RegHelp.Reg;
            labJhzt.Content = $"软件{GetJhztText(reg.Jhzt)},到期时间:{reg.Gqsj:yyyy年MM月dd日}";
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        }

        private void BtnGuanBi(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }

        private void BtnFuzhi(object sender, System.Windows.RoutedEventArgs e)
        {
            SafeClipboard.SetText(txtJqm.Text);
            Oops.Ok("机器码已复制到剪切板");
        }

        private void BtnJihuo(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtJhm.Text))
                {
                    Oops.Warning("请输入激活码");
                    return;
                }
                var regData = RegHelp.StrToRegData(txtJhm.Text, true);
                if (RegHelp.GetJqm() == regData.Jqm)
                {
                    regData.Zcsj = DateTime.Now;
                    regData.Sysj = DateTime.Now;
                    RegHelp.SetReg(regData);
                    Oops.Ok("软件激活成功");
                }
                else
                {
                    Msg.Warning("机器码不符");
                }
            }
            catch
            {
                //这里肯定是注册码有问题吃掉异常
                Msg.Warning("激活码无效");
            }
            var reg = RegHelp.Reg;
            labJhzt.Content = $"软件{GetJhztText(reg.Jhzt)},到期时间:{reg.Gqsj:yyyy年MM月dd日}";
        }

        /// <summary>
        /// 激活状态文案(0:未激活 1:激活 2:已过期)
        /// </summary>
        private static string GetJhztText(int jhzt)
        {
            switch (jhzt)
            {
                case 1:
                    return "已激活";
                case 2:
                    return "已过期";
                default:
                    return "试用中";
            }
        }
    }
}
