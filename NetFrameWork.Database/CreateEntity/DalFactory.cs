using static NetFrameWork.Database.ORM;

namespace NetFrameWork.Database.CreateEntity
{
    /// <summary>
    /// 数据库操作工厂类
    /// </summary>
    public class DalFactory
    {
        /// <summary>
        /// 创建Dal(默认mysql)
        /// </summary>
        /// <param name="dbType">数据库类型，如SQLite、MySql</param>
        public static IDal CreateDal(DbTypeEnum dbType = DbTypeEnum.MySql)
        {
            return dbType switch
            {
                DbTypeEnum.SqlIte => new SqLiteDal(),
                DbTypeEnum.MySql => new MySqlDal(),
                DbTypeEnum.SqlServer => new MssqlDal(),
                DbTypeEnum.Oracle => new OracleDal(),
                DbTypeEnum.Dameng => new DmDal(),
                DbTypeEnum.Pgsql => new PgSqlDal(),
                DbTypeEnum.Kingbase => new KdbDal(),
                DbTypeEnum.GaussDB => new GaussDBDal(),
                _ => new MySqlDal()
            };
        }
    }
}
