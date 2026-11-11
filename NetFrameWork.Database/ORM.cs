using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.Objects.DataClasses;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Dm;
using NetFrameWork.Database.CreateEntity;
using NetFrameWork.Database.SqlFunction;
using Kdbndp;
using Npgsql;
using OpenGauss.NET;
using System.Data.SQLite;
using MySqlConnector;

namespace NetFrameWork.Database
{

    /// <summary>
    /// 数据库操作类
    /// </summary>
    public class ORM
    {

        #region 数据库类型枚举

        /// <summary>
        /// 数据库类型枚举
        /// </summary>
        public enum DbTypeEnum
        {
            /// <summary>
            /// SqlIte数据库
            /// </summary>
            SqlIte = 0,
            /// <summary>
            /// Mysql数据库
            /// </summary>
            MySql = 1,
            /// <summary>
            /// SqlServer数据库
            /// </summary>
            SqlServer = 2,
            /// <summary>
            /// Oracle数据库
            /// </summary>
            Oracle = 3,
            /// <summary>
            /// 达梦数据库
            /// </summary>
            Dameng = 4,
            /// <summary>
            /// 人大金仓数据库
            /// </summary>
            Kingbase = 5,
            /// <summary>
            /// Pgsql数据库
            /// </summary>
            Pgsql = 6,
            /// <summary>
            /// 高斯数据库
            /// </summary>
            GaussDB
        }

        #endregion

        #region 构造

        /// <inheritdoc />
        /// <summary>
        /// 无参构造(默认数据库连接字符串名称:DbContext)
        /// </summary>
        public ORM() : this("DbContext") { }

        /// <summary>
        /// 带连接字符串名称构造
        /// </summary>
        /// <param name="dbConnectionName">数据库连接字符串Name</param>
        public ORM(string dbConnectionName) : this(ConfigurationManager.ConnectionStrings[dbConnectionName]) { }

        /// <summary>
        /// 带连接对象构造
        /// </summary>
        /// <param name="cm">数据库连接字符串对象</param>
        public ORM(ConnectionStringSettings cm)
        {
            try
            {
                var sqlLogCm = ConfigurationManager.ConnectionStrings["SqlLog"];
                if (sqlLogCm != null)
                {
                    _sqlLog = sqlLogCm.ConnectionString == "1" || sqlLogCm.ConnectionString.ToLower() == "true";
                }
                if (cm == null)
                {
                    throw new Exception("未找到连接字符串");
                }
                var connectionString = cm.ToString();
                var pn = cm.ProviderName;
                if (pn.Contains("System.Data.SQLite"))
                {
                    MDbType = DbTypeEnum.SqlIte;
                }
                else if (pn.Contains("MySql.Data.MySqlClient"))
                {
                    MDbType = DbTypeEnum.MySql;
                }
                else if (pn.Contains("System.Data.SqlClient"))
                {
                    MDbType = DbTypeEnum.SqlServer;
                }
                else if (pn.Contains("System.Data.OracleClient"))
                {
                    MDbType = DbTypeEnum.Oracle;
                }
                else if (pn.Contains("System.Data.Dm"))
                {
                    MDbType = DbTypeEnum.Dameng;
                }
                else if (pn.Contains("System.Data.Kdb"))
                {
                    MDbType = DbTypeEnum.Kingbase;
                }
                else if (pn.Contains("System.Data.Npgsql"))
                {
                    MDbType = DbTypeEnum.Pgsql;
                }
                else if (pn.Contains("System.Data.GaussDB") || pn.Contains("System.Data.OpenGauss"))
                {
                    MDbType = DbTypeEnum.GaussDB;
                }
                else
                {
                    throw new Exception("没有在连接字符串中检测到 providerName 无法判断数据库类型!");
                }
                MConnectionString = GetEncryptConnectionString(connectionString);
            }
            catch (Exception e)
            {
                throw new Exception($"数据库初始化失败,请检查数据库连接配置!{Environment.NewLine}{e.Message}");
            }
        }

        /// <summary>
        /// 获取解密后的连接字符串
        /// </summary>
        /// <param name="connectionString">解密前的连接字符串</param>
        /// <returns>解密后的连接字符串</returns>
        private string GetEncryptConnectionString(string connectionString)
        {
            if (connectionString.Contains(";") && connectionString.Contains("="))
            {
                return connectionString;
            }
            var c = AesDecrypt(connectionString, "ORMEncryptKey20050KKKKKKKKKKKKKK");
            return c;
        }

        /// <summary>
        /// 获取数据库类型名称
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetTypeName(DbTypeEnum type)
        {
            switch (type)
            {
                case DbTypeEnum.SqlIte:
                    return "System.Data.SQLite";
                case DbTypeEnum.MySql:
                    return "MySql.Data.MySqlClient";
                case DbTypeEnum.SqlServer:
                    return "System.Data.SqlClient";
                case DbTypeEnum.Oracle:
                    return "System.Data.OracleClient";
                case DbTypeEnum.Dameng:
                    return "System.Data.Dm";
                case DbTypeEnum.Kingbase:
                    return "System.Data.Kdb";
                case DbTypeEnum.Pgsql:
                    return "System.Data.Npgsql";
                case DbTypeEnum.GaussDB:
                    return "System.Data.OpenGauss";
                default:
                    return null;
            }
        }
        #endregion

        #region 变量

        /// <summary>
        /// 数据库类型
        /// </summary>
        public readonly DbTypeEnum MDbType;

        /// <summary>
        /// 数据库sql操作日志(1:开启日志  0:关闭日志)
        /// </summary>
        private static bool _sqlLog;

        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public readonly string MConnectionString;

        /// <summary>
        /// 事务
        /// </summary>
        [ThreadStatic]
        private static DbTransaction _mTran;

        #endregion

        #region 生成变量

        #region 生成 IDbCommand

        /// <summary>
        /// 生成 IDbCommand
        /// </summary>
        public DbCommand GetCommand()
        {
            DbCommand command;
            switch (MDbType)
            {
                case DbTypeEnum.SqlIte:
                    command = new SQLiteCommand();
                    break;
                case DbTypeEnum.MySql:
                    command = new MySqlCommand();
                    break;
                case DbTypeEnum.SqlServer:
                    command = new SqlCommand();
                    break;
                case DbTypeEnum.Oracle:
                    command = new OracleCommand();
                    break;
                case DbTypeEnum.Dameng:
                    command = new DmCommand();
                    break;
                case DbTypeEnum.Kingbase:
                    command = new KdbndpCommand();
                    break;
                case DbTypeEnum.Pgsql:
                    command = new NpgsqlCommand();
                    break;
                case DbTypeEnum.GaussDB:
                    command = new OpenGaussCommand();
                    break;
                default:
                    command = null;
                    break;
            }
            return command;
        }

        /// <summary>
        /// 生成 IDbCommand
        /// </summary>
        public DbCommand GetCommand(string sql, DbConnection conn)
        {
            DbCommand command;
            switch (MDbType)
            {
                case DbTypeEnum.SqlIte:
                    command = new SQLiteCommand(sql);
                    break;
                case DbTypeEnum.MySql:
                    command = new MySqlCommand(sql);
                    break;
                case DbTypeEnum.SqlServer:
                    command = new SqlCommand(sql);
                    break;
                case DbTypeEnum.Oracle:
                    command = new OracleCommand(sql);
                    break;
                case DbTypeEnum.Dameng:
                    command = new DmCommand(sql);
                    break;
                case DbTypeEnum.Kingbase:
                    command = new KdbndpCommand(sql);
                    break;
                case DbTypeEnum.Pgsql:
                    command = new NpgsqlCommand(sql);
                    break;
                case DbTypeEnum.GaussDB:
                    command = new OpenGaussCommand(sql);
                    break;
                default:
                    return null;
            }
            if (command == null)
            {
                return null;
            }

            command.Connection = conn;
            return command;
        }

        #endregion

        #region 生成 IDbConnection

        /// <summary>
        /// 生成 IDbConnection
        /// </summary>
        public DbConnection GetConnection()
        {
            DbConnection conn;
            try
            {
                switch (MDbType)
                {
                    case DbTypeEnum.SqlIte:
                        conn = new SQLiteConnection(MConnectionString);
                        break;
                    case DbTypeEnum.MySql:
                        conn = new MySqlConnection(MConnectionString);
                        break;
                    case DbTypeEnum.SqlServer:
                        conn = new SqlConnection(MConnectionString);
                        break;
                    case DbTypeEnum.Oracle:
                        conn = new OracleConnection(MConnectionString);
                        break;
                    case DbTypeEnum.Dameng:
                        conn = new DmConnection(MConnectionString);
                        break;
                    case DbTypeEnum.Kingbase:
                        conn = new KdbndpConnection(MConnectionString);
                        break;
                    case DbTypeEnum.Pgsql:
                        conn = new NpgsqlConnection(MConnectionString);
                        break;
                    case DbTypeEnum.GaussDB:
                        conn = new OpenGaussConnection(MConnectionString);
                        break;
                    default:
                        conn = null;
                        break;
                }
            }
            catch (Exception e)
            {
                throw new Exception($"生成 IDbConnection异常 请检查数据库连接字符串{Environment.NewLine}{e.Message}");
            }
            return conn;
        }

