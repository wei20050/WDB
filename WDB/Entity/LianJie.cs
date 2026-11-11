using NetFrameWork.Database;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WDB.Entity
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class LianJie
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        public string Port { get; set; }
        public string ServiceName { get; set; }

        /// <summary>
        /// 初始数据库(可选):服务器级连接时使用,展开节点后可浏览该服务器上的其它数据库
        /// </summary>
        public string DataBaseName { get; set; }
        public string UserName { get; set; }
        public string PassWord { get; set; }
        public ORM.DbTypeEnum DataBaseType { get; set; }
        public string NoDbType { get; set; }

        #region NoDb(MongoDB)连接参数

        /// <summary>
        /// MongoDB 认证库(authSource):为空时账号认证默认走 admin
        /// </summary>
        public string AuthSource { get; set; }

        /// <summary>
        /// MongoDB 认证机制(authMechanism):为空时由驱动自动协商(SCRAM-SHA-256/SCRAM-SHA-1)
        /// </summary>
        public string AuthMechanism { get; set; }

        /// <summary>
        /// MongoDB 附加连接参数(URL 查询串形式),如 retryWrites=true&amp;readPreference=secondary
        /// </summary>
        public string UrlParams { get; set; }

        #endregion

        /// <summary>
        /// 当前上下文下已加载的表/视图(运行时数据,不写入连接配置)
        /// </summary>
        [JsonIgnore]
        public List<Biao> BiaoList { get; set; }

        /// <summary>
        /// 连接级过滤:存在数据库层级时过滤数据库名,不存在(Oracle/Sqlite/达梦)时过滤表名
        /// </summary>
        public bool IsFilter { get; set; }
        public List<string> IncludingList { get; set; }
        public List<string> ExcludeList { get; set; }

        /// <summary>
        /// 各数据库下的过滤规则(键为数据库名):
        /// 存在模式层级时过滤模式名,不存在模式层级(如MySQL)时过滤表名
        /// </summary>
        public Dictionary<string, FilterConfig> DbFilterDict { get; set; }

        /// <summary>
        /// 各模式下的表过滤规则(键为"数据库名.模式名"),仅存在模式层级的类型使用
        /// </summary>
        public Dictionary<string, FilterConfig> TableFilterDict { get; set; }
    }
}
