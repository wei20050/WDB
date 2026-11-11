namespace WDB.Entity
{
    /// <summary>
    /// AI 模型配置项:一套"接口地址 + 模型名 + 密钥",可保存多套随时切换
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class AiModel
    {
        public string Id { get; set; }

        /// <summary>
        /// 配置名称:下拉里显示,用于区分不同服务商/用途
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 接口地址:可只填到 /v1,发送时会自动补全 /chat/completions
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 模型名称,如 DeepSeek-V4.1-Flash
        /// </summary>
        public string Model { get; set; }

        public string ApiKey { get; set; }
    }
}
