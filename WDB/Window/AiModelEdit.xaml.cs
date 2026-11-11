using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using WDB.Common;
using WDB.Entity;

namespace WDB.Window
{
    /// <summary>
    /// 模型配置弹窗:新增/修改/复制共用。绑定传入的模型对象(调用方传克隆,取消不会污染原列表)。
    /// 保存时校验名称非空且不与现有配置重名(排除自身 Id)。
    /// </summary>
    public partial class AiModelEdit
    {
        private readonly AiModel _m;
        private readonly List<AiModel> _existing;

        public AiModelEdit(AiModel src, List<AiModel> existing, string title)
        {
            InitializeComponent();
            _m = src;
            _existing = existing;
            DataContext = _m;
            Title = title;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            var name = (_m.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = _m.Model;
            }
            if (_existing != null && _existing.Exists(p => p.Id != _m.Id &&
                string.Equals((p.Name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase)))
            {
                Msg.Warning($"已存在名为「{name}」的配置,名称不能重复");
                return;
            }
            _m.Name = name;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 测试:用当前填写(未保存)的地址/模型/密钥发一次最小请求,验证配置是否可用。
        /// 请求放后台线程,避免界面卡死;结果用后台线程回调,Msg 内部会切回 UI 线程。
        /// </summary>
        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            var url = (_m.Url ?? string.Empty).Trim();
            var model = (_m.Model ?? string.Empty).Trim();
            var apikey = (_m.ApiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                Msg.Warning("请先填写接口地址");
                return;
            }
            if (string.IsNullOrWhiteSpace(model))
            {
                Msg.Warning("请先填写模型名称");
                return;
            }
            SetBusy(true);
            _ = Task.Factory.StartNew(new Action(() =>
            {
                try
                {
                    //不带系统提示词,只要一个极短回复:温度给 0、输出长度给最小值以便快速返回
                    var ret = AIHelp.Ask(url, model, apikey, null, "你好", 0, 64);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetBusy(false);
                        Msg.Ok("测试成功,模型已正常返回:" + Short(ret));
                    }));
                }
                catch (Exception ex)
                {
                    Log.Error("AI模型测试失败:" + ex);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        SetBusy(false);
                        Msg.Error("测试失败:" + ex.Message);
                    }));
                }
            }));
        }

        /// <summary>
        /// 切换"测试中"状态:显示遮罩并禁用测试按钮,防止重复点击
        /// </summary>
        private void SetBusy(bool busy)
        {
            LoadingG.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            BtnTest.IsEnabled = !busy;
        }

        /// <summary>
        /// 回复正文压成一行并截断,避免弹窗被长回复撑开
        /// </summary>
        private static string Short(string text)
        {
            var s = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (string.IsNullOrWhiteSpace(s))
            {
                return "模型已响应(无正文)";
            }
            return s.Length > 80 ? s.Substring(0, 80) + "..." : s;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