        #endregion

        #region 生成 IDbDataAdapter

        /// <summary>
        /// 生成 IDbDataAdapter
        /// </summary>
        public DbDataAdapter GetDataAdapter(DbCommand cmd)
        {
            DbDataAdapter dataAdapter;

            switch (MDbType)
            {
                case DbTypeEnum.SqlIte:
                    dataAdapter = new SQLiteDataAdapter();
                    break;
                case DbTypeEnum.MySql:
                    dataAdapter = new MySqlDataAdapter();
                    break;
                case DbTypeEnum.SqlServer:
                    dataAdapter = new SqlDataAdapter();
                    break;
                case DbTypeEnum.Oracle:
                    dataAdapter = new OracleDataAdapter();
                    break;
                case DbTypeEnum.Dameng:
                    dataAdapter = new DmDataAdapter();
                    break;
                case DbTypeEnum.Kingbase:
                    dataAdapter = new KdbndpDataAdapter();
                    break;
                case DbTypeEnum.Pgsql:
                    dataAdapter = new NpgsqlDataAdapter();
                    break;
                case DbTypeEnum.GaussDB:
                    dataAdapter = new OpenGaussDataAdapter();
                    break;
                default:
                    return null;
            }
            if (dataAdapter == null)
            {
                return null;
            }

            dataAdapter.SelectCommand = cmd;
            return dataAdapter;
        }

        #endregion

        #region 生成 DbParameter

        /// <summary>
        /// 生成 DbParameter
        /// </summary>
        /// <param name="name">参数名</param>
        /// <param name="value">参数值</param>
        /// <returns></returns>
        public DbParameter GetDbParameter(string name, object value)
        {
            DbParameter dbParameter;

            switch (MDbType)
            {
                case DbTypeEnum.SqlIte:
                    dbParameter = new SQLiteParameter(name, value);
                    break;
                case DbTypeEnum.MySql:
                    dbParameter = new MySqlParameter(name, value);
                    break;
                case DbTypeEnum.SqlServer:
                    dbParameter = new SqlParameter(name, value);
                    break;
                case DbTypeEnum.Oracle:
                    dbParameter = new OracleParameter(name, value);
                    break;
                case DbTypeEnum.Dameng:
                    dbParameter = new DmParameter(name, value);
                    break;
                case DbTypeEnum.Kingbase:
                    dbParameter = new KdbndpParameter(name, value);
                    break;
                case DbTypeEnum.Pgsql:
                    dbParameter = new NpgsqlParameter(name, value);
                    break;
                case DbTypeEnum.GaussDB:
                    dbParameter = new OpenGaussParameter(name, value);
                    break;
                default:
                    dbParameter = null;
                    break;
            }
            return dbParameter;
        }

        #endregion

        #region 获取 ParameterMark

        /// <summary>
        /// 获取 ParameterMark(参数化查询前面的标识符与参数名)
        /// </summary>
        public string GetParameterMark(string parameter = "")
        {
            return MDbType switch
            {
                DbTypeEnum.SqlIte => ":" + parameter,
                DbTypeEnum.MySql => "@" + parameter,
                DbTypeEnum.SqlServer => "@" + parameter,
                DbTypeEnum.Oracle => ":" + parameter,
                DbTypeEnum.Dameng => ":" + parameter,
                DbTypeEnum.Kingbase => ":" + parameter,
                DbTypeEnum.Pgsql => ":" + parameter,
                DbTypeEnum.GaussDB => ":" + parameter,
                _ => ":" + parameter
            };
        }

        #endregion


        #region 获取转义字符

        /// <summary>
        /// 获取 处理后的字段名
        /// </summary>
        public string GetEscapeName(string name)
        {
            switch (MDbType)
            {
                case DbTypeEnum.SqlIte:
                case DbTypeEnum.MySql:
                    return "`" + name + "`";
                case DbTypeEnum.SqlServer:
                    return "[" + name + "]";
                case DbTypeEnum.Oracle:
                case DbTypeEnum.Dameng:
                case DbTypeEnum.Pgsql:
                case DbTypeEnum.Kingbase:
                case DbTypeEnum.GaussDB:
                    return "\"" + name + "\"";
                default:
                    return "\"" + name + "\"";
            }
        }

        #endregion

        #endregion 生成变量

        #region 基础方法

        #region 函数相关

        /// <summary>
        /// 当前函数对象
        /// </summary>
        /// <returns></returns>
        public SqlFunBase Fun
        {
            get
            {
                var sqlFun = MDbType switch
                {
                    DbTypeEnum.MySql => new SqlFunBase(),
                    DbTypeEnum.SqlIte => new SqLiteFunction(),
                    DbTypeEnum.SqlServer => new MsSqlFunction(),
                    DbTypeEnum.Oracle => new OracleFunction(),
                    DbTypeEnum.Dameng => new DmFunction(),
                    DbTypeEnum.Kingbase => new KdbFunction(),
                    DbTypeEnum.Pgsql => new PgsqlFunction(),
                    DbTypeEnum.GaussDB => new GaussDBFunction(),
                    _ => new SqlFunBase()
                };
                return sqlFun;
            }
        }

        #endregion

        #region 执行sql 返回是否成功

        /// <summary>
        /// 执行sql
        /// </summary>
        /// <param name="sqlString">sql文本</param>
        /// <returns>是否成功</returns>
        public bool Exists(string sqlString)
        {
            LogInfo(sqlString);
            using var conn = GetConnection();
            using var cmd = GetCommand(sqlString, conn);
            try
            {
                conn.Open();
                var obj = cmd.ExecuteScalar();
                return !Equals(obj, null) && !Equals(obj, DBNull.Value);
            }
            catch (Exception ex)
            {
                throw new Exception($"执行Sql[{sqlString}]异常{Environment.NewLine}{ex.Message}");
            }
            finally
            {
                cmd.Dispose();
                conn.Close();
            }
        }

        #endregion

        #region 执行一条计算查询结果语句，返回查询结果

        /// <summary>
        /// 执行一条计算查询结果语句(带?的sql执行)
        /// </summary>
        /// <param name="sqlString">SQL语句</param>
        /// <param name="ps">参数</param>
        /// <returns>影响的记录数</returns>
        public object ExecuteScalar(string sqlString, params object[] ps)
        {
            if (ps?.Length > 0)
            {
                GetSqlAndDbParameter(sqlString, ps, out var sql, out var paramArr);
                return ExecuteScalarParam(sql, paramArr);
            }
            return ExecuteScalarParam(sqlString);
        }

        /// <summary>
        /// 执行一条计算查询结果语句
        /// </summary>
        /// <param name="sql">sql对象</param>
        /// <returns>查询结果（object）</returns>
        public object ExecuteScalar(Sql sql)
        {
            GetSqlAndParamArr(sql, out var conditions, out var parameters);
            return ExecuteScalarParam(conditions, parameters);
        }

        /// <summary>
        /// 执行一条计算查询结果语句
        /// </summary>
        /// <param name="sqlString">计算查询结果语句</param>
        /// <param name="cmdParamArr">参数</param>
        /// <returns>查询结果（object）</returns>
        public object ExecuteScalarParam(string sqlString, params DbParameter[] cmdParamArr)
        {
            LogInfo(sqlString, cmdParamArr);
            using var conn = GetConnection();
            using var cmd = GetCommand(sqlString, conn);
            try
            {
                PrepareCommand(cmd, conn, null, sqlString, cmdParamArr);

                var obj = cmd.ExecuteScalar();
                if (Equals(obj, null) || Equals(obj, DBNull.Value))
                {
                    return null;
                }
                else
                {
                    return obj;
                }
            }
            catch (Exception ex)
            {
                LogErr(sqlString, cmdParamArr);
                throw new Exception($"执行一条计算查询结果语句异常{Environment.NewLine}{ex.Message}");
            }
            finally
            {
                cmd.Dispose();
            }
        }

        #endregion

        #region 执行SQL语句，返回影响的记录数

        /// <summary>
        /// 执行SQL语句,返回影响的记录数(带?的sql执行)
        /// </summary>
        /// <param name="sqlString">SQL语句</param>
        /// <param name="ps">参数</param>
        /// <returns>影响的记录数</returns>
        public int ExecuteSql(string sqlString, params object[] ps)
        {
            if (ps?.Length > 0)
            {
                GetSqlAndDbParameter(sqlString, ps, out var sql, out var paramArr);
                return ExecuteSqlParam(sql, paramArr);
            }
            return ExecuteSqlParam(sqlString);
        }

