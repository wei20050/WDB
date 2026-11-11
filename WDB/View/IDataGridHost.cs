namespace WDB.View
{
    /// <summary>
    /// 承载数据表格的页面(新的"查看数据"页),表格右键菜单通过该接口把筛选/排序/加载状态回传给宿主页面
    /// </summary>
    public interface IDataGridHost
    {
        /// <summary>
        /// 显示/隐藏加载遮罩
        /// </summary>
        void SetLoading(bool visible);

        /// <summary>
        /// 把表格右键生成的筛选条件加进[筛选&排序]面板并直接执行
        /// </summary>
        /// <param name="condition">形如 `列`='值'、`列`LIKE'%值%'、`列`IS NULL</param>
        void ApplyFilter(string condition);

        /// <summary>
        /// 把表格右键生成的排序条件加进[筛选&排序]面板并直接执行
        /// </summary>
        /// <param name="orderBy">形如 " ORDER BY `列` ASC"</param>
        void ApplyOrderBy(string orderBy);
    }
}
