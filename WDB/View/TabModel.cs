namespace WDB.View
{
    public class TabModel
    {
        public string Header { get; set; }

        /// <summary>
        /// 标签页唯一标识:同一连接下的同一对象复用同一个标签(为空表示不参与去重)
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 是否允许关闭(首页等固定页为 false:隐藏关闭按钮且不参与关闭操作)
        /// </summary>
        public bool CanClose { get; set; } = true;

        public object Content { get; set; }
    }
}