        /// <summary>
        /// 执行SQL语句,返回影响的记录数
        /// </summary>
        /// <param name="sqlString">SQL语句</param>
        /// <param name="cmdParamArr">参数</param>
        /// <returns>影响的记录数</returns>
        public int ExecuteSqlParam(string sqlString, params DbParameter[] cmdParamArr)
        {
            LogInfo(sqlString, cmdParamArr);
            var conn = _mTran == null ? GetConnection() : _mTran.Connection;
            using var cmd = GetCommand();
            try
            {
                PrepareCommand(cmd, conn, _mTran, sqlString, cmdParamArr);
                var rows = cmd.ExecuteNonQuery();
                cmd.Parameters.Clear();
                return rows;
            }
            catch (Exception ex)
            {
                LogErr(sqlString, cmdParamArr);
                throw new Exception($"执行SQL语句,返回影响的记录数{Environment.NewLine}{ex.Message}");
            }
            finally
            {
                cmd.Dispose();
                if (_mTran == null)
                {
                    conn.Close();
                }
            }
        }

        #endregion

        #region 执行查询语句，返回IDataReader

        /// <summary>
        /// 执行查询语句( 注意：调用该方法后，一定要对IDataReader进行Close )
        /// </summary>
        /// <param name="sqlString">查询语句</param>
        /// <param name="cmdParamArr">参数</param>
        /// <returns>IDataReader</returns>
        public IDataReader ExecuteReader(string sqlString, params DbParameter[] cmdParamArr)
        {
            LogInfo(sqlString, cmdParamArr);
            var conn = GetConnection();
            var cmd = GetCommand();
            try
            {
                PrepareCommand(cmd, conn, null, sqlString, cmdParamArr);
                var myReader = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                cmd.Parameters.Clear();
                return myReader;
            }
            catch (Exception ex)
            {
                LogErr(sqlString, cmdParamArr);
                throw new Exception($"执行SQL语句{Environment.NewLine}{ex.Message}");
            }

        }

        #endregion

        #region 执行查询语句，返回DataSet

