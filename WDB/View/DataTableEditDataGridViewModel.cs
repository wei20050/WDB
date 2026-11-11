using System.Data;
using WDB.Entity;

namespace WDB.View
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class DataTableEditDataGridViewModel
    {
        public LianJie Lj { get; set; }
        public DataTable Dt { get; set; }

        /// <summary>
        /// 非 SQL 连接(MongoDB)的表格编辑器:为 null 时按 SQL 生成/执行
        /// </summary>
        public MongoDataGridEditor MongoEditor { get; set; }

        /// <summary>
        /// 只读:查询画面(SQL编辑器页)的结果集可能是多表连接出来的,直接改数据会落到猜出来的目标表上;
        /// 该场景整表只读,改数据走"查看数据"页
        /// </summary>
        public bool ReadOnly { get; set; }
    }
}