        /// <summary>
        /// 执行查询语句
        /// </summary>
        /// <param name="sqlString">查询语句</param>
        /// <param name="cmdParamArr">参数</param>
        /// <returns>DataSet</returns>
        public DataSet ExecuteSet(string sqlString, params DbParameter[] cmdParamArr)
        {
            LogInfo(sqlString, cmdParamArr);
            var conn = GetConnection();
            try
            {
                return FillSet(conn, sqlString, cmdParamArr, false);
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        /// 执行查询语句
        /// </summary>
        /// <param name="sqlString">查询语句</param>
        /// <param name="cmdParamArr">参数</param>
        /// <returns>DataSet</returns>
        public DataSet ExecuteSetTry(string sqlString, params DbParameter[] cmdParamArr)
        {
            LogInfo(sqlString, cmdParamArr);
            var conn = GetConnection();
            try
            {
                return FillSet(conn, sqlString, cmdParamArr, true);
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        /// 执行查询并填充 DataSet
        /// 高斯(openGauss)存在驱动未内置映射的类型(如 tinyint 的 int1),
        /// 首次执行若因此失败,则标记为"未知类型按字符串读取"后重试一次
        /// </summary>
        private DataSet FillSet(DbConnection conn, string sqlString, DbParameter[] cmdParamArr, bool tryMode)
        {
            var ds = new DataSet();
            var cmd = GetCommand();
            if (tryMode)
            {
                PrepareCommandTry(cmd, conn, null, sqlString, cmdParamArr);
            }
            else
            {
                PrepareCommand(cmd, conn, null, sqlString, cmdParamArr);
            }
            var da = GetDataAdapter(cmd);
            try
            {
                da.Fill(ds, "ds");
                cmd.Parameters.Clear();
                return ds;
            }
            catch (Exception ex)
            {
                if (!EnableUnknownResultTypesAsText(cmd))
                {
                    LogErr(sqlString, cmdParamArr);
                    throw new Exception($"执行查询语句{Environment.NewLine}{ex.Message}");
                }
                LogInfo($"{sqlString}{Environment.NewLine}{ex.Message}", cmdParamArr);
            }
            finally
            {
                da.Dispose();
                cmd.Dispose();
            }
            //按字符串读取重试
            ds = new DataSet();
            var retryCmd = GetCommand();
            if (tryMode)
            {
                PrepareCommandTry(retryCmd, conn, null, sqlString, cmdParamArr);
            }
            else
            {
                PrepareCommand(retryCmd, conn, null, sqlString, cmdParamArr);
            }
            EnableUnknownResultTypesAsText(retryCmd);
            var retryDa = GetDataAdapter(retryCmd);
            try
            {
                retryDa.Fill(ds, "ds");
                retryCmd.Parameters.Clear();
            }
            catch (Exception ex)
            {
                LogErr(sqlString, cmdParamArr);
                throw new Exception($"执行查询语句{Environment.NewLine}{ex.Message}");
            }
            finally
            {
                retryDa.Dispose();
                retryCmd.Dispose();
            }
            return ds;
        }

        /// <summary>
        /// 将命令标记为"未知结果类型按字符串读取"
        /// 高斯(openGauss)的 int1(tinyint)等类型驱动没有内置映射,直接查询会报
        /// "which is currently unknown to OpenGauss",标记后可按字符串正常返回
        /// 非高斯命令返回 false(表示调用方按原逻辑处理)
        /// </summary>
        /// <param name="command">查询命令</param>
        /// <returns>是否为高斯命令且已标记成功</returns>
        public static bool EnableUnknownResultTypesAsText(DbCommand command)
        {
            if (command is OpenGaussCommand gauss)
            {
                gauss.AllResultTypesAreUnknown = true;
                return true;
            }
            return false;
        }

        #endregion

        #region 准备命令

        /// <summary>
        /// 包装数据库对象
        /// </summary>
        /// <param name="cmd">命令对象</param>
        /// <param name="conn">数据库连接对象</param>
        /// <param name="trans">事物对象</param>
        /// <param name="cmdText">运行的文本</param>
        /// <param name="cmdParamArr">DbCommand参数</param>
        private void PrepareCommand(DbCommand cmd, DbConnection conn, DbTransaction trans, string cmdText, DbParameter[] cmdParamArr)
        {
            try
            {
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }

                cmd.Connection = conn;
                cmd.CommandText = cmdText;
                if (trans != null)
                {
                    cmd.Transaction = trans;
                }

                cmd.CommandType = CommandType.Text;
                if (cmdParamArr == null)
                {
                    return;
                }

                foreach (var param in cmdParamArr)
                {
                    cmd.Parameters.Add(param);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"打开数据库异常,请检查数据库连接配置{Environment.NewLine}{e.Message}");
            }
        }

        /// <summary>
        /// 包装数据库对象
        /// </summary>
        /// <param name="cmd">命令对象</param>
        /// <param name="conn">数据库连接对象</param>
        /// <param name="trans">事物对象</param>
        /// <param name="cmdText">运行的文本</param>
        /// <param name="cmdParamArr">DbCommand参数</param>
        private void PrepareCommandTry(DbCommand cmd, DbConnection conn, DbTransaction trans, string cmdText, DbParameter[] cmdParamArr)
        {
            try
            {
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }

                cmd.Connection = conn;
                cmd.CommandText = cmdText;
                if (trans != null)
                {
                    cmd.Transaction = trans;
                }

                cmd.CommandType = CommandType.Text;
                if (cmdParamArr == null)
                {
                    return;
                }

                foreach (var param in cmdParamArr)
                {
                    cmd.Parameters.Add(param);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"打开数据库异常,请检查数据库连接配置{Environment.NewLine}{e.Message}");
            }
        }

        /// <summary>
        /// 从sql中 获取sqlStr与DbParameter[]
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="sqlStr"></param>
        /// <param name="paramArr"></param>
        private void GetSqlAndParamArr(Sql sql, out string sqlStr, out DbParameter[] paramArr)
        {
            sqlStr = sql.ToString();
            paramArr = new DbParameter[sql.ParamDict.Count];
            var i = 0;
            foreach (var p in sql.ParamDict)
            {
                //这里参数化为了匹配不同的数据库 在这里补充下参数化的参数前缀
                var paramKey = GetParameterMark(p.Key);
                sqlStr = sqlStr.Replace(p.Key, paramKey);
                paramArr[i] = GetDbParameter(p.Key, p.Value ?? DBNull.Value);
                i++;
            }
        }

        #endregion

        #endregion 基础方法

        #region 增删改查

        #region 添加

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="obj">要添加的实体对象</param>
        /// <returns>返回影响的行数 成功为1 失败为0</returns>
        public bool Insert(object obj)
        {
            var strSql = new StringBuilder();
            var type = obj.GetType();
            strSql.AppendFormat("insert into {0}(", type.Name);
            var propertyInfoList = GetEntityProperties(type);
            var propertyNameList = new List<string>();
            var savedCount = 0;
            foreach (var propertyInfo in propertyInfoList)
            {
                var val = propertyInfo.GetValue(obj, null);
                if (val == null)
                {
                    continue;
                }
                propertyNameList.Add(propertyInfo.Name);
                savedCount++;
            }
            var index = 0;
            strSql.AppendFormat("{0}) values ({1})",
                string.Join(",", propertyNameList.ConvertAll(a => a).ToArray()),
                string.Join(",", propertyNameList.ConvertAll(a => GetParameterMark(a + index++)).ToArray()));
            var parameters = new DbParameter[savedCount];
            var k = 0;
            for (var i = 0; i < propertyInfoList.Length && savedCount > 0; i++)
            {
                var propertyInfo = propertyInfoList[i];
                var val = propertyInfo.GetValue(obj, null);
                if (val == null)
                {
                    continue;
                }

                var param = GetDbParameter(propertyInfo.Name + k, val);
                parameters[k++] = param;
            }
            return ExecuteSqlParam(strSql.ToString(), parameters) > 0;
        }

        /// <summary>
        /// 批量添加
        /// </summary>
        /// <param name="objList">要批量添加的实体集合</param>
        /// <returns>返回是否成功</returns>
        public bool Inserts<T>(List<T> objList) where T : new()
        {
            try
            {
                BeginTransaction();
                foreach (var obj in objList)
                {
                    InsertOrUpdate(obj);
                }
                CommitTransaction();
                return true;
            }
            catch (Exception e)
            {
                DbLogError("批量添加发生异常:" + e.Message);
                RollbackTransaction();
                return false;
            }
        }

        /// <summary>
        /// 批量添加(不带事物)
        /// </summary>
        /// <param name="objList">要批量添加的实体集合</param>
        /// <returns>返回是否成功</returns>
        public bool InsertsWithoutTran<T>(List<T> objList) where T : new()
        {
            try
            {
                foreach (var obj in objList)
                {
                    Insert(obj);
                }
                return true;
            }
            catch (Exception e)
            {
                DbLogError("批量添加发生异常:" + e.Message);
                throw;
            }
        }

        /// <summary>
        /// 添加或修改
        /// </summary>
        /// <param name="obj">要添加的实体对象</param>
        /// <param name="isCover">是否覆盖</param>
        /// <returns>返回是否执行成功</returns>
        public bool InsertOrUpdate(object obj, bool isCover = true)
        {
            var type = obj.GetType();
            var sql = new Sql();
            sql.Select().Add("1").From().Add(type.Name).Where().AddBs("1=1");
            var propertyInfoList = GetEntityProperties(type);
            foreach (var propertyInfo in propertyInfoList)
            {
                var attrs = (EdmScalarPropertyAttribute[])propertyInfo.GetCustomAttributes(typeof(EdmScalarPropertyAttribute), true);
                if (attrs.Length < 1 || !attrs[0].EntityKeyProperty)
                {
                    continue;
                }
                var val = propertyInfo.GetValue(obj, null);
                sql.And(propertyInfo.Name).Equal(val);
            }
            GetSqlAndParamArr(sql, out var conditions, out var parameters);
            var rd = ExecuteScalarParam(conditions, parameters);
            return rd == null ? Insert(obj) : !isCover || Update(obj);
        }

        #endregion

        #region 删除

        /// <summary>
        /// 根据条件删除
        /// </summary>
        /// <typeparam name="T">要删除的实体类型</typeparam>
        /// <param name="conditions">删除条件(从where后面开始组装)</param>
        /// <returns></returns>
        public bool Delete<T>(string conditions)
        {
            var st = new Sql();
            st.Add(conditions);
            var ret = Delete<T>(st);
            st.Clear();
            return ret;
        }

        /// <summary>
        /// 根据条件删除
        /// </summary>
        /// <typeparam name="T">要删除的实体类型</typeparam>
        /// <param name="sql">sql对象(从where后面开始组装)</param>
        /// <returns></returns>
        public bool Delete<T>(Sql sql)
        {
            GetSqlAndParamArr(sql, out var conditions, out var parameters);
            if (string.IsNullOrEmpty(conditions))
            {
                return false;
            }

            var type = typeof(T);
            var sbSql = new StringBuilder();
            sbSql.AppendFormat("delete from {0} where {1}", type.Name, conditions);
            return ExecuteSqlParam(sbSql.ToString(), parameters) > 0;
        }

        /// <summary>
        /// 根据条件删除
        /// </summary>
        /// <typeparam name="T">要删除的实体类型</typeparam>
        /// <param name="lambdaWhere">拉姆达表达式</param>
        /// <returns></returns>
        public bool Delete<T>(Expression<Func<T, bool>> lambdaWhere)
        {
            var sql = new Sql().ToSql(lambdaWhere);
            return Delete<T>(sql);
        }

        /// <summary>
        /// 删除表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool Delete<T>()
        {
            try
            {
                ExecuteSqlParam($"delete from {typeof(T).Name} ");
                return true;
            }
            catch (Exception ex)
            {
                DbLogError("删除表数据发生异常:" + ex.Message);
                throw;
            }

        }

        #endregion

        #region 修改

        /// <summary>
        /// 根据实体修改(适用于有主键的实体)
        /// </summary>
        /// <param name="obj">要修改的实体</param>
        /// <returns></returns>
        public bool Update(object obj)
        {
            var type = obj.GetType();
            var sql = new Sql();
            var propertyInfoList = GetEntityProperties(type);
            foreach (var propertyInfo in propertyInfoList)
            {
                var attrs = (EdmScalarPropertyAttribute[])propertyInfo.GetCustomAttributes(typeof(EdmScalarPropertyAttribute), true);
                //这里拼接where 排除非主键字段
                if (attrs.Length < 1 || !attrs[0].EntityKeyProperty)
                {
                    continue;
                }

                var val = propertyInfo.GetValue(obj, null);
                sql.And(propertyInfo.Name).Equal(val);
            }
            return Update(obj, sql);
        }

        /// <summary>
        /// 根据条件修改
        /// </summary>
        /// <param name="obj">要修改的实体</param>
        /// <param name="sql">sql对象(从where后面开始组装)</param>
        /// <returns></returns>
        public bool Update(object obj, Sql sql)
        {
            var strSql = new StringBuilder();
            var type = obj.GetType();
            strSql.AppendFormat("update {0} ", type.Name);
            //获取要修改的字段数
            var propertyInfoList = GetEntityProperties(type);
            var savedCount = (from propertyInfo in propertyInfoList
                              let val = propertyInfo.GetValue(obj, null)
                              let attrs =
                                  (EdmScalarPropertyAttribute[])propertyInfo.GetCustomAttributes(typeof(EdmScalarPropertyAttribute),
                                      true)
                              where attrs.Length <= 0 || !attrs[0].EntityKeyProperty
                              select val).Count();
            //这里定义参数化数组 长度加上where中的参数
            var parameters = new DbParameter[savedCount + sql.ParamDict.Count];
            //开始拼接写入字段
            strSql.Append(" set ");
            //SQL参数下标
            var k = 0;
            //写入字段拼接字符串
            var sbPros = new StringBuilder();
            var index = 0;
            for (var i = 0; i < propertyInfoList.Length && savedCount > 0; i++)
            {
                var propertyInfo = propertyInfoList[i];
                var val = propertyInfo.GetValue(obj, null);
                var attrs = (EdmScalarPropertyAttribute[])propertyInfo.GetCustomAttributes(typeof(EdmScalarPropertyAttribute), true);
                //判断是主键不赋值
                if (attrs.Length > 0 && attrs[0].EntityKeyProperty)
                {
                    continue;
                }
               
                sbPros.Append(string.Format(" {0}={1}{0}{2}Param,", propertyInfo.Name, GetParameterMark(), index));
                var param = GetDbParameter(propertyInfo.Name + index + "Param", val ?? DBNull.Value);
                parameters[k++] = param;
                index++;
            }
            //写入字段存在 去掉最后的逗号加到sql中
            if (sbPros.Length > 0)
            {
                strSql.Append(sbPros.ToString(0, sbPros.Length - 1));
            }
            var conditions = sql.ToString();
            //添加where中的参数
            foreach (var p in sql.ParamDict)
            {
                //这里参数化为了匹配不同的数据库 在这里补充下参数化的参数前缀
                var paramKey = GetParameterMark(p.Key);
                conditions = conditions.Replace(p.Key, paramKey);
                parameters[k++] = GetDbParameter(p.Key, p.Value);
            }
            //拼接上where语句
            strSql.AppendFormat(" where {0}", conditions);
            //执行
            return savedCount > 0 && ExecuteSqlParam(strSql.ToString(), parameters) > 0;
        }

        /// <summary>
        /// 根据条件修改
        /// </summary>
        /// <param name="t">要修改的实体</param>
        /// <param name="lambdaWhere">拉姆达表达式</param>
        /// <returns></returns>
        public bool Update<T>(T t, Expression<Func<T, bool>> lambdaWhere)
        {
            var sql = new Sql().ToSql(lambdaWhere);
            return Update(t, sql);
        }

        #endregion

        #region 查询

        /// <summary>
        /// 根据条件查询单条数据
        /// </summary>
        /// <param name="conditions">查询条件(从where后面开始组装)</param>
        /// <returns></returns>
        public T Select<T>(string conditions = "") where T : new()
        {
            var st = new Sql();
            st.Add(conditions);
            var ret = Select<T>(st);
            st.Clear();
            return ret;
        }

        /// <summary>
        /// 根据条件查询单条数据
        /// </summary>
        /// <param name="lambdaWhere">拉姆达表达式</param>
        /// <returns></returns>
        public T Select<T>(Expression<Func<T, bool>> lambdaWhere) where T : new()
        {
            var sql = new Sql().ToSql(lambdaWhere);
            return Select<T>(sql);
        }

        /// <summary>
        /// 根据条件查询单条数据
        /// </summary>
        /// <param name="sql">sql对象(从where后面开始组装)</param>
        /// <returns></returns>
        public T Select<T>(Sql sql) where T : new()
        {
            GetSqlAndParamArr(sql, out var conditions, out var parameters);
            var type = typeof(T);
            var sbSql = new StringBuilder();
            var where = conditions == "" ? "" : " where " + conditions;
            sbSql.AppendFormat("select * from {0} {1}", type.Name, where);
            // ReSharper disable once CoVariantArrayConversion
            return FindBySql<T>(sbSql.ToString(), parameters);
        }

        /// <summary>
        /// 根据条件查询列表
        /// </summary>
        /// <param name="conditions">修改的条件(从where后面开始组装)</param>
        /// <returns></returns>
        public List<T> SelectAll<T>(string conditions = "") where T : new()
        {
            var st = new Sql();
            st.Add(conditions);
            var ret = SelectAll<T>(st);
            st.Clear();
            return ret;
        }

        /// <summary>
        /// 根据条件查询列表
        /// </summary>
        /// <param name="lambdaWhere">拉姆达表达式</param>
        /// <returns></returns>
        public List<T> SelectAll<T>(Expression<Func<T, bool>> lambdaWhere) where T : new()
        {
            var sql = new Sql().ToSql(lambdaWhere);
            return SelectAll<T>(sql);
        }

        /// <summary>
        /// 根据条件查询列表
        /// </summary>
        /// <param name="sql">sql对象(从where后面开始组装)</param>
        /// <returns></returns>
        public List<T> SelectAll<T>(Sql sql) where T : new()
        {
            GetSqlAndParamArr(sql, out var conditions, out var parameters);
            var type = typeof(T);
            var sbSql = new StringBuilder();
            var where = conditions == "" ? "" : " where " + conditions;
            sbSql.AppendFormat("select * from {0} {1}", type.Name, where);
            // ReSharper disable once CoVariantArrayConversion
            return FindListBySql<T>(sbSql.ToString(), parameters);
        }

        /// <summary>
        /// 查询直接返回DataSet
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public DataSet DataSetQuery(Sql sql)
        {
            GetSqlAndParamArr(sql, out var conditions, out var parameters);
            return ExecuteSet(conditions, parameters);
        }

        #endregion

        #region 带?的sql和object可变参数分析出真正的sql和DbParameter

        /// <summary>
        /// 带?的sql和object可变参数分析出真正的sql和DbParameter
        /// </summary>
        /// <param name="sqlString"></param>
        /// <param name="objList"></param>
        /// <param name="sql"></param>
        /// <param name="dbParameters"></param>
        private void GetSqlAndDbParameter(string sqlString, IList<object> objList, out string sql, out DbParameter[] dbParameters)
        {
            sql = sqlString;
            dbParameters = null;
            if (objList?.Count > 0)
            {
                if (objList.GetType() == typeof(DbParameter[]))
                {
                    dbParameters = objList as DbParameter[];
                }
                else
                {
                    dbParameters = new DbParameter[objList.Count];
                    for (var i = 0; i < objList.Count; i++)
                    {
                        //参数化的参数名
                        var key = "ORMSqlParameter" + i;
                        //这里参数化为了匹配不同的数据库 在这里补充下参数化的参数前缀
                        var paramKey = GetParameterMark(key);
                        //找到第一个问号位置
                        var index = sql.IndexOf('?');
                        //这里找不到?了 代表参数写多了 忽略后面参数
                        if (index == -1)
                        {
                            continue;
                        }
                        //删除这个问号
                        sql = sql.Remove(index, 1);
                        //在原问号位置插入参数字符串
                        sql = sql.Insert(index, paramKey);
                        dbParameters[i] = GetDbParameter(key, objList[i]);
                    }
                }
            }
        }

        #endregion

        #region 获取实体

        /// <summary>
        /// 根据sqlString获取实体(带?的sql执行)
        /// </summary>
        public T FindBySql<T>(string sqlString, params object[] ps) where T : new()
        {
            if (ps?.Length > 0)
            {
                GetSqlAndDbParameter(sqlString, ps, out var sql, out var paramArr);
                return FindBySqlParameter<T>(sql, paramArr);
            }
            return FindBySqlParameter<T>(sqlString);
        }

        /// <summary>
        /// 根据sqlString获取实体
        /// </summary>
        public T FindBySqlParameter<T>(string sqlString, params DbParameter[] cmdParamArr) where T : new()
        {
            var type = typeof(T);
            var result = (T)Activator.CreateInstance(type);
            IDataReader rd = null;
            try
            {
                rd = ExecuteReader(sqlString, cmdParamArr);
                if (rd == null)
                {
                    return result;
                }
                var fieldCount = rd.FieldCount;
                var filedArr = new List<string>();
                for (var i = 0; i < fieldCount; i++)
                {
                    filedArr.Add(rd.GetName(i).ToUpper());
                }
                var propertyInfoList = GetEntityProperties(type);
                if (rd.Read())
                {
                    IDataRecord record = rd;
                    foreach (var pro in propertyInfoList)
                    {
                        try
                        {
                            switch (pro.PropertyType.Namespace)
                            {
                                case "System":
                                    if (!filedArr.Contains(pro.Name.ToUpper()) || record[pro.Name] == DBNull.Value)
                                    {
                                        continue;
                                    }
                                    pro.SetValue(result, GetReaderValue(record[pro.Name], pro.PropertyType), null);
                                    break;
                                default:
                                    var proInfoList = GetEntityProperties(pro.PropertyType);
                                    var tmp = Activator.CreateInstance(pro.PropertyType);
                                    foreach (var p in proInfoList)
                                    {
                                        var dataName = $"{pro.PropertyType.Name}_{p.Name}";
                                        if (!filedArr.Contains(dataName) || record[dataName] == DBNull.Value)
                                        {
                                            continue;
                                        }
                                        var v = GetReaderValue(record[dataName], p.PropertyType);
                                        p.SetValue(tmp, v, null);
                                    }
                                    pro.SetValue(result, tmp, null);
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            DbLogError($"字段:{pro.Name} 异常=>{e.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DbLogError(ex.Message);
            }
            finally
            {
                if (rd is { IsClosed: false })
                {
                    rd.Close();
                    rd.Dispose();
                }
            }
            return result;
        }

        /// <summary>
        /// 根据sql对象获取实体
        /// </summary>
        /// <typeparam name="T">要查询的实体类型</typeparam>
        /// <param name="sql">sql对象(全SQL)</param>
        /// <returns></returns>
        public T FindBySql<T>(Sql sql) where T : new()
        {
            GetSqlAndParamArr(sql, out var conditions, out var parameters);
            return FindBySqlParameter<T>(conditions, parameters);
        }

        #endregion

        #region 获取实体集合

        /// <summary>
        /// 根据sqlString获取集合(带?的sql执行)
        /// </summary>
        public List<T> FindListBySql<T>(string sqlString, params object[] ps) where T : new()
        {
            if (ps?.Length > 0)
            {
                GetSqlAndDbParameter(sqlString, ps, out var sql, out var paramArr);
                return FindListBySqlParameter<T>(sql, paramArr);
            }
            return FindListBySqlParameter<T>(sqlString);
        }

        /// <summary>
        /// 根据sqlString获取集合
        /// </summary>
        /// <typeparam name="T">要查询的列表实体类型</typeparam>
        /// <param name="sqlString">SQL语句(全SQL)</param>
        /// <param name="cmdParamArr">参数</param>
        /// <returns></returns>
        public List<T> FindListBySqlParameter<T>(string sqlString, params DbParameter[] cmdParamArr) where T : new()
        {
            var list = new List<T>();
            IDataReader rd = null;
            try
            {
                rd = ExecuteReader(sqlString, cmdParamArr);
                if (rd == null)
                {
                    return list;
                }
                if (typeof(T) == typeof(int))
                {
                    while (rd.Read())
                    {
                        list.Add((T)rd[0]);
                    }
                }
                else if (typeof(T) == typeof(string))
                {
                    while (rd.Read())
                    {
                        list.Add((T)rd[0]);
                    }
                }
                else
                {
                    var propertyInfoList = typeof(T).GetProperties();
                    var fieldCount = rd.FieldCount;
                    var filedArr = new List<string>();
                    for (var i = 0; i < fieldCount; i++)
                    {
                        filedArr.Add(rd.GetName(i).ToUpper());
                    }
                    while (rd.Read())
                    {
                        IDataRecord record = rd;
                        object obj = new T();
                        foreach (var pro in propertyInfoList)
                        {
                            switch (pro.PropertyType.Namespace)
                            {
                                case nameof(System):
                                    if (!filedArr.Contains(pro.Name.ToUpper()) || record[pro.Name] == DBNull.Value)
                                    {
                                        continue;
                                    }
                                    pro.SetValue(obj, GetReaderValue(record[pro.Name.ToUpper()], pro.PropertyType), null);
                                    break;
                                default:
                                    var proInfoList = GetEntityProperties(pro.PropertyType);
                                    var tmp = Activator.CreateInstance(pro.PropertyType);
                                    foreach (var p in proInfoList)
                                    {
                                        var dataName = $"{pro.PropertyType.Name}_{p.Name}";
                                        if (!filedArr.Contains(dataName.ToUpper()) || record[dataName] == DBNull.Value)
                                        {
                                            continue;
                                        }
                                        var v = GetReaderValue(record[dataName], p.PropertyType);
                                        p.SetValue(tmp, v, null);
                                    }
                                    pro.SetValue(obj, tmp, null);
                                    break;
                            }
                        }
                        list.Add((T)obj);
                    }
                }
            }
            catch (Exception ex)
            {
                DbLogError(ex.Message);
            }
            finally
            {
                if (rd is { IsClosed: false })
                {
                    rd.Close();
                    rd.Dispose();
                }
            }
            return list;
        }

        /// <summary>
        /// 根据sql对象获取集合
        /// </summary>
        /// <typeparam name="T">要查询的实体类型</typeparam>
        /// <param name="sql">sql对象(全SQL)</param>
        /// <returns></returns>
        public List<T> FindListBySql<T>(Sql sql) where T : new()
        {
            GetSqlAndParamArr(sql, out var conditions, out var parameters);
            return FindListBySqlParameter<T>(conditions, parameters);
        }

        #endregion

        #endregion 增删改查

        #region 分页查询

        #region 分页获取列表

        /// <summary>
        /// 分页(任意entity，尽量少的字段)
        /// </summary>
        /// <param name="sql">sql对象(全SQL)</param>
        /// <param name="pageSize">每页行数</param>
        /// <param name="currentPage">当前页数</param>
        /// <param name="rows">返回的总行数</param>
        /// <param name="orderBy">排序</param>
        /// <returns>返回查到的实体集合</returns>
        public List<T> FindPageBySql<T>(Sql sql, int pageSize, int currentPage, out int rows, string orderBy = null) where T : new()
        {
            GetSqlAndParamArr(sql, out var conditions, out var parameters);
            rows = QueryCount(conditions, parameters);
            // ReSharper disable once CoVariantArrayConversion
            return FindListBySql<T>(GetPageSql(conditions, pageSize, currentPage, orderBy), parameters);
        }

        /// <summary>
        /// 查询总行数(使用带数据库参数前缀的SQL,并对参数重新生成副本,避免与后续查询共用同一批参数对象)
        /// </summary>
        /// <param name="sql">已包含数据库参数前缀的SQL</param>
        /// <param name="parameters">参数集合</param>
        /// <returns>总行数</returns>
        private int QueryCount(string sql, DbParameter[] parameters)
        {
            using var connection = GetConnection();
            var commandText = "select count(*) from (" + sql + ") T";
            using var cmd = GetCommand(commandText, connection);
            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    if (p == null)
                    {
                        continue;
                    }
                    cmd.Parameters.Add(GetDbParameter(p.ParameterName, p.Value ?? DBNull.Value));
                }
            }
            connection.Open();
            var obj = cmd.ExecuteScalar();
            return obj == null || obj == DBNull.Value ? 0 : Convert.ToInt32(obj);
        }

        #endregion

        #region 分页获取DataSet

        /// <summary>
        /// 分页(任意entity，尽量少的字段)
        /// </summary>
        /// <param name="sql">sql对象(全SQL)</param>
        /// <param name="pageSize">每页行数</param>
        /// <param name="currentPage">当前页数</param>
        /// <param name="rows">返回的总行数</param>
        /// <param name="orderBy">排序</param>
        /// <returns>返回查到的DataSet</returns>
        public DataSet FindPageBySql(Sql sql, int pageSize, int currentPage, out int rows, string orderBy = null)
        {
            GetSqlAndParamArr(sql, out var conditions, out var parameters);
            rows = QueryCount(conditions, parameters);
            var ds = ExecuteSet(GetPageSql(conditions, pageSize, currentPage, orderBy), parameters);
            return ds;
        }

        #endregion

        #region 分页语句拼接
        private string GetPageSql(string sql, int pageSize, int currentPage, string orderBy = null)
        {
            var sb = new StringBuilder();
            int startRow;
            int endRow;
            switch (MDbType)
            {
                case DbTypeEnum.Oracle:

                    #region 分页查询语句

                    startRow = pageSize * (currentPage - 1);
                    endRow = startRow + pageSize;

                    sb.Append("select * from ( select row_limit.*, rownum rownum_ from (");
                    sb.Append(sql);
                    if (!string.IsNullOrEmpty(orderBy))
                    {
                        sb.Append(" ");
                        sb.Append(orderBy);
                    }
                    sb.Append(" ) row_limit where rownum <= ");
                    sb.Append(endRow);
                    sb.Append(" ) where rownum_ >");
                    sb.Append(startRow);

                    #endregion

                    break;

                case DbTypeEnum.Dameng:

                    #region 分页查询语句

                    startRow = pageSize * (currentPage - 1);
                    endRow = startRow + pageSize;

                    sb.Append("select * from ( select row_limit.*, rownum rownum_ from (");
                    sb.Append(sql);
                    if (!string.IsNullOrEmpty(orderBy))
                    {
                        sb.Append(" ");
                        sb.Append(orderBy);
                    }
                    sb.Append(" ) row_limit where rownum <= ");
                    sb.Append(endRow);
                    sb.Append(" ) where rownum_ >");
                    sb.Append(startRow);

                    #endregion

                    break;
                case DbTypeEnum.SqlServer:

                    #region 分页查询语句

                    startRow = pageSize * (currentPage - 1) + 1;
                    endRow = startRow + pageSize - 1;

                    //ROW_NUMBER 必须带 ORDER BY,未指定排序时给一个常量排序,避免生成非法SQL
                    var msOrderBy = string.IsNullOrEmpty(orderBy) ? "(ORDER BY (SELECT 1))" : orderBy;
                    sb.AppendFormat(@"
                            select * from
                            (select ROW_NUMBER() over({1}) as rowNumber, t.* from ({0}) t) tempTable
                            where rowNumber between {2} and {3} ", sql, msOrderBy, startRow, endRow);

                    #endregion

                    break;
                case DbTypeEnum.MySql:

                    #region 分页查询语句

                    startRow = pageSize * (currentPage - 1);

                    sb.Append(sql);
                    if (!string.IsNullOrEmpty(orderBy))
                    {
                        sb.Append(" ");
                        sb.Append(orderBy);
                    }
                    sb.AppendFormat(" limit {0},{1}", startRow, pageSize);

                    #endregion

                    break;
                case DbTypeEnum.SqlIte:
                    #region 分页查询语句

                    startRow = pageSize * (currentPage - 1);

                    sb.Append(sql);
                    if (!string.IsNullOrEmpty(orderBy))
                    {
                        sb.Append(" ");
                        sb.Append(orderBy);
                    }
                    sb.AppendFormat(" limit {0} offset {1}", pageSize, startRow);

                    #endregion
                    break;
                default:
                    #region 分页查询语句

                    startRow = pageSize * (currentPage - 1);

                    sb.Append(sql);
                    if (!string.IsNullOrEmpty(orderBy))
                    {
                        sb.Append(" ");
                        sb.Append(orderBy);
                    }
                    sb.AppendFormat(" limit {0} offset {1}", pageSize, startRow);

                    #endregion
                    break;
            }
            return sb.ToString();
        }

        #endregion

        #region getReaderValue 转换数据

        /// <summary>
        /// 转换数据
        /// </summary>
        private static object GetReaderValue(object rdValue, Type pType)
        {
            if (rdValue == DBNull.Value) return null;
            if (rdValue.GetType() == pType) return rdValue;
            if (pType == typeof(string)) return rdValue.ToString();
            if (pType == typeof(double) || pType == typeof(double?)) return Convert.ToDouble(rdValue);
            if (pType == typeof(decimal) || pType == typeof(decimal?)) return Convert.ToDecimal(rdValue);
            if (pType == typeof(int) || pType == typeof(int?)) return Convert.ToInt32(rdValue);
            if (pType == typeof(long) || pType == typeof(long?)) return Convert.ToInt64(rdValue);
            if (pType == typeof(DateTime) || pType == typeof(DateTime?)) return Convert.ToDateTime(rdValue);
            return rdValue;
        }

        #endregion

        #region 获取实体类属性

        /// <summary>
        /// 获取实体类属性
        /// </summary>
        private static PropertyInfo[] GetEntityProperties(Type type)
        {
            var propertyInfoList = type.GetProperties();
            return propertyInfoList.Where(propertyInfo => propertyInfo.GetCustomAttributes(typeof(EdmRelationshipNavigationPropertyAttribute), false).Length == 0 && propertyInfo.GetCustomAttributes(typeof(BrowsableAttribute), false).Length == 0).ToArray();
        }

        #endregion

        #region 获取基类

        /// <summary>
        /// 获取基类
        /// </summary>
        public static Type GetBaseType(Type type)
        {
            while (type.BaseType != null && type.BaseType.Name != nameof(Object))
            {
                type = type.BaseType;
            }
            return type;
        }

        #endregion

        #endregion 分页

        #region 事务

        #region 开始事务

        /// <summary>
        /// 开始事务
        /// </summary>
        public void BeginTransaction()
        {
            if (_sqlLog)
            {
                DbLogInfo("开始事务");
            }

            var conn = GetConnection();
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }

            _mTran = conn.BeginTransaction();
        }

        #endregion

        #region 提交事务

        /// <summary>
        /// 提交事务
        /// </summary>
        public void CommitTransaction()
        {
            //防止重复提交
            if (_mTran == null)
            {
                return;
            }

            if (_sqlLog)
            {
                DbLogInfo("提交事务");
            }

            var conn = _mTran.Connection;
            try
            {
                _mTran.Commit();
            }
            catch (Exception ex)
            {
                _mTran.Rollback();
                DbLogError("提交事务:" + ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }

                _mTran.Dispose();
                _mTran = null;
            }
        }

        #endregion

        #region 回滚事务(出错时调用该方法回滚)

        /// <summary>
        /// 回滚事务(出错时调用该方法回滚)
        /// </summary>
        public void RollbackTransaction()
        {
            //防止重复回滚
            if (_mTran == null)
            {
                return;
            }

            if (_sqlLog)
            {
                DbLogInfo("回滚事务");
            }

            var conn = _mTran.Connection;
            try
            {
                _mTran.Rollback();
            }
            catch (Exception ex)
            {
                DbLogError("回滚事务:" + ex.Message);
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
                _mTran.Dispose();
                _mTran = null;
            }
        }

        #endregion

        #endregion

        #region SQL日志

        /// <summary>
        /// 获取具体执行的SQL
        /// </summary>
        /// <returns></returns>
        private string GetSql(string sqlString, params DbParameter[] cmdParamArr)
        {
            var sqlStr = sqlString;
            foreach (var p in cmdParamArr)
            {
                var key = GetParameterMark(p.ParameterName);
                if (p.Value == null)
                {
                    sqlStr = sqlStr.Replace(key, "null");
                    continue;
                }
                switch (p.DbType)
                {
                    case DbType.AnsiString:

                    case DbType.Binary:

                    case DbType.Date:

                    case DbType.DateTime:

                    case DbType.Guid:

                    case DbType.Object:

                    case DbType.String:

                    case DbType.Time:

                    case DbType.AnsiStringFixedLength:

                    case DbType.StringFixedLength:

                    case DbType.Xml:

                    case DbType.DateTime2:

                    case DbType.DateTimeOffset:
                        sqlStr = sqlStr.Replace(key, "'" + p.Value + "'");
                        break;
                    default:
                        sqlStr = sqlStr.Replace(key, p.Value?.ToString());
                        break;
                }
            }
            return sqlStr;
        }

        /// <summary>
        /// SQL记录日志
        /// </summary>
        private void LogInfo(string sqlString, params DbParameter[] cmdParamArr)
        {
            if (!_sqlLog)
            {
                return;
            }
            if (Debugger.IsAttached)
            {
                Debug.WriteLine("-- --------------正常查询-------------");
                Debug.WriteLine(GetSql(sqlString, cmdParamArr));
                Debug.WriteLine("-- --------------查询结束-------------");
            }
            DbLogInfo("原查询:" + sqlString + string.Concat(cmdParamArr.Select(m => " " + m.ParameterName + ":" + m.Value)));
            DbLogInfo("调试查询:" + GetSql(sqlString, cmdParamArr));
        }

        /// <summary>
        /// SQL错误日志
        /// </summary>
        private void LogErr(string sqlString, params DbParameter[] cmdParamArr)
        {
            if (Debugger.IsAttached)
            {
                Debug.WriteLine("-- --------------异常查询-------------");
                Debug.WriteLine(GetSql(sqlString, cmdParamArr));
                Debug.WriteLine("-- --------------查询结束-------------");
            }
            DbLogError("原查询:" + sqlString + string.Concat(cmdParamArr.Select(m => " " + m.ParameterName + ":" + m.Value)));
            DbLogError("调试查询:" + GetSql(sqlString, cmdParamArr));
        }

        /// <summary>
        /// 数据库消息日志
        /// </summary>
        /// <param name="o"></param>
        public static void DbLogInfo(object o)
        {
            LogInfo(o, "DB_");
        }

        /// <summary>
        /// 数据库错误日志
        /// </summary>
        /// <param name="o"></param>
        public static void DbLogError(object o)
        {
            LogError(o, "DB_");
        }

        #endregion

        #region 日志

        /// <summary>
        /// 日志文件夹默认根目录logs文件夹
        /// </summary>
        public static string LogFolderPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        /// <summary>
        /// 日志分隔文件大小 10M
        /// </summary>
        private const int FileSize = 10 * 1024 * 1024;

        /// <summary>
        /// 写错误日志
        /// </summary>
        public static void LogError(object log)
        {
            LogError(log, string.Empty);
        }

        /// <summary>
        /// 写操作日志
        /// </summary>
        public static void LogInfo(object log)
        {
            LogInfo(log, string.Empty);
        }

        /// <summary>
        /// 写错误日志
        /// </summary>
        public static void LogError(object log, string prefix)
        {
            WriteFile("[Error] " + log + Environment.NewLine, CreateLogPath(prefix));
        }

        /// <summary>
        /// 写操作日志
        /// </summary>
        public static void LogInfo(object log, string prefix)
        {
            WriteFile("[Info] " + log + Environment.NewLine, CreateLogPath(prefix));
        }

        /// <summary>
        /// 生成日志文件路径
        /// </summary>
        /// <param name="prefix">前缀</param>
        /// <returns></returns>
        private static string CreateLogPath(string prefix)
        {
            var index = 0;
            string logPath;
            var bl = true;
            do
            {
                index++;
                logPath = Path.Combine(LogFolderPath, prefix + DateTime.Now.ToString("yyyyMMdd") + "_" + index + ".log");
                if (File.Exists(logPath))
                {
                    var fileInfo = new FileInfo(logPath);
                    if (fileInfo.Length < FileSize)
                    {
                        bl = false;
                    }
                }
                else
                {
                    bl = false;
                }
            } while (bl);
            return logPath;
        }

        /// <summary>
        /// 写文件
        /// </summary>
        private static void WriteFile(string txt, string logPath)
        {
            MutexExec(LogFolderPath, () =>
            {
                if (!Directory.Exists(LogFolderPath))
                {
                    Directory.CreateDirectory(LogFolderPath);
                }
                var value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + txt + Environment.NewLine;
                File.AppendAllText(logPath, value);
            });
        }

        #region 进程与线程保证同步

        /// <summary>
        /// 多线程与多进程间同步操作文件
        /// </summary>
        /// <param name="filePath">文件路径
        /// (如果将 name 指定为 null 或空字符串，则创建一个局部互斥体。 
        /// 如果名称以前缀“Global\”开头，则 mutex 在所有终端服务器会话中均为可见。 
        /// 如果名称以前缀“Local\”开头，则 mutex 仅在创建它的终端服务器会话中可见。 
        /// 如果创建已命名 mutex 时不指定前缀，则它将采用前缀“Local\”。)</param>
        /// <param name="action">同步处理操作</param>
        /// <param name="recursive">指示当前调用是否为递归处理，递归处理时检测到异常则抛出异常，避免进入无限递归</param>
        private static void MutexExec(string filePath, Action action, bool recursive = false)
        {
            //生成文件对应的同步键，可自定义格式（互斥体名称对特殊字符支持不友好，遂转换为BASE64格式字符串）
            var fileKey = Convert.ToBase64String(Encoding.Default.GetBytes(Path.Combine("FILE", filePath)));
            //转换为操作系统级的同步键
            var mutexKey = Path.Combine("Global", fileKey);
            //声明一个已命名的互斥体，实现进程间同步；该命名互斥体不存在则自动创建，已存在则直接获取
            //initiallyOwned: false：默认当前线程并不拥有已存在互斥体的所属权，即默认本线程并非为首次创建该命名互斥体的线程
            //注意：并发声明同名的命名互斥体时，若间隔时间过短，则可能同时声明了多个名称相同的互斥体，并且同名的多个互斥体之间并不同步，高并发用户请另行处理
            using var mut = new Mutex(false, mutexKey);
            try
            {
                //上锁，其他线程需等待释放锁之后才能执行处理；若其他线程已经上锁或优先上锁，则先等待其他线程执行完毕
                mut.WaitOne();
                //执行处理代码（在调用WaitHandle.WaitOne至WaitHandle.ReleaseMutex的时间段里，只有一个线程处理，其他线程都得等待释放锁后才能执行该代码段）
                action();
            }
            //当其他进程已上锁且没有正常释放互斥锁时(譬如进程忽然关闭或退出)，则会抛出AbandonedMutexException异常
            catch (AbandonedMutexException)
            {
                //避免进入无限递归
                if (recursive) throw;

                //非递归调用，由其他进程抛出互斥锁解锁异常时，重试执行
                MutexExec(mutexKey, action, true);
            }
            finally
            {
                //释放锁，让其他进程(或线程)得以继续执行
                mut.ReleaseMutex();
            }
        }

        #endregion

        #endregion

        #region 加解密

        /// <summary>
        /// 32位的MD5加密
        /// </summary>
        /// <param name="str">待加密的文本</param>
        /// <param name="isUpper">是否大写(默认不是)</param>
        /// <returns>返回32位MD5</returns>
        public static string Md5(string str, bool isUpper = false)
        {
            var md5 = MD5.Create();
            var data = md5.ComputeHash(Encoding.Default.GetBytes(str));
            var sb = new StringBuilder();
            foreach (var t in data)
            {
                sb.AppendFormat(isUpper ? "{0:X2}" : "{0:x2}", t);
            }
            return sb.ToString();
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="str">加密字符</param>
        /// <param name="key">加密的key</param>
        /// <param name="isBase64">是否返回Base64字符串(默认16进制字符串)</param>
        /// <param name="cipherMode">加密模式(默认 ECB)</param>
        /// <param name="paddingMode">填充模式(默认pk-cs5padding)</param>
        /// <param name="iv">偏移量(默认与秘钥相同)</param>
        /// <param name="encoding">编码(默认UTF-8)</param>
        /// <returns></returns>
        public static string AesEncrypt(string str, string key, bool isBase64 = false, CipherMode cipherMode = CipherMode.ECB, PaddingMode paddingMode = PaddingMode.PKCS7, string iv = null, string encoding = "utf-8")
        {
            if (string.IsNullOrEmpty(str)) return null;
            var toEncryptArray = Encoding.GetEncoding(encoding).GetBytes(str);
            var keyArray = GetKey(Encoding.GetEncoding(encoding).GetBytes(key));
            var rm = new RijndaelManaged
            {
                Key = keyArray,
                Mode = cipherMode,
                Padding = paddingMode
            };
            if (cipherMode != CipherMode.ECB)
            {
                rm.IV = iv == null ? keyArray : Encoding.GetEncoding(encoding).GetBytes(iv);
            }
            var cTransform = rm.CreateEncryptor();
            var resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return isBase64 ? Convert.ToBase64String(resultArray, 0, resultArray.Length) : BytesToHex(resultArray);
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="str">加密字符</param>
        /// <param name="key">加密的key</param>
        /// <param name="isBase64">是否返回Base64字符串(默认16进制字符串)</param>
        /// <param name="cipherMode">加密模式(默认 ECB)</param>
        /// <param name="paddingMode">填充模式(默认pk-cs5padding)</param>
        /// <param name="iv">偏移量(默认与秘钥相同)</param>
        /// <param name="encoding">编码(默认UTF-8)</param>
        /// <returns></returns>
        public static string AesDecrypt(string str, string key, bool isBase64 = false, CipherMode cipherMode = CipherMode.ECB, PaddingMode paddingMode = PaddingMode.PKCS7, string iv = null, string encoding = "utf-8")
        {
            if (string.IsNullOrEmpty(str)) return null;
            var toEncryptArray = isBase64 ? Convert.FromBase64String(str) : HexToBytes(str);
            var keyArray = GetKey(Encoding.GetEncoding(encoding).GetBytes(key));
            var rm = new RijndaelManaged
            {
                Key = keyArray,
                Mode = cipherMode,
                Padding = paddingMode
            };
            if (cipherMode != CipherMode.ECB)
            {
                rm.IV = iv == null ? keyArray : Encoding.GetEncoding(encoding).GetBytes(iv);
            }
            var cTransform = rm.CreateDecryptor();
            var resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return Encoding.GetEncoding(encoding).GetString(resultArray);
        }

        /// <summary>
        /// 获取AES加解密KEY
        /// </summary>
        /// <param name="keyBytes"></param>
        /// <returns></returns>
        private static byte[] GetKey(byte[] keyBytes)
        {
            // 如果密钥位数不是16的倍数位，那么就补byte 0
            var baseLen = 16;
            if ((keyBytes.Length % baseLen) != 0)
            {
                var groups = (keyBytes.Length / baseLen) + ((keyBytes.Length % baseLen) != 0 ? 1 : 0);
                var temp = new byte[groups * baseLen];
                // 关键代码 补全
                Array.Copy(keyBytes, 0, temp, 0, keyBytes.Length);
                keyBytes = temp;
            }
            return keyBytes;
        }

        /// <summary>
        /// 16进制字符串转Byte数组
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        private static byte[] HexToBytes(string hex)
        {
            var num = (int)Math.Round((double)hex.Length / 2);
            var buffer = new byte[num];
            var num3 = num - 1;
            for (var i = 0; i <= num3; i++)
            {
                var s = hex.Substring(i * 2, 2);
                buffer[i] = (byte)int.Parse(s, NumberStyles.HexNumber);
            }
            return buffer;
        }

        /// <summary>
        /// Byte数组转16进制字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static string BytesToHex(IList<byte> bytes)
        {
            var builder = new StringBuilder();
            var num2 = bytes.Count - 1;
            for (var i = 0; i <= num2; i++)
            {
                builder.AppendFormat("{0:X2}", bytes[i]);
            }
            return builder.ToString();
        }

        #endregion

        #region 数据库实体生成

        /// <summary>
        /// 创建实体类文件
        /// </summary>
        /// <param name="path">创建到的路径</param>
        /// <param name="namespaceStr">类命名空间</param>
        public void CreateClassFile(string path, string namespaceStr)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var dal = DalFactory.CreateDal(MDbType);
                    var tableList = dal.GetAllTables(this);
                    foreach (var table in tableList)
                    {
                        var sb = new StringBuilder();
                        var columnList = dal.GetAllColumns(table["table_name"], this);
                        sb.AppendLine("// ReSharper disable InconsistentNaming");
                        sb.AppendLine("// ReSharper disable IdentifierTypo");
                        sb.AppendLine();
                        sb.AppendLine("namespace " + namespaceStr);
                        sb.AppendLine("{");
                        sb.AppendLine("    /// <summary>");
                        sb.AppendLine("    /// " + table["comments"]);
                        sb.AppendLine("    /// </summary>");
                        sb.AppendLine("    [PropertyChanged.AddINotifyPropertyChangedInterface]");
                        sb.AppendLine("    // ReSharper disable once PartialTypeWithSinglePart");
                        sb.AppendLine("    public partial class " + table["table_name"]);
                        sb.AppendLine("    {");
                        foreach (var column in columnList)
                        {
                            var dataType = dal.ConvertDataType(column);
                            sb.AppendLine("        /// <summary>");
                            sb.AppendLine("        /// " + column["comments"]);
                            sb.AppendLine("        /// </summary>");
                            var constraintType = column["constraint_type"] == "P" ? "true" : "false";
                            var notNull = column["notnull"] == "1" ? "false" : "true";
                            sb.AppendLine($"        [System.Data.Objects.DataClasses.EdmScalarProperty(EntityKeyProperty = {constraintType}, IsNullable = {notNull})]");
                            sb.AppendLine("        public " + dataType + " " + column["columns_name"] + " { get; set; }");
                        }
                        sb.AppendLine("    }");
                        sb.AppendLine("}");

                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }
                        var filePath = Path.Combine(path, table["table_name"] + ".cs");
                        File.WriteAllText(filePath, sb.ToString());
                    }
                }
                catch (Exception ex)
                {
                    DbLogError("生成实体异常=>" + ex.Message);
                }
            });
        }

        #endregion

    }
}
