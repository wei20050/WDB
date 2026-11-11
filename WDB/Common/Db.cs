using NetFrameWork.Database;
using NetFrameWork.Database.CreateEntity;
using Newtonsoft.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ServiceStack.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WDB.Entity;

namespace WDB.Common
{
    public class Db
    {

        private static string GetConnectionString(LianJie l)
        {
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.MySql:
                    return $"server={l.Host};port={l.Port};database={l.DataBaseName};user id={l.UserName};password={l.PassWord};SslMode=None;";
                case ORM.DbTypeEnum.SqlServer:
                    return $"Data Source={l.Host},{l.Port};Initial Catalog={l.DataBaseName};User ID={l.UserName};Password={l.PassWord};";
                case ORM.DbTypeEnum.SqlIte:
                    return $"Data Source={l.Host};password={l.PassWord};Version=3;";
                case ORM.DbTypeEnum.Oracle:
                    return $"Data Source={l.Host}:{l.Port}/{l.ServiceName};Pooling=false;User Id={l.UserName};password={l.PassWord};";
                case ORM.DbTypeEnum.Dameng:
                    return $"server={l.Host};port={l.Port};database={l.DataBaseName};User Id={l.UserName};PWD={l.PassWord};";
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.GaussDB:
                    return $"server={l.Host};port={l.Port};database={l.DataBaseName};User Id={l.UserName};PWD={l.PassWord};SearchPath={l.ServiceName};";
                default:
                    return $"server={l.Host};port={l.Port};database={l.DataBaseName};user id={l.UserName};password={l.PassWord};";
            }
        }

        private static ORM DB(LianJie l)
        {
            var providerName = ORM.GetTypeName(l.DataBaseType);
            var db = new ORM(new ConnectionStringSettings
            {
                ConnectionString = GetConnectionString(l),
                ProviderName = providerName
            });
            return db;
        }

        /// <summary>
        /// 获取 ORM 实例(供导出等需要直接操作连接的场景使用)
        /// </summary>
        public static ORM GetOrm(LianJie l)
        {
            return DB(l);
        }

        /// <summary>
        /// 测试连接(服务器级别,不再依赖某个具体库下的表)
        /// </summary>
        public static void Test(LianJie l)
        {
            try
            {
                if (!string.IsNullOrEmpty(l.NoDbType))
                {
                    //Redis/MongoDB 各有自己的连通性探测方法
                    var noDbOk = "MongoDB".Equals(l.NoDbType, StringComparison.OrdinalIgnoreCase)
                        ? GetMongo(l) != null
                        : GetNoDb(l) != null;
                    if (noDbOk)
                    {
                        Oops.Ok(l.NoDbType + " 连接成功");
                    }
                    else
                    {
                        Msg.Warning(l.NoDbType + " 连接失败,请检查主机、端口及账号密码");
                    }
                    return;
                }
                if (HasDatabaseLayer(l.DataBaseType))
                {
                    var dbNames = GetDataBases(l, out var error);
                    if (dbNames != null)
                    {
                        Oops.Ok(l.DataBaseType + " 连接成功");
                        return;
                    }
                    //列出数据库失败:把失败原因报出来(此前静默返回,点"测试"没有任何反馈)
                    if (!string.IsNullOrEmpty(error))
                    {
                        Msg.Warning("连接失败:" + error);
                        return;
                    }
                    //服务器可连但读不到库列表,且连接串缺少库名时无法回退到查表,给出明确提示
                    Msg.Warning(string.IsNullOrEmpty(l.DataBaseName)
                        ? "连接失败:请填写默认数据库后重试"
                        : "连接失败:无法读取数据库列表");
                    return;
                }
                //不支持数据库层级的类型(Oracle/Sqlite等)回退到查询表(内部失败已自带提示)
                if (GetTables(l) != null)
                {
                    Oops.Ok(l.DataBaseType + " 连接成功");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Msg.Warning(ex.Message);
            }
        }

        #region NoDb

        /// <summary>
        /// 解析 Redis 端口(非法值回退到默认 6379,避免直接抛异常)
        /// </summary>
        private static int GetRedisPort(string port)
        {
            return int.TryParse(port, out var value) && value > 0 ? value : 6379;
        }

        public static List<Biao> GetNoDb(LianJie l)
        {
            try
            {
                var list = new List<Biao>();
                var userName = string.IsNullOrEmpty(l.UserName) ? string.Empty : $"{l.UserName}:";
                RedisClient client = new RedisClient(l.Host, GetRedisPort(l.Port))
                {
                    Password = userName + l.PassWord
                };
                foreach (var info in client.Info)
                {
                    if (info.Key.StartsWith("db"))
                    {
                        var db = info.Key.Substring(2);
                        list.Add(new Biao
                        {
                            Name = db,
                            Comment = info.Value
                        });
                    }
                }
                var tmpList = new List<Biao>();
                for (int i = 0; i < 16; i++)
                {
                    var name = i.ToString();
                    var b = list.FirstOrDefault(p => p.Name == name);
                    if (b != null)
                    {
                        tmpList.Add(b);
                    }
                    else
                    {
                        tmpList.Add(new Biao { Name = name });
                    }
                }
                return tmpList;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            return null;
        }

        public static Dictionary<string, string> GetRedisInfo(LianJie l)
        {
            var userName = string.IsNullOrEmpty(l.UserName) ? string.Empty : $"{l.UserName}:";
            RedisClient client = new RedisClient(l.Host, GetRedisPort(l.Port))
            {
                ReceiveTimeout = 10000,
                SendTimeout = 10000,
                Password = userName + l.PassWord
            };
            return client.Info;
        }

        public static List<NoDbData> GetRedisKey(LianJie l, string name)
        {
            var userName = string.IsNullOrEmpty(l.UserName) ? string.Empty : $"{l.UserName}:";
            RedisClient client = new RedisClient(l.Host, GetRedisPort(l.Port))
            {
                ReceiveTimeout = 10000,
                SendTimeout = 10000,
                Password = userName + l.PassWord
            };
            client.Db = long.Parse(name);
            var keys = client.GetAllKeys();
            var list = new List<NoDbData>();
            foreach (var key in keys)
            {
                list.Add(new NoDbData
                {
                    Key = key
                });
            }
            return list;
        }

        /// <summary>
        /// 执行一条 Redis 命令(命令与参数以空格分隔,值里有空格可用单/双引号包起来)
        /// </summary>
        /// <param name="l">连接</param>
        /// <param name="db">逻辑库序号(相当于先执行 SELECT)</param>
        /// <param name="commandLine">命令行,例如 "GET name"、"HGETALL user:1"</param>
        /// <param name="isError">是否执行失败</param>
        /// <returns>命令结果文本(失败时为错误信息)</returns>
        public static string ExecuteRedis(LianJie l, long db, string commandLine, out bool isError)
        {
            isError = false;
            var args = SplitRedisCommand(commandLine);
            if (args.Count == 0)
            {
                return string.Empty;
            }
            var st = DateTime.Now;
            var info = $"[{l.Host}] {l.Name}{Environment.NewLine}[Redis] db{db}";
            try
            {
                var userName = string.IsNullOrEmpty(l.UserName) ? string.Empty : $"{l.UserName}:";
                RedisClient client = new RedisClient(l.Host, GetRedisPort(l.Port))
                {
                    ReceiveTimeout = 10000,
                    SendTimeout = 10000,
                    Password = userName + l.PassWord
                };
                if (db > 0)
                {
                    client.ChangeDb(db);
                }
                var data = client.RawCommand(args.Select(p => (object)p).ToArray());
                Config.AddSqlLog(info, st, commandLine, 0, (DateTime.Now - st).TotalMilliseconds, true);
                return FormatRedisData(data);
            }
            catch (Exception ex)
            {
                isError = true;
                Config.AddSqlLog(info, st, commandLine, 0, (DateTime.Now - st).TotalMilliseconds, false);
                Log.Error($"执行Redis命令[{commandLine}]失败:{ex.Message}");
                return ex.Message;
            }
        }

        /// <summary>
        /// 把一行 Redis 命令拆成命令与参数(支持单/双引号包裹带空格的值)
        /// </summary>
        public static List<string> SplitRedisCommand(string commandLine)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return list;
            }
            var sb = new StringBuilder();
            var quote = '\0';
            foreach (var c in commandLine.Trim())
            {
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    quote = c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                    {
                        list.Add(sb.ToString());
                        sb.Clear();
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0)
            {
                list.Add(sb.ToString());
            }
            return list;
        }

        /// <summary>
        /// 格式化 Redis 返回结果(多值结果按 1) 2) 逐行输出)
        /// </summary>
        private static string FormatRedisData(RedisData data)
        {
            if (data == null)
            {
                return "(nil)";
            }
            if (data.Children != null && data.Children.Count > 0)
            {
                var sb = new StringBuilder();
                for (var i = 0; i < data.Children.Count; i++)
                {
                    sb.Append(i + 1).Append(") ").Append(FormatRedisData(data.Children[i]));
                    if (i < data.Children.Count - 1)
                    {
                        sb.Append(Environment.NewLine);
                    }
                }
                return sb.ToString();
            }
            if (data.Data == null)
            {
                return "(nil)";
            }
            var text = Encoding.UTF8.GetString(data.Data);
            //空字符串明确显示出来,避免看起来像没有返回
            return string.IsNullOrEmpty(text) ? "\"\"" : text;
        }

        public static NoDbData GetRedisData(LianJie l, string name, string key)
        {
            var userName = string.IsNullOrEmpty(l.UserName) ? string.Empty : $"{l.UserName}:";
            RedisClient client = new RedisClient(l.Host, GetRedisPort(l.Port))
            {
                ReceiveTimeout = 10000,
                SendTimeout = 10000,
                Password = userName + l.PassWord
            };
            client.Db = long.Parse(name);
            var info = new NoDbData();
            info.Key = key;
            info.Type = client.Type(key);
            info.Ttl = client.Ttl(key);

            switch (info.Type)
            {
                case "string":
                    info.Size = client.StrLen(key);
                    info.Value = client.Get<string>(key);
                    break;
                case "hash":
                    info.Size = client.HLen(key);
                    info.Value = JsonConvert.SerializeObject(client.GetAllEntriesFromHash(key));
                    break;
                case "list":
                    info.Size = client.LLen(key);
                    info.Value = JsonConvert.SerializeObject(client.GetAllItemsFromList(key));
                    break;
                case "set":
                    info.Size = client.SCard(key);
                    info.Value = JsonConvert.SerializeObject(client.GetAllItemsFromSet(key));
                    break;
                case "zset":
                    info.Size = client.ZCard(key);
                    info.Value = JsonConvert.SerializeObject(client.GetRangeWithScoresFromSortedSet(key, 0, -1));
                    break;
                case "ReJSON":
                    info.Size = client.StrLen(key);
                    info.Value = client.Get<string>(key);
                    break;
                case "stream":
                    info.Size = client.HLen(key);
                    info.Value = "Stream 条目数：" + info.Size;
                    break;
                default:
                    info.Size = 0;
                    info.Value = "未知类型";
                    break;
            }
            return info;
        }

        public static void SetRedisData(LianJie l, string name, NoDbData data)
        {
            var st = DateTime.Now;
            var info = $"[{l.Host}] {l.Name}{Environment.NewLine}[Redis] db{name}";
            var command = $"SET {data.Key}";
            try
            {
                var userName = string.IsNullOrEmpty(l.UserName) ? string.Empty : $"{l.UserName}:";
                RedisClient client = new RedisClient(l.Host, GetRedisPort(l.Port))
                {
                    ReceiveTimeout = 10000,
                    SendTimeout = 10000,
                    Password = userName + l.PassWord
                };
                client.Db = long.Parse(name);
                if (data.Type == "string")
                {
                    client.SetValue(data.Key, data.Value);
                }
                if (data.Ttl > -1)
                {
                    _ = client.Expire(data.Key, (int)data.Ttl);
                }
                else
                {
                    client.Persist(data.Key);
                }
                Config.AddSqlLog(info, st, command, 1, (DateTime.Now - st).TotalMilliseconds, true);
                Oops.Ok("数据已保存");
            }
            catch (Exception ex)
            {
                Config.AddSqlLog(info, st, command, 0, (DateTime.Now - st).TotalMilliseconds, false);
                Log.Error(ex.Message);
                Oops.Error(ex.Message);
            }
        }

        public static void DeleteRedisData(LianJie l, string name, string[] keys)
        {
            var st = DateTime.Now;
            var info = $"[{l.Host}] {l.Name}{Environment.NewLine}[Redis] db{name}";
            var command = $"DEL {string.Join(" ", keys)}";
            try
            {
                var userName = string.IsNullOrEmpty(l.UserName) ? string.Empty : $"{l.UserName}:";
                RedisClient client = new RedisClient(l.Host, GetRedisPort(l.Port))
                {
                    ReceiveTimeout = 10000,
                    SendTimeout = 10000,
                    Password = userName + l.PassWord
                };
                client.Db = long.Parse(name);
                client.RemoveAll(keys);
                Config.AddSqlLog(info, st, command, keys.Length, (DateTime.Now - st).TotalMilliseconds, true);
                Oops.Ok("数据已删除 条数:" + keys.Length);
            }
            catch (Exception ex)
            {
                Config.AddSqlLog(info, st, command, 0, (DateTime.Now - st).TotalMilliseconds, false);
                Log.Error(ex.Message);
                Oops.Error(ex.Message);
            }
        }


        #endregion

        #region MongoDb

        /// <summary>
        /// 文档输出为 JSON 时的格式(缩进,标准 JSON 便于编辑与再解析)
        /// </summary>
        private static readonly MongoDB.Bson.IO.JsonWriterSettings MongoJsonSettings = new MongoDB.Bson.IO.JsonWriterSettings { Indent = true };

        /// <summary>
        /// 解析 MongoDB 端口(非法值回退到默认 27017)
        /// </summary>
        private static int GetMongoPort(string port)
        {
            return int.TryParse(port, out var value) && value > 0 ? value : 27017;
        }

        /// <summary>
        /// 创建 MongoDB 客户端
        /// 连接串组成:mongodb://账号:密码@主机:端口/ + authSource(认证库) + authMechanism(认证机制) + 附加 URL 参数
        /// </summary>
        private static MongoClient GetMongoClient(LianJie l)
        {
            var host = string.IsNullOrWhiteSpace(l.Host) ? "localhost" : l.Host.Trim();
            var sb = new StringBuilder("mongodb://");
            if (!string.IsNullOrEmpty(l.UserName))
            {
                //账号密码按 URL 规则转义,避免密码里的 @ : / 等字符破坏连接串
                sb.Append(Uri.EscapeDataString(l.UserName))
                    .Append(':')
                    .Append(Uri.EscapeDataString(l.PassWord ?? string.Empty))
                    .Append('@');
            }
            sb.Append(host).Append(':').Append(GetMongoPort(l.Port)).Append('/');
            var query = new List<string>();
            if (!string.IsNullOrEmpty(l.UserName))
            {
                //未指定认证库时按 MongoDB 惯例走 admin
                var authSource = string.IsNullOrWhiteSpace(l.AuthSource) ? "admin" : l.AuthSource.Trim();
                query.Add("authSource=" + Uri.EscapeDataString(authSource));
            }
            if (!string.IsNullOrWhiteSpace(l.AuthMechanism))
            {
                query.Add("authMechanism=" + Uri.EscapeDataString(l.AuthMechanism.Trim()));
            }
            var extra = (l.UrlParams ?? string.Empty).Trim().TrimStart('?').Trim().Trim('&');
            if (extra.Length > 0)
            {
                query.Add(extra);
            }
            if (query.Count > 0)
            {
                sb.Append('?').Append(string.Join("&", query));
            }
            var settings = MongoClientSettings.FromUrl(new MongoUrl(sb.ToString()));
            //驱动的 SocketTimeout 默认是 0(不超时):服务端接受连接却不响应时查询会永久挂起,
            //界面上就会一直卡在"加载中"。这里给一个兜底值,需要更长的等待就在 URL 参数里写 socketTimeoutMS
            if (settings.SocketTimeout <= TimeSpan.Zero)
            {
                settings.SocketTimeout = TimeSpan.FromSeconds(60);
            }
            return new MongoClient(settings);
        }

        /// <summary>
        /// 当前连接上下文对应的数据库名(优先取上下文,其次取连接上的初始数据库)
        /// </summary>
        private static string GetMongoDatabaseName(LianJie l)
        {
            return string.IsNullOrEmpty(l.DataBaseName) ? null : l.DataBaseName;
        }

        /// <summary>
        /// 字节数转可读大小
        /// </summary>
        private static string FormatMongoSize(double bytes)
        {
            if (bytes >= 1024 * 1024 * 1024)
            {
                return $"{bytes / 1024 / 1024 / 1024:0.##} GB";
            }
            if (bytes >= 1024 * 1024)
            {
                return $"{bytes / 1024 / 1024:0.##} MB";
            }
            if (bytes >= 1024)
            {
                return $"{bytes / 1024:0.##} KB";
            }
            return $"{bytes:0} B";
        }

        /// <summary>
        /// 列出 MongoDB 服务器上的全部数据库(注释为占用空间)
        /// </summary>
        public static List<Biao> GetMongo(LianJie l)
        {
            try
            {
                var client = GetMongoClient(l);
                var list = new List<Biao>();
                foreach (var doc in client.ListDatabases().ToList())
                {
                    if (!doc.Contains("name") || doc["name"].IsBsonNull)
                    {
                        continue;
                    }
                    var size = doc.Contains("sizeOnDisk") && !doc["sizeOnDisk"].IsBsonNull
                        ? doc["sizeOnDisk"].ToDouble()
                        : 0;
                    list.Add(new Biao
                    {
                        Name = doc["name"].AsString,
                        Comment = FormatMongoSize(size)
                    });
                }
                return list.OrderBy(p => p.Name).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Msg.Warning(ex.Message);
            }
            return null;
        }

        /// <summary>
        /// 列出指定数据库下的全部集合
        /// </summary>
        public static List<Biao> GetMongoCollections(LianJie l)
        {
            try
            {
                var dbName = GetMongoDatabaseName(l);
                if (string.IsNullOrEmpty(dbName))
                {
                    return new List<Biao>();
                }
                var names = GetMongoClient(l).GetDatabase(dbName).ListCollectionNames().ToList();
                return names.OrderBy(p => p).Select(p => new Biao { Name = p }).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Msg.Warning(ex.Message);
            }
            return null;
        }

        /// <summary>
        /// 查询集合文档(filterJson 为 JSON 过滤条件,_id 倒序取前 limit 条)
        /// </summary>
        public static List<NoDbData> GetMongoDocuments(LianJie l, string collection, string filterJson, int limit)
        {
            try
            {
                var dbName = GetMongoDatabaseName(l);
                if (string.IsNullOrEmpty(dbName))
                {
                    return new List<NoDbData>();
                }
                var coll = GetMongoClient(l).GetDatabase(dbName).GetCollection<BsonDocument>(collection);
                var docs = coll.Find(ParseMongoFilter(filterJson))
                    .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                    .Limit(limit <= 0 ? 100 : limit)
                    .ToList();
                return docs.Select(p => ToNoDbData(p)).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Msg.Warning(ex.Message);
            }
            return null;
        }

        /// <summary>
        /// 保存(存在则覆盖,不存在则新增)单个文档
        /// </summary>
        public static void SetMongoDocument(LianJie l, string collection, NoDbData data)
        {
            try
            {
                var dbName = GetMongoDatabaseName(l);
                var coll = GetMongoClient(l).GetDatabase(dbName).GetCollection<BsonDocument>(collection);
                var doc = ParseMongoDocument(data?.Value);
                if (string.IsNullOrEmpty(data?.Key))
                {
                    //未指定 _id 时由服务端生成
                    if (!doc.Contains("_id"))
                    {
                        doc.Add("_id", ObjectId.GenerateNewId());
                    }
                    coll.InsertOne(doc);
                }
                else
                {
                    var id = ParseMongoId(data.Key);
                    doc["_id"] = id;
                    coll.ReplaceOne(Builders<BsonDocument>.Filter.Eq("_id", id), doc, new ReplaceOptions { IsUpsert = true });
                }
                Oops.Ok("数据已保存");
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Oops.Error(ex.Message);
            }
        }

        /// <summary>
        /// 按 _id 批量删除文档
        /// </summary>
        public static void DeleteMongoDocuments(LianJie l, string collection, string[] ids)
        {
            try
            {
                var dbName = GetMongoDatabaseName(l);
                var coll = GetMongoClient(l).GetDatabase(dbName).GetCollection<BsonDocument>(collection);
                var values = ids.Where(p => !string.IsNullOrEmpty(p)).Select(ParseMongoId).ToList();
                var result = coll.DeleteMany(Builders<BsonDocument>.Filter.In("_id", values));
                Oops.Ok("数据已删除 条数:" + result.DeletedCount);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Oops.Error(ex.Message);
            }
        }

        /// <summary>
        /// MongoDB 服务器信息(供"查看属性"窗口使用)
        /// </summary>
        public static Dictionary<string, string> GetMongoInfo(LianJie l)
        {
            var dict = new Dictionary<string, string>();
            var client = GetMongoClient(l);
            var admin = client.GetDatabase("admin");
            try
            {
                AddMongoInfo(dict, "buildInfo", admin.RunCommand<BsonDocument>(new BsonDocument("buildInfo", 1)));
            }
            catch (Exception ex)
            {
                dict["buildInfo"] = ex.Message;
            }
            try
            {
                AddMongoInfo(dict, "serverStatus", admin.RunCommand<BsonDocument>(new BsonDocument("serverStatus", 1)));
            }
            catch (Exception ex)
            {
                dict["serverStatus"] = ex.Message;
            }
            try
            {
                var dbs = client.ListDatabases().ToList();
                for (var i = 0; i < dbs.Count; i++)
                {
                    var name = dbs[i].Contains("name") ? dbs[i]["name"].AsString : string.Empty;
                    var size = dbs[i].Contains("sizeOnDisk") ? dbs[i]["sizeOnDisk"].ToDouble() : 0;
                    dict[$"数据库{i + 1}"] = $"{name} ({FormatMongoSize(size)})";
                }
            }
            catch (Exception ex)
            {
                dict["数据库列表"] = ex.Message;
            }
            return dict;
        }

        /// <summary>
        /// 把服务端返回文档的顶层字段平铺进字典
        /// </summary>
        private static void AddMongoInfo(Dictionary<string, string> dict, string prefix, BsonDocument doc)
        {
            if (doc == null)
            {
                return;
            }
            foreach (var item in doc)
            {
                dict[$"{prefix}.{item.Name}"] = item.Value == null ? string.Empty : item.Value.ToJson();
            }
        }

        /// <summary>
        /// 执行一条 MongoDB shell 风格命令
        /// 支持:show dbs/collections、use db、db.stats()、db.version()、db.runCommand({...})、
        ///       db.&lt;集合&gt;.find/findOne/count/insert/insertOne/deleteOne/deleteMany/updateOne/drop
        /// </summary>
        /// <param name="l">连接</param>
        /// <param name="db">当前数据库(为空时用连接上的初始数据库)</param>
        /// <param name="commandLine">命令行</param>
        /// <param name="isError">是否执行失败</param>
        public static string ExecuteMongo(LianJie l, string db, string commandLine, out bool isError)
        {
            var st = DateTime.Now;
            var result = ExecuteMongoCore(l, db, commandLine, out isError);
            if (!string.IsNullOrEmpty((commandLine ?? string.Empty).Trim()))
            {
                var current = string.IsNullOrEmpty(db) ? GetMongoDatabaseName(l) : db;
                var info = $"[{l.Host}] {l.Name}{Environment.NewLine}[MongoDB] {current}";
                Config.AddSqlLog(info, st, commandLine.Trim(), 0, (DateTime.Now - st).TotalMilliseconds, !isError);
            }
            return result;
        }

        private static string ExecuteMongoCore(LianJie l, string db, string commandLine, out bool isError)
        {
            isError = false;
            var cmd = (commandLine ?? string.Empty).Trim().TrimEnd(';').Trim();
            if (cmd.Length == 0)
            {
                return string.Empty;
            }
            try
            {
                var client = GetMongoClient(l);
                var current = string.IsNullOrEmpty(db) ? GetMongoDatabaseName(l) : db;
                var lower = cmd.ToLowerInvariant();
                if (lower == "show dbs" || lower == "show databases")
                {
                    return string.Join(Environment.NewLine, client.ListDatabaseNames().ToList());
                }
                if (lower == "show collections" || lower == "show tables")
                {
                    if (string.IsNullOrEmpty(current))
                    {
                        throw new Exception("请先执行 use <数据库> 选择数据库");
                    }
                    return string.Join(Environment.NewLine, client.GetDatabase(current).ListCollectionNames().ToList());
                }
                if (lower.StartsWith("use "))
                {
                    //切换由界面层记录(与 Redis 的 SELECT 一致)
                    return $"已切换到数据库 [{cmd.Substring(4).Trim()}]";
                }
                //db.<操作>(参数):仅数据库级操作
                var dbMatch = Regex.Match(cmd, @"^db\.(?<op>[A-Za-z]+)\s*\((?<args>.*)\)$", RegexOptions.Singleline);
                if (dbMatch.Success)
                {
                    if (string.IsNullOrEmpty(current))
                    {
                        throw new Exception("请先执行 use <数据库> 选择数据库");
                    }
                    var op = dbMatch.Groups["op"].Value.ToLowerInvariant();
                    var args = SplitMongoArgs(dbMatch.Groups["args"].Value);
                    switch (op)
                    {
                        case "version":
                            var build = client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("buildInfo", 1));
                            return build.Contains("version") ? build["version"].ToString() : string.Empty;
                        case "stats":
                            return client.GetDatabase(current).RunCommand<BsonDocument>(new BsonDocument("dbStats", 1)).ToJson(MongoJsonSettings);
                        case "listcollections":
                        case "getcollectionnames":
                            return string.Join(Environment.NewLine, client.GetDatabase(current).ListCollectionNames().ToList());
                        case "runcommand":
                            if (args.Count == 0)
                            {
                                return string.Empty;
                            }
                            return client.GetDatabase(current)
                                .RunCommand<BsonDocument>(ParseMongoDocument(args[0]))
                                .ToJson(MongoJsonSettings);
                        case "dropdatabase":
                            client.DropDatabase(current);
                            return $"数据库 [{current}] 已删除";
                        default:
                            throw new Exception($"暂不支持的命令: db.{op}(...)");
                    }
                }
                //db.<集合>.<操作>(参数)
                var collMatch = Regex.Match(cmd, @"^db\.(?<coll>.+)\.(?<op>[A-Za-z]+)\s*\((?<args>.*)\)$", RegexOptions.Singleline);
                if (collMatch.Success)
                {
                    if (string.IsNullOrEmpty(current))
                    {
                        throw new Exception("请先执行 use <数据库> 选择数据库");
                    }
                    var collName = collMatch.Groups["coll"].Value.Trim();
                    var op = collMatch.Groups["op"].Value.ToLowerInvariant();
                    var args = SplitMongoArgs(collMatch.Groups["args"].Value);
                    var database = client.GetDatabase(current);
                    var coll = database.GetCollection<BsonDocument>(collName);
                    switch (op)
                    {
                        case "find":
                            {
                                var limit = 20;
                                if (args.Count > 1 && int.TryParse(args[1].Trim('{', '}', ' ').Replace("limit", string.Empty).Trim(':', ' '), out var lmt))
                                {
                                    limit = lmt;
                                }
                                var docs = coll.Find(args.Count > 0 ? ParseMongoFilter(args[0]) : new BsonDocument())
                                    .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                                    .Limit(limit)
                                    .ToList();
                                return FormatMongoDocs(docs);
                            }
                        case "findone":
                            {
                                var doc = coll.Find(args.Count > 0 ? ParseMongoFilter(args[0]) : new BsonDocument()).FirstOrDefault();
                                return doc == null ? "(空)" : doc.ToJson(MongoJsonSettings);
                            }
                        case "count":
                        case "countdocuments":
                            return coll.CountDocuments(args.Count > 0 ? ParseMongoFilter(args[0]) : new BsonDocument()).ToString();
                        case "insert":
                        case "insertone":
                            {
                                if (args.Count == 0)
                                {
                                    throw new Exception("请提供要插入的 JSON 文档");
                                }
                                var doc = ParseMongoDocument(args[0]);
                                if (!doc.Contains("_id"))
                                {
                                    doc.Add("_id", ObjectId.GenerateNewId());
                                }
                                coll.InsertOne(doc);
                                return $"插入成功 _id: {MongoIdToString(doc["_id"])}";
                            }
                        case "deleteone":
                            {
                                var result = coll.DeleteOne(args.Count > 0 ? ParseMongoFilter(args[0]) : new BsonDocument());
                                return $"删除成功 条数: {result.DeletedCount}";
                            }
                        case "deletemany":
                            {
                                var result = coll.DeleteMany(args.Count > 0 ? ParseMongoFilter(args[0]) : new BsonDocument());
                                return $"删除成功 条数: {result.DeletedCount}";
                            }
                        case "updateone":
                            {
                                if (args.Count < 2)
                                {
                                    throw new Exception("用法: db.<集合>.updateOne(<条件>, <文档>)");
                                }
                                var filter = ParseMongoFilter(args[0]);
                                var replacement = ParseMongoDocument(args[1]);
                                var result = coll.ReplaceOne(filter, replacement, new ReplaceOptions { IsUpsert = true });
                                var upsertId = result.UpsertedId == null ? "-" : MongoIdToString(result.UpsertedId);
                                return $"更新成功 (新增 {upsertId} / 修改 {result.ModifiedCount})";
                            }
                        case "drop":
                            database.DropCollection(collName);
                            return $"集合 [{collName}] 已删除";
                        default:
                            throw new Exception($"暂不支持的命令: db.{collName}.{op}(...)");
                    }
                }
                throw new Exception($"无法识别的命令: {cmd}");
            }
            catch (Exception ex)
            {
                isError = true;
                Log.Error($"执行MongoDB命令[{commandLine}]失败:{ex.Message}");
                return ex.Message;
            }
        }

        /// <summary>
        /// 把一行 MongoDB 命令按"顶层空格"拆分(引号与括号内的空格不拆分)
        /// </summary>
        public static List<string> SplitMongoCommand(string commandLine)
        {
            return SplitMongoTop(commandLine, false);
        }

        /// <summary>
        /// 把参数列表按"顶层逗号"拆分
        /// </summary>
        private static List<string> SplitMongoArgs(string args)
        {
            return SplitMongoTop(args, true);
        }

        /// <summary>
        /// 顶层分隔(byComma=true 按逗号,否则按空格),忽略引号与括号内部
        /// </summary>
        private static List<string> SplitMongoTop(string text, bool byComma)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return list;
            }
            var sb = new StringBuilder();
            var quote = '\0';
            var depth = 0;
            foreach (var c in text.Trim())
            {
                if (quote != '\0')
                {
                    sb.Append(c);
                    if (c == quote)
                    {
                        quote = '\0';
                    }
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    quote = c;
                    sb.Append(c);
                    continue;
                }
                if (c == '{' || c == '[' || c == '(')
                {
                    depth++;
                    sb.Append(c);
                    continue;
                }
                if (c == '}' || c == ']' || c == ')')
                {
                    depth--;
                    sb.Append(c);
                    continue;
                }
                var isSplit = byComma ? c == ',' : char.IsWhiteSpace(c);
                if (depth == 0 && isSplit)
                {
                    if (sb.Length > 0)
                    {
                        list.Add(sb.ToString().Trim());
                        sb.Clear();
                    }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0 && sb.ToString().Trim().Length > 0)
            {
                list.Add(sb.ToString().Trim());
            }
            return list;
        }

        /// <summary>
        /// 文档转浏览数据(_id 作为键,缩进 JSON 作为值)
        /// </summary>
        private static NoDbData ToNoDbData(BsonDocument doc)
        {
            var json = doc.ToJson(MongoJsonSettings);
            return new NoDbData
            {
                Key = doc.Contains("_id") ? MongoIdToString(doc["_id"]) : null,
                Type = "document",
                Value = json,
                Size = Encoding.UTF8.GetByteCount(json)
            };
        }

        private static string FormatMongoDocs(IEnumerable<BsonDocument> docs)
        {
            var list = docs.ToList();
            if (list.Count == 0)
            {
                return "(空)";
            }
            var sb = new StringBuilder();
            for (var i = 0; i < list.Count; i++)
            {
                sb.Append(i + 1).Append(") ").Append(list[i].ToJson(MongoJsonSettings));
                if (i < list.Count - 1)
                {
                    sb.Append(Environment.NewLine);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// _id 的展示文本
        /// </summary>
        private static string MongoIdToString(BsonValue id)
        {
            if (id == null || id.IsBsonNull)
            {
                return null;
            }
            if (id.IsObjectId)
            {
                return id.AsObjectId.ToString();
            }
            if (id.IsString)
            {
                return id.AsString;
            }
            return id.ToJson();
        }

        /// <summary>
        /// 把展示文本还原成 _id 值(依次尝试 ObjectId/整数/小数/布尔/字符串)
        /// </summary>
        public static BsonValue ParseMongoId(string text)
        {
            if (text == null)
            {
                return BsonNull.Value;
            }
            if (ObjectId.TryParse(text, out var oid))
            {
                return oid;
            }
            if (int.TryParse(text, out var iv))
            {
                return iv;
            }
            if (long.TryParse(text, out var lv))
            {
                return lv;
            }
            if (double.TryParse(text, out var dv))
            {
                return dv;
            }
            if (bool.TryParse(text, out var bv))
            {
                return bv;
            }
            return text;
        }

        /// <summary>
        /// 解析 JSON 过滤条件(空条件返回空文档)
        /// </summary>
        private static BsonDocument ParseMongoFilter(string json)
        {
            var text = NormalizeMongoJson(json);
            if (string.IsNullOrWhiteSpace(text) || text == "{}")
            {
                return new BsonDocument();
            }
            return BsonDocument.Parse(text);
        }

        /// <summary>
        /// 解析 JSON 文档
        /// </summary>
        private static BsonDocument ParseMongoDocument(string json)
        {
            var text = NormalizeMongoJson(json);
            if (string.IsNullOrWhiteSpace(text) || text == "{}" )
            {
                return new BsonDocument();
            }
            return BsonDocument.Parse(text);
        }

        /// <summary>
        /// 把 mongo shell 的 JS 字面量规范化成标准 JSON:
        /// 1)ObjectId("...")/ISODate("...")/NumberLong(..)/NumberInt(..) 转扩展 JSON
        /// 2)单引号字符串转双引号
        /// 3)未加引号的键补上引号
        /// </summary>
        public static string NormalizeMongoJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            var t = text.Trim();
            t = Regex.Replace(t, @"ObjectId\s*\(\s*['""]([0-9a-fA-F]{24})['""]\s*\)", "{\"$$oid\":\"$1\"}");
            t = Regex.Replace(t, @"ISODate\s*\(\s*['""]([^'""]*)['""]\s*\)", "{\"$$date\":\"$1\"}");
            t = Regex.Replace(t, @"NumberLong\s*\(\s*(-?\d+)\s*\)", "{\"$$numberLong\":\"$1\"}");
            t = Regex.Replace(t, @"NumberInt\s*\(\s*(-?\d+)\s*\)", "{\"$$numberInt\":\"$1\"}");

            var sb = new StringBuilder();
            var quote = '\0';
            var prev = '\0';
            for (var i = 0; i < t.Length; i++)
            {
                var c = t[i];
                if (quote != '\0')
                {
                    if (c == '\\' && i + 1 < t.Length)
                    {
                        sb.Append(c).Append(t[++i]);
                        continue;
                    }
                    if (c == quote)
                    {
                        sb.Append('"');
                        quote = '\0';
                        prev = '"';
                        continue;
                    }
                    sb.Append(c == '"' ? "\\\"" : c.ToString());
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    quote = c;
                    sb.Append('"');
                    prev = '"';
                    continue;
                }
                if (char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                    continue;
                }
                if (char.IsLetter(c) || c == '_' || c == '$')
                {
                    var start = i;
                    while (i < t.Length && (char.IsLetterOrDigit(t[i]) || t[i] == '_' || t[i] == '$'))
                    {
                        i++;
                    }
                    var word = t.Substring(start, i - start);
                    var j = i;
                    while (j < t.Length && char.IsWhiteSpace(t[j]))
                    {
                        j++;
                    }
                    //仅对象里紧跟在 { 或 , 后面的"标识符:"才当作键
                    if (j < t.Length && t[j] == ':' && (prev == '{' || prev == ','))
                    {
                        sb.Append('"').Append(word).Append('"');
                    }
                    else
                    {
                        sb.Append(word);
                    }
                    i--;
                    prev = 'x';
                    continue;
                }
                sb.Append(c);
                prev = c;
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// BsonValue 转表格单元格文本(嵌套对象/数组输出紧凑 JSON)
        /// </summary>
        public static string MongoValueToText(BsonValue value)
        {
            if (value == null || value.IsBsonNull)
            {
                return null;
            }
            if (value.IsString)
            {
                return value.AsString;
            }
            if (value.IsObjectId)
            {
                return value.AsObjectId.ToString();
            }
            if (value.IsBsonDateTime)
            {
                return value.AsBsonDateTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
            if (value.IsBoolean)
            {
                return value.AsBoolean ? "true" : "false";
            }
            return value.ToJson();
        }

        /// <summary>
        /// 表格单元格文本还原成 BsonValue:
        /// 先按该字段原值的类型解析(避免把字符串"123"误存成数字),原值为对象/数组时按 JSON 解析
        /// </summary>
        /// <param name="value">单元格值(可能是 DBNull)</param>
        /// <param name="origin">该字段原值的 BsonValue(可能为 null)</param>
        public static BsonValue MongoTextToValue(object value, BsonValue origin)
        {
            if (value == null || value == DBNull.Value)
            {
                return BsonNull.Value;
            }
            var text = value.ToString();
            if (origin != null && !origin.IsBsonNull)
            {
                if (origin.IsBoolean && bool.TryParse(text, out var b))
                {
                    return b;
                }
                if (origin.IsInt32 && int.TryParse(text, out var i))
                {
                    return i;
                }
                if (origin.IsInt64 && long.TryParse(text, out var l))
                {
                    return l;
                }
                if (origin.IsDouble && double.TryParse(text, out var d))
                {
                    return d;
                }
                if (origin.IsDecimal128 && decimal.TryParse(text, out var m))
                {
                    return new BsonDecimal128(m);
                }
                if (origin.IsBsonDateTime && DateTime.TryParse(text, out var dt))
                {
                    return new BsonDateTime(dt);
                }
                if (origin.IsObjectId && ObjectId.TryParse(text, out var oid))
                {
                    return oid;
                }
            }
            var t = text.Trim();
            if (t.Length > 1 && t.StartsWith("{") && t.EndsWith("}"))
            {
                try
                {
                    return BsonDocument.Parse(NormalizeMongoJson(t));
                }
                catch
                {
                    //解析失败按普通字符串处理
                }
            }
            if (t.Length > 1 && t.StartsWith("[") && t.EndsWith("]"))
            {
                try
                {
                    return MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonArray>(NormalizeMongoJson(t));
                }
                catch
                {
                    //解析失败按普通字符串处理
                }
            }
            return text;
        }

        /// <summary>
        /// 解析排序 JSON(形如 {"_id":-1,"name":1};为空时按 _id 倒序)
        /// </summary>
        private static SortDefinition<BsonDocument> ParseMongoSort(string sortJson)
        {
            var builder = Builders<BsonDocument>.Sort;
            var text = NormalizeMongoJson(sortJson);
            if (string.IsNullOrWhiteSpace(text) || text == "{}")
            {
                return builder.Descending("_id");
            }
            var defs = new List<SortDefinition<BsonDocument>>();
            foreach (var item in BsonDocument.Parse(text))
            {
                var asc = item.Value.IsNumeric && item.Value.ToInt32() >= 0;
                defs.Add(asc ? builder.Ascending(item.Name) : builder.Descending(item.Name));
            }
            return defs.Count == 0 ? builder.Descending("_id") : builder.Combine(defs);
        }

        /// <summary>
        /// 按投影 JSON 裁剪文档字段(包含模式只保留列出的字段;排除模式去掉列出的字段)
        /// _id 始终保留,否则表格无法定位文档
        /// </summary>
        private static void ApplyMongoProjection(List<BsonDocument> docs, string projectionJson)
        {
            var text = NormalizeMongoJson(projectionJson);
            if (docs == null || docs.Count == 0 || string.IsNullOrWhiteSpace(text) || text == "{}")
            {
                return;
            }
            var includes = new List<string>();
            var excludes = new List<string>();
            foreach (var item in BsonDocument.Parse(text))
            {
                if (item.Name == "_id")
                {
                    continue;
                }
                //1/true 为包含,0/false 为排除
                if (item.Value.IsNumeric && item.Value.ToInt32() != 0)
                {
                    includes.Add(item.Name);
                }
                else
                {
                    excludes.Add(item.Name);
                }
            }
            foreach (var doc in docs)
            {
                foreach (var name in doc.Names.ToList())
                {
                    if (name == "_id")
                    {
                        continue;
                    }
                    var keep = includes.Count > 0 ? includes.Contains(name) : !excludes.Contains(name);
                    if (!keep)
                    {
                        doc.Remove(name);
                    }
                }
            }
        }

        /// <summary>
        /// 查询集合文档并转成可编辑的 DataTable(每行一个文档,列取文档字段并集,_id 放在首列)
        /// 行与原始文档的对应关系记录在 DataTable.ExtendedProperties["MongoDocs"] 中,供保存时定位 _id 与还原类型
        /// </summary>
        /// <param name="l">连接(需含数据库名)</param>
        /// <param name="collection">集合名</param>
        /// <param name="filterJson">过滤条件 JSON</param>
        /// <param name="sortJson">排序 JSON(为空按 _id 倒序)</param>
        /// <param name="projectionJson">投影 JSON(可为空)</param>
        /// <param name="offset">跳过条数</param>
        /// <param name="limit">最多条数</param>
        /// <param name="error">失败原因(成功为 null)</param>
        public static DataTable GetMongoDataTable(LianJie l, string collection, string filterJson, string sortJson, string projectionJson, int offset, int limit, out string error)
        {
            error = null;
            try
            {
                var dbName = GetMongoDatabaseName(l);
                if (string.IsNullOrEmpty(dbName))
                {
                    error = "未指定数据库";
                    return null;
                }
                var coll = GetMongoClient(l).GetDatabase(dbName).GetCollection<BsonDocument>(collection);
                var docs = coll.Find(ParseMongoFilter(filterJson))
                    .Sort(ParseMongoSort(sortJson))
                    .Skip(offset < 0 ? 0 : offset)
                    .Limit(limit <= 0 ? 100 : limit)
                    .ToList();
                ApplyMongoProjection(docs, projectionJson);

                var dt = new DataTable(collection);
                dt.Columns.Add("_id", typeof(string));
                //先汇总全部列(保持首次出现的顺序),再逐行填充
                foreach (var doc in docs)
                {
                    foreach (var item in doc)
                    {
                        if (item.Name != "_id" && !dt.Columns.Contains(item.Name))
                        {
                            dt.Columns.Add(item.Name, typeof(string));
                        }
                    }
                }
                var idMap = new Dictionary<DataRow, BsonDocument>();
                foreach (var doc in docs)
                {
                    var row = dt.NewRow();
                    row["_id"] = doc.Contains("_id") ? MongoIdToString(doc["_id"]) : null;
                    foreach (var item in doc)
                    {
                        if (item.Name == "_id")
                        {
                            continue;
                        }
                        row[item.Name] = (object)MongoValueToText(item.Value) ?? DBNull.Value;
                    }
                    dt.Rows.Add(row);
                    idMap[row] = doc;
                }
                dt.ExtendedProperties["MongoDocs"] = idMap;
                return dt;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                error = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 取样集合文档汇总顶层字段名(供智能提示使用,_id 固定在最前)
        /// </summary>
        public static List<string> GetMongoCollectionFields(LianJie l, string collection, int sampleSize = 50)
        {
            var names = new List<string> { "_id" };
            try
            {
                var dbName = GetMongoDatabaseName(l);
                if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(collection))
                {
                    return names;
                }
                var docs = GetMongoClient(l).GetDatabase(dbName).GetCollection<BsonDocument>(collection)
                    .Find(new BsonDocument())
                    .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                    .Limit(sampleSize <= 0 ? 50 : sampleSize)
                    .ToList();
                foreach (var doc in docs)
                {
                    foreach (var item in doc)
                    {
                        if (!names.Contains(item.Name))
                        {
                            names.Add(item.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            return names;
        }

        /// <summary>
        /// 取样集合文档,汇总字段名与样本类型(供筛选&amp;排序面板的字段下拉与值字面量判断)
        /// </summary>
        public static List<ZiDuan> GetMongoCollectionZiDuans(LianJie l, string collection, int sampleSize = 50)
        {
            var list = new List<ZiDuan> { new ZiDuan { Name = "_id" } };
            try
            {
                var dbName = GetMongoDatabaseName(l);
                if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(collection))
                {
                    return list;
                }
                var docs = GetMongoClient(l).GetDatabase(dbName).GetCollection<BsonDocument>(collection)
                    .Find(new BsonDocument())
                    .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                    .Limit(sampleSize <= 0 ? 50 : sampleSize)
                    .ToList();
                foreach (var doc in docs)
                {
                    foreach (var item in doc)
                    {
                        var exist = list.FirstOrDefault(p => p.Name == item.Name);
                        if (exist == null)
                        {
                            list.Add(new ZiDuan { Name = item.Name, Type = MongoTypeName(item.Value) });
                        }
                        else if (string.IsNullOrEmpty(exist.Type))
                        {
                            exist.Type = MongoTypeName(item.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            return list;
        }

        /// <summary>
        /// BsonValue 对应的类型名(供值字面量判断)
        /// </summary>
        private static string MongoTypeName(BsonValue value)
        {
            if (value == null || value.IsBsonNull)
            {
                return null;
            }
            if (value.IsString)
            {
                return "string";
            }
            if (value.IsBoolean)
            {
                return "bool";
            }
            if (value.IsInt32)
            {
                return "int32";
            }
            if (value.IsInt64)
            {
                return "int64";
            }
            if (value.IsDouble)
            {
                return "double";
            }
            if (value.IsDecimal128)
            {
                return "decimal";
            }
            if (value.IsBsonDateTime)
            {
                return "date";
            }
            if (value.IsObjectId)
            {
                return "objectId";
            }
            if (value.IsBsonArray)
            {
                return "array";
            }
            if (value.IsBsonDocument)
            {
                return "object";
            }
            return "string";
        }

        /// <summary>
        /// 判断一条 MongoDB 命令是否走分页:形如 db.集合.find(...) (可带 .sort() 链),且没自带 .limit()/.skip()。
        /// 命中时 head 为去掉尾部 .sort() 之外的查询主体(find 部分),翻页时在它后面补 .skip()/.limit()
        /// </summary>
        public static bool TrySplitMongoForPage(string commandLine, out string head)
        {
            head = null;
            var cmd = (commandLine ?? string.Empty).Trim().TrimEnd(';').Trim();
            if (cmd.Length == 0)
            {
                return false;
            }
            var body = cmd;
            while (true)
            {
                var cm = Regex.Match(body, @"\.(?<fn>sort|limit|skip)\s*\((?<arg>[^()]*)\)\s*$", RegexOptions.IgnoreCase);
                if (!cm.Success)
                {
                    break;
                }
                //自带 limit/skip 的命令按用户写法原样执行(与 SQL 页"自带行数限制就不分页"一致)
                if (!cm.Groups["fn"].Value.Equals("sort", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                body = body.Substring(0, cm.Index).TrimEnd();
            }
            //findOne 固定只返回一条,不参与分页
            if (!Regex.IsMatch(body, @"^db\.(.+)\.find\s*\(.*\)$", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                return false;
            }
            head = body;
            return true;
        }

        /// <summary>
        /// 执行查询框里输入的一条 MongoDB 命令:
        /// find/findOne 返回可编辑的文档表,其它命令返回单列文本结果集
        /// </summary>
        /// <param name="l">连接</param>
        /// <param name="db">当前数据库(为空时用连接上的初始数据库)</param>
        /// <param name="commandLine">命令行</param>
        /// <param name="limit">find 结果最多返回的条数</param>
        /// <param name="isRet">是否查询类命令(返回结果集)</param>
        /// <param name="error">失败原因(成功为 null)</param>
        public static DataSet ExecuteMongoSet(LianJie l, string db, string commandLine, int limit, out bool isRet, out string error)
        {
            isRet = false;
            error = null;
            var ds = new DataSet();
            var cmd = (commandLine ?? string.Empty).Trim().TrimEnd(';').Trim();
            if (cmd.Length == 0)
            {
                return ds;
            }
            var current = string.IsNullOrEmpty(db) ? GetMongoDatabaseName(l) : db;
            //先剥掉尾部的 .sort()/.limit()/.skip() 链式调用(快速查询生成的就是这种形态),
            //剩下的头部才是 db.<集合>.find(<参数>)
            var chain = new Dictionary<string, string>();
            var head = cmd;
            while (true)
            {
                var cm = Regex.Match(head, @"\.(?<fn>sort|limit|skip)\s*\((?<arg>[^()]*)\)\s*$", RegexOptions.IgnoreCase);
                if (!cm.Success)
                {
                    break;
                }
                chain[cm.Groups["fn"].Value.ToLowerInvariant()] = cm.Groups["arg"].Value.Trim();
                head = head.Substring(0, cm.Index).TrimEnd();
            }
            //集合名允许含点(如 system.users),用贪婪匹配取到最后一个 .find( 之前
            var match = Regex.Match(head, @"^db\.(?<coll>.+)\.(?<op>find|findOne)\s*\((?<args>.*)\)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success && !string.IsNullOrEmpty(current))
            {
                var st = DateTime.Now;
                var info = $"[{l.Host}] {l.Name}{Environment.NewLine}[MongoDB] {current}";
                var collName = match.Groups["coll"].Value.Trim();
                var args = SplitMongoArgs(match.Groups["args"].Value);
                var isOne = match.Groups["op"].Value.Equals("findOne", StringComparison.OrdinalIgnoreCase);
                //链式 .limit()/.skip() 优先,缺省时 limit 用界面上的条数(单个文档固定取 1)
                var limitValue = isOne ? 1 : limit;
                if (chain.TryGetValue("limit", out var lmtText) && int.TryParse(lmtText, out var lmt) && lmt > 0)
                {
                    limitValue = lmt;
                }
                var skipValue = chain.TryGetValue("skip", out var skipText) && int.TryParse(skipText, out var skp) && skp > 0 ? skp : 0;
                var sortJson = chain.TryGetValue("sort", out var sortText) ? sortText : null;
                var context = CreateContext(l, current);
                var dt = GetMongoDataTable(
                    context,
                    collName,
                    args.Count > 0 ? args[0] : null,
                    sortJson,
                    args.Count > 1 ? args[1] : null,
                    skipValue,
                    limitValue,
                    out var err);
                if (dt == null)
                {
                    Config.AddSqlLog(info, st, cmd, 0, (DateTime.Now - st).TotalMilliseconds, false);
                    error = err;
                    return ds;
                }
                Config.AddSqlLog(info, st, cmd, dt.Rows.Count, (DateTime.Now - st).TotalMilliseconds, true);
                ds.Tables.Add(dt);
                isRet = true;
                return ds;
            }
            var text = ExecuteMongo(l, db, cmd, out var isError);
            if (isError)
            {
                error = text;
                return ds;
            }
            ds.Tables.Add(BuildTextTable("结果", text));
            return ds;
        }

        /// <summary>
        /// 把文本结果包装成单列结果集(便于和其它结果一样用表格展示)
        /// </summary>
        private static DataTable BuildTextTable(string tableName, string text)
        {
            var dt = new DataTable(tableName);
            dt.Columns.Add("结果", typeof(string));
            foreach (var line in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
            {
                dt.Rows.Add(line);
            }
            return dt;
        }

        /// <summary>
        /// 新增一个文档(供表格内新增行使用)
        /// </summary>
        public static void InsertMongoDocument(LianJie l, string collection, BsonDocument doc)
        {
            var st = DateTime.Now;
            var dbName = GetMongoDatabaseName(l);
            var info = $"[{l.Host}] {l.Name}{Environment.NewLine}[MongoDB] {dbName}";
            var command = $"db.{collection}.insertOne({doc?.ToJson()})";
            try
            {
                GetMongoClient(l).GetDatabase(dbName).GetCollection<BsonDocument>(collection).InsertOne(doc);
                Config.AddSqlLog(info, st, command, 1, (DateTime.Now - st).TotalMilliseconds, true);
            }
            catch (Exception)
            {
                Config.AddSqlLog(info, st, command, 0, (DateTime.Now - st).TotalMilliseconds, false);
                throw;
            }
        }

        /// <summary>
        /// 按 _id 更新文档的若干字段(供表格内修改单元格使用),返回实际修改条数
        /// </summary>
        public static long UpdateMongoDocumentFields(LianJie l, string collection, BsonValue id, Dictionary<string, BsonValue> fields)
        {
            var st = DateTime.Now;
            var dbName = GetMongoDatabaseName(l);
            var info = $"[{l.Host}] {l.Name}{Environment.NewLine}[MongoDB] {dbName}";
            var updates = new BsonDocument();
            foreach (var item in fields)
            {
                updates[item.Key] = item.Value ?? BsonNull.Value;
            }
            var command = $"db.{collection}.updateOne(_id: {MongoIdToString(id)}, {{ $set: {updates.ToJson()} }})";
            try
            {
                var result = GetMongoClient(l).GetDatabase(dbName).GetCollection<BsonDocument>(collection)
                    .UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", id), new BsonDocument("$set", updates));
                Config.AddSqlLog(info, st, command, (int)result.ModifiedCount, (DateTime.Now - st).TotalMilliseconds, true);
                return result.ModifiedCount;
            }
            catch (Exception)
            {
                Config.AddSqlLog(info, st, command, 0, (DateTime.Now - st).TotalMilliseconds, false);
                throw;
            }
        }

        /// <summary>
        /// 按 _id 批量删除文档(供表格内删除行使用),返回实际删除条数
        /// </summary>
        public static long DeleteMongoDocumentsByIds(LianJie l, string collection, List<BsonValue> ids)
        {
            var st = DateTime.Now;
            var dbName = GetMongoDatabaseName(l);
            var info = $"[{l.Host}] {l.Name}{Environment.NewLine}[MongoDB] {dbName}";
            var command = $"db.{collection}.deleteMany(_id in [{string.Join(",", ids.Select(MongoIdToString))}])";
            try
            {
                var result = GetMongoClient(l).GetDatabase(dbName).GetCollection<BsonDocument>(collection)
                    .DeleteMany(Builders<BsonDocument>.Filter.In("_id", ids));
                Config.AddSqlLog(info, st, command, (int)result.DeletedCount, (DateTime.Now - st).TotalMilliseconds, true);
                return result.DeletedCount;
            }
            catch (Exception)
            {
                Config.AddSqlLog(info, st, command, 0, (DateTime.Now - st).TotalMilliseconds, false);
                throw;
            }
        }

        #endregion

        /// <summary>
        /// 是否存在"数据库"层级(服务器下可列出多个数据库)
        /// </summary>
        public static bool HasDatabaseLayer(ORM.DbTypeEnum type)
        {
            switch (type)
            {
                case ORM.DbTypeEnum.MySql:
                case ORM.DbTypeEnum.SqlServer:
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 是否存在"模式(schema)"层级
        /// </summary>
        public static bool HasSchemaLayer(ORM.DbTypeEnum type)
        {
            switch (type)
            {
                case ORM.DbTypeEnum.SqlServer:
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 以指定数据库/模式创建连接上下文副本(不改动原始连接配置)
        /// </summary>
        public static LianJie CreateContext(LianJie l, string database = null, string schema = null)
        {
            var context = CoreConvert.Map<LianJie>(l);
            if (database != null)
            {
                context.DataBaseName = database;
            }
            if (schema != null)
            {
                context.ServiceName = schema;
            }
            context.BiaoList = null;
            return context;
        }

        /// <summary>
        /// 获取服务器上的所有数据库
        /// </summary>
        public static List<string> GetDataBases(LianJie l)
        {
            return GetDataBases(l, out _);
        }

        /// <summary>
        /// 获取服务器上的所有数据库,失败返回 null 并通过 error 返回失败原因
        /// </summary>
        public static List<string> GetDataBases(LianJie l, out string error)
        {
            error = null;
            string sql;
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.MySql:
                    sql = "SELECT SCHEMA_NAME FROM information_schema.SCHEMATA ORDER BY SCHEMA_NAME";
                    break;
                case ORM.DbTypeEnum.SqlServer:
                    sql = "SELECT name FROM sys.databases ORDER BY name";
                    break;
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    sql = "SELECT datname FROM pg_database WHERE datistemplate = false ORDER BY datname";
                    break;
                default:
                    return null;
            }
            return QueryNames(l, sql, out error);
        }

        /// <summary>
        /// 获取指定数据库下的所有模式
        /// </summary>
        public static List<string> GetSchemas(LianJie l, string database)
        {
            string sql;
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.SqlServer:
                    sql = "SELECT name FROM sys.schemas WHERE name NOT IN ('sys','INFORMATION_SCHEMA','guest') ORDER BY name";
                    break;
                case ORM.DbTypeEnum.Pgsql:
                    sql = "SELECT nspname FROM pg_namespace WHERE nspname NOT LIKE 'pg\\_%' AND nspname <> 'information_schema' ORDER BY nspname";
                    break;
                case ORM.DbTypeEnum.Kingbase:
                    //KingbaseES 建库时会默认创建 SYS_CATALOG 系统模式(存系统表/内置函数),不作为业务模式展示
                    sql = "SELECT nspname FROM pg_namespace WHERE nspname NOT LIKE 'pg\\_%' AND nspname <> 'information_schema' AND nspname <> 'sys_catalog' ORDER BY nspname";
                    break;
                case ORM.DbTypeEnum.GaussDB:
                    //openGauss/GaussDB 内置大量系统模式(dbe_*/pkg_*/prvt_*/rds*/cstore/snapshot/sqladvisor/blockchain/db4ai/sys 等),
                    //官方明确这些模式禁止放业务数据,查询时按前缀+名单排除,否则全混进树里
                    sql = "SELECT nspname FROM pg_namespace WHERE nspname NOT LIKE 'pg\\_%' AND nspname <> 'information_schema'"
                        + " AND nspname !~ '^(dbe_|pkg_|prvt_|rds)'"
                        + " AND nspname NOT IN ('sys','snapshot','sqladvisor','cstore','blockchain','db4ai','block','ddb','expression','pgagent','gtmsystem','dbmsystem','gamematch')"
                        + " ORDER BY nspname";
                    break;
                default:
                    return null;
            }
            return QueryNames(CreateContext(l, database), sql);
        }

        /// <summary>
        /// 查询并返回第一列(用于库名/模式名列表),失败返回 null
        /// </summary>
        private static List<string> QueryNames(LianJie l, string sql)
        {
            return QueryNames(l, sql, out _);
        }

        /// <summary>
        /// 查询并返回第一列(用于库名/模式名列表),失败返回 null 并通过 error 返回失败原因
        /// </summary>
        private static List<string> QueryNames(LianJie l, string sql, out string error)
        {
            error = null;
            try
            {
                var list = new List<string>();
                var ds = DB(l).ExecuteSetTry(sql);
                if (ds != null && ds.Tables.Count > 0)
                {
                    foreach (DataRow info in ds.Tables[0].Rows)
                    {
                        list.Add(info[0].ToString());
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Log.Error(ex.Message);
            }
            return null;
        }

        /// <summary>
        /// 获取表/视图列表(连接上下文已包含数据库/模式)
        /// </summary>
        public static List<Biao> GetTables(LianJie l)
        {
            try
            {
                if (l.DataBaseType == ORM.DbTypeEnum.SqlIte && !File.Exists(l.Host))
                {
                    Msg.Warning($"请确认数据库文件[{l.Host}]是否存在!");
                    return null;
                }
                var db = DB(l);
                return GetTablesCore(l, db.Fun.GetTableNames(), db.Fun.GetViews());
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Oops.Error(ex.Message);
            }
            return null;
        }

        /// <summary>
        /// 按模式获取表/视图(SqlServer 需自定义sql,其余库通过连接串 SearchPath 已生效)
        /// </summary>
        public static List<Biao> GetTablesBySchema(LianJie l, string schema)
        {
            if (l.DataBaseType != ORM.DbTypeEnum.SqlServer || string.IsNullOrEmpty(schema))
            {
                return GetTables(l);
            }
            var cm = schema.Replace("'", "''");
            var tableSql = $@"SELECT t.name AS name, ISNULL(ep.value, '') AS description
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
LEFT JOIN sys.extended_properties ep ON ep.major_id = t.object_id AND ep.minor_id = 0
WHERE s.name = '{cm}' ORDER BY t.name";
            var viewSql = $@"SELECT o.name AS name, m.definition AS view_sql
FROM sys.sql_modules m
INNER JOIN sys.objects o ON m.object_id = o.object_id
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE o.type = 'V' AND s.name = '{cm}' ORDER BY o.name";
            return GetTablesCore(l, tableSql, viewSql);
        }

        private static List<Biao> GetTablesCore(LianJie l, string tableSql, string viewSql)
        {
            try
            {
                var list = new List<Biao>();
                var db = DB(l);
                //查询表
                var ds = db.ExecuteSetTry(tableSql);
                if (ds != null && ds.Tables.Count > 0)
                {
                    var dt = ds.Tables[0];
                    foreach (DataRow info in dt.Rows)
                    {
                        list.Add(new Biao
                        {
                            Name = info["name"].ToString(),
                            Comment = info["description"].ToString()
                        });
                    }
                    //查询视图
                    ds = db.ExecuteSetTry(viewSql);
                    if (ds != null && ds.Tables.Count > 0)
                    {
                        dt = ds.Tables[0];
                        foreach (DataRow info in dt.Rows)
                        {
                            list.Add(new Biao
                            {
                                Name = info["name"].ToString(),
                                TableSql = info["view_sql"].ToString(),
                                IsView = true
                            });
                        }
                    }
                    return list;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Oops.Error(ex.Message);
            }
            return null;
        }

        public static List<ZiDuan> GetZiDuans(Biao b, LianJie l)
        {
            return GetZiDuans(b, l, false);
        }

        /// <summary>
        /// 获取字段清单
        /// </summary>
        /// <param name="silent">true 时不弹提示(供内部自动纠错使用)</param>
        public static List<ZiDuan> GetZiDuans(Biao b, LianJie l, bool silent)
        {
            var list = new List<ZiDuan>();
            try
            {
                var db = DB(l);
                var dal = DalFactory.CreateDal(l.DataBaseType);
                var data = dal.GetAllColumns(b.Name, db);
                foreach (var item in data)
                {
                    var ziduan = new ZiDuan
                    {
                        Name = item["columns_name"],
                        Comment = item["comments"],
                        Type = item["data_type"],
                        Notnull = item["notnull"] == "1",
                        IsPK = item["constraint_type"] == "P",
                        DefaultValue = item["default_value"]
                    };
                    list.Add(ziduan);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                if (!silent)
                {
                    Msg.Warning(ex.Message);
                }
            }
            return list;
        }

        #region 高斯未知类型兼容

        /// <summary>
        /// 高斯(openGauss)特有类型 -> 查询时可CAST成的等价已知类型
        /// 官方 OpenGauss.NET 驱动没有内置这些类型的映射,直接查询结果里出现这类列会报
        /// "which is currently unknown to OpenGauss"
        /// 键为 pg_type 类型名(format_type 返回的类型名)
        /// </summary>
        private static readonly Dictionary<string, string> GaussTypeCastMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "int1", "int" },              // tinyint
            { "tinyint", "int" },
            { "uint1", "int" },
            { "uint2", "int" },
            { "uint4", "bigint" },
            { "uint8", "numeric" },
            { "smalldatetime", "timestamp" },
            { "datetime", "timestamp" },
            { "abstime", "timestamp" },
            { "reltime", "interval" },
            { "tinterval", "text" },
            { "nvarchar2", "varchar" },
            { "nvarchar", "varchar" },
            { "clob", "text" },
            { "blob", "bytea" },
            { "raw", "bytea" },
            { "binary", "bytea" },
            { "varbinary", "bytea" }
        };

        /// <summary>
        /// 把简单的 [SELECT * FROM 单表] 改写成带字段清单的查询:
        /// 仅对驱动没有内置映射的类型做 CAST(其余字段原样保留),
        /// 这样结果集里只有那几列变成等价已知类型,其它列仍是原生强类型
        /// 无法识别或没有需要转换的字段时返回 null
        /// </summary>
        private static string BuildGaussCastSql(LianJie l, string sql)
        {
            if (l.DataBaseType != ORM.DbTypeEnum.GaussDB || string.IsNullOrWhiteSpace(sql))
            {
                return null;
            }
            var match = Regex.Match(
                sql.Trim(),
                @"^SELECT\s+\*\s+FROM\s+(?<t>[^\s;]+)(?<tail>\s*(?:LIMIT\s+\d+)?)\s*;?\s*$",
                RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }
            var raw = match.Groups["t"].Value.Trim().Trim('"', '[', ']', '`');
            var parts = raw.Split('.');
            var tableName = parts[parts.Length - 1].Trim().Trim('"', '[', ']', '`');
            var cols = GetZiDuans(new Biao { Name = tableName }, l, true);
            if (cols.Count == 0 || !cols.Any(p => GaussTypeCastMap.ContainsKey((p.Type ?? string.Empty).Trim())))
            {
                return null;
            }
            var sb = new StringBuilder("SELECT ");
            for (var i = 0; i < cols.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                var name = GetEscapeName(l, cols[i].Name);
                if (GaussTypeCastMap.TryGetValue((cols[i].Type ?? string.Empty).Trim(), out var castTo))
                {
                    sb.Append("CAST(").Append(name).Append(" AS ").Append(castTo).Append(") AS ").Append(name);
                }
                else
                {
                    sb.Append(name);
                }
            }
            sb.Append(" FROM ").Append(string.Join(".", parts.Select(p => GetEscapeName(l, p.Trim().Trim('"', '[', ']', '`')))));
            sb.Append(match.Groups["tail"].Value);
            return sb.ToString();
        }

        /// <summary>
        /// 查询失败后的高斯(openGauss)补救(使用独立连接,避免复用已损坏的连接):
        /// 1)先把简单 [SELECT * FROM 表] 改写成对未映射类型做CAST的语句重试(其余列保持强类型)
        /// 2)仍不行则退化为"未知类型按字符串读取"重试
        /// 成功时结果写入 ds
        /// </summary>
        private static bool TryGaussQueryRepair(LianJie l, ORM db, string sql, DataSet ds)
        {
            if (l.DataBaseType != ORM.DbTypeEnum.GaussDB)
            {
                return false;
            }
            var castSql = BuildGaussCastSql(l, sql);
            if (castSql != null && FillSet(db, castSql, ds))
            {
                return true;
            }
            ds.Tables.Clear();
            return FillSet(db, sql, ds, true);
        }

        /// <summary>
        /// 用独立连接执行查询并填充 DataSet(失败返回 false,不抛异常)
        /// </summary>
        private static bool FillSet(ORM db, string sql, DataSet ds, bool unknownAsText = false)
        {
            DbConnection conn = null;
            DbCommand cmd = null;
            try
            {
                conn = db.GetConnection();
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }
                cmd = db.GetCommand(sql, conn);
                if (unknownAsText)
                {
                    ORM.EnableUnknownResultTypesAsText(cmd);
                }
                var da = db.GetDataAdapter(cmd);
                da.Fill(ds, "ds");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"{sql}{Environment.NewLine}{ex.Message}");
                ds.Tables.Clear();
                return false;
            }
            finally
            {
                cmd?.Dispose();
                if (conn != null)
                {
                    if (conn.State == ConnectionState.Open)
                    {
                        conn.Close();
                    }
                    conn.Dispose();
                }
            }
        }

        #endregion

        public static string GetDDL(LianJie l)
        {
            try
            {
                switch (l.DataBaseType)
                {
                    case ORM.DbTypeEnum.SqlIte:
                        return @"CREATE TABLE IF NOT EXISTS new_table (
    id1 INTEGER,
    id2 INTEGER,
    msg TEXT,
    PRIMARY KEY (id1, id2)
);";
                    case ORM.DbTypeEnum.MySql:
                        return @"CREATE TABLE IF NOT EXISTS new_table (
    key_col1 VARCHAR(50) NOT NULL COMMENT '主键1',
    key_col2 VARCHAR(50) NOT NULL COMMENT '主键2',
    int_col1 INT(10) DEFAULT 0 COMMENT '长整型',
    int_col2 INT(1) DEFAULT 1 COMMENT '短整型',
    create_time DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    update_time DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    PRIMARY KEY (key_col1, key_col2)
) ENGINE = InnoDB 
DEFAULT CHARSET = utf8mb4 
COMMENT = '新建表';";
                    case ORM.DbTypeEnum.SqlServer:
                        return @"CREATE TABLE new_table (
    key_col1 VARCHAR(50) NOT NULL,
    key_col2 VARCHAR(50) NOT NULL,
    int_col1 INT DEFAULT 0,
    int_col2 INT DEFAULT 1,
    create_time DATETIME DEFAULT GETDATE(),
    update_time DATETIME DEFAULT GETDATE(),
    PRIMARY KEY (key_col1, key_col2)
);
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'主键1', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'new_table', @level2type = N'COLUMN', @level2name = N'key_col1';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'主键2', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'new_table', @level2type = N'COLUMN', @level2name = N'key_col2';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'长整型', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'new_table', @level2type = N'COLUMN', @level2name = N'int_col1';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'短整型', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'new_table', @level2type = N'COLUMN', @level2name = N'int_col2';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'创建时间', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'new_table', @level2type = N'COLUMN', @level2name = N'create_time';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'更新时间', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'new_table', @level2type = N'COLUMN', @level2name = N'update_time';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'新建表', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'new_table';";
                    case ORM.DbTypeEnum.Oracle:
                    case ORM.DbTypeEnum.Dameng:
                        return @"CREATE TABLE new_table (
    key_col1    VARCHAR2(50) NOT NULL,  -- Oracle 用 VARCHAR2
    key_col2    VARCHAR2(50) NOT NULL,
    int_col1   INT DEFAULT 0,
    int_col2   INT DEFAULT 1,
    create_time DATE DEFAULT SYSDATE,
    update_time DATE DEFAULT SYSDATE,
    PRIMARY KEY (key_col1, key_col2)
);
COMMENT ON COLUMN new_table.key_col1 IS '主键1';
COMMENT ON COLUMN new_table.key_col2 IS '主键2';
COMMENT ON COLUMN new_table.int_col1 IS '长整型';
COMMENT ON COLUMN new_table.int_col2 IS '短整型';
COMMENT ON COLUMN new_table.create_time IS '创建时间';
COMMENT ON COLUMN new_table.update_time IS '更新时间';
COMMENT ON TABLE new_table IS '新建表';";
                    case ORM.DbTypeEnum.Kingbase:
                    case ORM.DbTypeEnum.Pgsql:
                    case ORM.DbTypeEnum.GaussDB:
                        return @"CREATE TABLE IF NOT EXISTS new_table (
    key_col1 VARCHAR(50) NOT NULL,
    key_col2 VARCHAR(50) NOT NULL,
    int_col1 INT DEFAULT 0,
    int_col2 INT DEFAULT 1,
    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    update_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (key_col1, key_col2)
);
COMMENT ON COLUMN new_table.key_col1 IS '主键1';
COMMENT ON COLUMN new_table.key_col2 IS '主键2';
COMMENT ON COLUMN new_table.int_col1 IS '长整型';
COMMENT ON COLUMN new_table.int_col2 IS '短整型';
COMMENT ON COLUMN new_table.create_time IS '创建时间';
COMMENT ON COLUMN new_table.update_time IS '更新时间';
COMMENT ON TABLE new_table IS '新建表';";
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Msg.Warning(ex.Message);
            }
            return string.Empty;
        }

        public static string GetDDL(Biao b, LianJie l)
        {
            try
            {
                var db = DB(l);
                var dal = DalFactory.CreateDal(l.DataBaseType);
                return dal.GetDDL(b.Name, db);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Msg.Warning(ex.Message);
            }
            return string.Empty;
        }

        /// <summary>
        /// 查询表的建表语句(失败返回 null,不弹提示,便于后台批量导出)
        /// </summary>
        public static string GetDDLSilent(Biao b, LianJie l)
        {
            try
            {
                var db = DB(l);
                var dal = DalFactory.CreateDal(l.DataBaseType);
                return dal.GetDDL(b.Name, db);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            return null;
        }

        /// <summary>
        /// 查询视图的定义SQL(优先用已加载的定义,其次按库类型实时查询,失败返回 null)
        /// </summary>
        public static string GetViewDdlSilent(Biao b, LianJie l)
        {
            if (!string.IsNullOrWhiteSpace(b.TableSql))
            {
                return b.TableSql;
            }
            try
            {
                if (l.DataBaseType == ORM.DbTypeEnum.MySql)
                {
                    var ds = DB(l).ExecuteSetTry($"SHOW CREATE VIEW {GetEscapeName(l, b.Name)}");
                    if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    {
                        return ds.Tables[0].Rows[0][1].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            return GetDDLSilent(b, l);
        }

        /// <summary>
        /// 是否支持创建数据库(只有存在数据库层级的类型才支持)
        /// Oracle/达梦/Sqlite 连接下直接是表,建库没有意义,应直接使用"新建表"
        /// </summary>
        public static bool CanCreateDataBase(ORM.DbTypeEnum type)
        {
            return HasDatabaseLayer(type);
        }

        /// <summary>
        /// 可选的字符集
        /// </summary>
        public static List<string> GetCharSetList(ORM.DbTypeEnum type)
        {
            switch (type)
            {
                case ORM.DbTypeEnum.MySql:
                    return new List<string> { "utf8mb4", "utf8", "gbk", "gb2312", "latin1", "utf16", "binary" };
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    return new List<string> { "UTF8", "GBK", "GB18030", "LATIN1", "SQL_ASCII" };
                case ORM.DbTypeEnum.Dameng:
                    return new List<string> { "UTF-8", "GB18030", "GBK", "BIG5" };
                default:
                    //SqlServer 通过排序规则确定字符集
                    return new List<string>();
            }
        }

        /// <summary>
        /// 可选的排序规则
        /// </summary>
        public static List<string> GetCollateList(ORM.DbTypeEnum type)
        {
            switch (type)
            {
                case ORM.DbTypeEnum.MySql:
                    return new List<string> { "utf8mb4_general_ci", "utf8mb4_unicode_ci", "utf8mb4_bin", "utf8mb4_0900_ai_ci", "utf8_general_ci", "gbk_chinese_ci" };
                case ORM.DbTypeEnum.SqlServer:
                    return new List<string> { "Chinese_PRC_CI_AS", "Chinese_PRC_BIN", "Chinese_PRC_CS_AS", "SQL_Latin1_General_CP1_CI_AS", "Latin1_General_CI_AS" };
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    return new List<string> { "C", "C.UTF-8", "en_US.UTF-8", "zh_CN.UTF-8", "default" };
                default:
                    return new List<string>();
            }
        }

        /// <summary>
        /// 生成创建数据库的SQL(不支持的类型返回 null)
        /// </summary>
        public static string GetCreateDataBaseSql(LianJie l, string dataBaseName, string charSet = null, string collate = null, string owner = null)
        {
            var name = GetEscapeName(l, dataBaseName);
            var cs = string.IsNullOrWhiteSpace(charSet) ? null : charSet.Trim();
            var cl = string.IsNullOrWhiteSpace(collate) ? null : collate.Trim();
            var ow = string.IsNullOrWhiteSpace(owner) ? null : owner.Trim();
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.MySql:
                    var sbMy = new StringBuilder($"CREATE DATABASE {name}");
                    if (cs != null)
                    {
                        sbMy.Append($" DEFAULT CHARACTER SET {cs}");
                    }
                    if (cl != null)
                    {
                        sbMy.Append($" COLLATE {cl}");
                    }
                    return sbMy.Append(";").ToString();
                case ORM.DbTypeEnum.SqlServer:
                    var sbMs = new StringBuilder($"CREATE DATABASE {name}");
                    if (cl != null)
                    {
                        sbMs.Append($" COLLATE {cl}");
                    }
                    return sbMs.Append(";").ToString();
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    var sbPg = new StringBuilder($"CREATE DATABASE {name}");
                    var opts = new List<string>();
                    if (ow != null)
                    {
                        opts.Add($"OWNER = {ow}");
                    }
                    if (cs != null)
                    {
                        opts.Add($"ENCODING = '{cs}'");
                    }
                    if (cl != null)
                    {
                        opts.Add($"LC_COLLATE = '{cl}'");
                        opts.Add($"LC_CTYPE = '{cl}'");
                    }
                    //指定了编码/排序规则时必须使用 template0
                    if (cs != null || cl != null)
                    {
                        opts.Add("TEMPLATE = template0");
                    }
                    if (opts.Count > 0)
                    {
                        sbPg.Append(" WITH ").Append(string.Join(" ", opts));
                    }
                    return sbPg.Append(";").ToString();
                case ORM.DbTypeEnum.Dameng:
                    //达梦以模式作为库的单位
                    var sbDm = new StringBuilder($"CREATE SCHEMA {name}");
                    if (ow != null)
                    {
                        sbDm.Append($" AUTHORIZATION {ow}");
                    }
                    return sbDm.Append(";").ToString();
                default:
                    return null;
            }
        }

        /// <summary>
        /// 在服务器上创建数据库
        /// </summary>
        public static bool CreateDataBase(LianJie l, string dataBaseName, out string error, string charSet = null, string collate = null, string owner = null)
        {
            error = null;
            var sql = GetCreateDataBaseSql(l, dataBaseName, charSet, collate, owner);
            if (sql == null)
            {
                error = $"{l.DataBaseType} 暂不支持创建数据库";
                return false;
            }
            var db = DB(l);
            var conn = db.GetConnection();
            try
            {
                conn.Open();
                _ = db.GetCommand(sql, conn).ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                error = ex.Message;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
            return false;
        }

        /// <summary>
        /// 是否支持新建模式(只有存在模式层级的类型才支持:SqlServer/Pg系列)
        /// </summary>
        public static bool CanCreateSchema(ORM.DbTypeEnum type)
        {
            return HasSchemaLayer(type);
        }

        /// <summary>
        /// 生成新建模式的SQL(不支持的类型返回 null)
        /// </summary>
        public static string GetCreateSchemaSql(LianJie l, string schemaName)
        {
            var name = GetEscapeName(l, schemaName);
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.SqlServer:
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    return $"CREATE SCHEMA {name};";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 在当前数据库下新建模式(l 为所在库的连接上下文)
        /// </summary>
        public static bool CreateSchema(LianJie l, string schemaName, out string error)
        {
            error = null;
            var sql = GetCreateSchemaSql(l, schemaName);
            if (sql == null)
            {
                error = $"{l.DataBaseType} 暂不支持新建模式";
                return false;
            }
            var db = DB(l);
            var conn = db.GetConnection();
            try
            {
                conn.Open();
                _ = db.GetCommand(sql, conn).ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                error = ex.Message;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
            return false;
        }

        /// <summary>
        /// 生成删除数据库的SQL(不支持的类型返回 null)
        /// </summary>
        public static string GetDropDataBaseSql(LianJie l, string dataBaseName)
        {
            var name = GetEscapeName(l, dataBaseName);
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.MySql:
                case ORM.DbTypeEnum.SqlServer:
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    return $"DROP DATABASE {name};";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 在服务器上删除数据库(l 为服务器级连接上下文)
        /// </summary>
        public static bool DropDataBase(LianJie l, string dataBaseName, out string error)
        {
            error = null;
            var sql = GetDropDataBaseSql(l, dataBaseName);
            if (sql == null)
            {
                error = $"{l.DataBaseType} 暂不支持删除数据库";
                return false;
            }
            //Pg/SqlServer 删不掉连接正连着的库:目标库恰好是连接库时,换到其它可用的库上执行
            var ctx = l;
            if (string.Equals(l.DataBaseName, dataBaseName, StringComparison.OrdinalIgnoreCase))
            {
                switch (l.DataBaseType)
                {
                    case ORM.DbTypeEnum.SqlServer:
                        ctx = CreateContext(l, "master");
                        break;
                    case ORM.DbTypeEnum.Pgsql:
                    case ORM.DbTypeEnum.Kingbase:
                    case ORM.DbTypeEnum.GaussDB:
                        var altDb = GetDataBases(l)?
                            .FirstOrDefault(n => !string.Equals(n, dataBaseName, StringComparison.OrdinalIgnoreCase));
                        if (altDb != null)
                        {
                            ctx = CreateContext(l, altDb);
                        }
                        break;
                }
            }
            var db = DB(ctx);
            var conn = db.GetConnection();
            try
            {
                conn.Open();
                _ = db.GetCommand(sql, conn).ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                error = ex.Message;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
            return false;
        }

        /// <summary>
        /// 生成删除模式(schema)的SQL(不支持的类型返回 null)
        /// </summary>
        public static string GetDropSchemaSql(LianJie l, string schemaName)
        {
            var name = GetEscapeName(l, schemaName);
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.SqlServer:
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    return $"DROP SCHEMA {name};";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 删除指定数据库下的模式(l 为该库的连接上下文,模式内还有对象时保留原生报错)
        /// </summary>
        public static bool DropSchema(LianJie l, string schemaName, out string error)
        {
            error = null;
            var sql = GetDropSchemaSql(l, schemaName);
            if (sql == null)
            {
                error = $"{l.DataBaseType} 暂不支持删除模式";
                return false;
            }
            var db = DB(l);
            var conn = db.GetConnection();
            try
            {
                conn.Open();
                _ = db.GetCommand(sql, conn).ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                error = ex.Message;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
            return false;
        }

        /// <summary>
        /// 生成新建表的DDL(基于内置模板并替换表名)
        /// </summary>
        public static string GetCreateTableSql(LianJie l, string tableName)
        {
            var ddl = GetDDL(l);
            if (string.IsNullOrEmpty(ddl))
            {
                return null;
            }
            var name = GetEscapeName(l, tableName);
            ddl = ddl.Replace("new_table", name);
            //SqlServer 需要显式指定模式
            if (l.DataBaseType == ORM.DbTypeEnum.SqlServer && !string.IsNullOrEmpty(l.ServiceName))
            {
                var schema = GetEscapeName(l, l.ServiceName);
                ddl = ddl.Replace($"CREATE TABLE {name}", $"CREATE TABLE {schema}.{name}");
                ddl = ddl.Replace("N'dbo'", $"N'{l.ServiceName}'");
            }
            return ddl;
        }

        /// <summary>
        /// 新建表(执行模板DDL)
        /// </summary>
        public static bool CreateTable(LianJie l, string tableName)
        {
            var sql = GetCreateTableSql(l, tableName);
            if (sql == null)
            {
                Msg.Warning($"{l.DataBaseType} 暂不支持新建表");
                return false;
            }
            _ = GetDataTable(l, sql, out _, out bool isErr);
            return !isErr;
        }

        #region 表结构编辑(可视化建表/改表)

        /// <summary>
        /// 是否 Pg 系列(Pgsql/Kingbase/GaussDB)
        /// </summary>
        private static bool IsPgLike(ORM.DbTypeEnum type)
        {
            return type == ORM.DbTypeEnum.Pgsql || type == ORM.DbTypeEnum.Kingbase || type == ORM.DbTypeEnum.GaussDB;
        }

        /// <summary>
        /// 各库的常用字段类型(供下拉选择)
        /// </summary>
        public static List<string> GetCommonTypes(ORM.DbTypeEnum type)
        {
            switch (type)
            {
                case ORM.DbTypeEnum.MySql:
                    return new List<string> { "int", "bigint", "tinyint", "varchar(50)", "varchar(255)", "text", "longtext", "decimal(10,2)", "datetime", "date", "timestamp" };
                case ORM.DbTypeEnum.SqlServer:
                    return new List<string> { "int", "bigint", "bit", "nvarchar(50)", "nvarchar(255)", "nvarchar(max)", "decimal(18,2)", "datetime", "date", "uniqueidentifier" };
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    return new List<string> { "int4", "int8", "varchar(50)", "varchar(255)", "text", "numeric(10,2)", "timestamp", "date", "bool", "jsonb" };
                case ORM.DbTypeEnum.Oracle:
                case ORM.DbTypeEnum.Dameng:
                    return new List<string> { "NUMBER(10)", "NUMBER(19)", "VARCHAR2(50)", "VARCHAR2(255)", "CLOB", "DATE", "TIMESTAMP", "CHAR(1)" };
                case ORM.DbTypeEnum.SqlIte:
                    return new List<string> { "INTEGER", "TEXT", "REAL", "BLOB", "NUMERIC" };
                default:
                    return new List<string> { "int", "varchar(50)", "text", "datetime" };
            }
        }

        /// <summary>
        /// 格式化默认值(数字与函数不加引号)
        /// </summary>
        private static string FormatDefault(ORM.DbTypeEnum type, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            var v = value.Trim();
            if (v.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                return "NULL";
            }
            if (v.Length > 1 && v.StartsWith("'") && v.EndsWith("'"))
            {
                return v;
            }
            if (double.TryParse(v, out _))
            {
                return v;
            }
            var funcs = new[] { "CURRENT_TIMESTAMP", "CURRENT_DATE", "CURRENT_TIME", "NOW()", "GETDATE()", "SYSDATE", "LOCALTIMESTAMP", "NEWID()" };
            foreach (var f in funcs)
            {
                if (v.Equals(f, StringComparison.OrdinalIgnoreCase))
                {
                    return v;
                }
            }
            return "'" + v.Replace("'", "''") + "'";
        }

        /// <summary>
        /// SqlServer 模式名(未配置时用 dbo)
        /// </summary>
        private static string GetSchema(LianJie l)
        {
            return string.IsNullOrEmpty(l.ServiceName) ? "dbo" : l.ServiceName;
        }

        /// <summary>
        /// 列定义片段(CREATE/ADD/MODIFY 通用)
        /// </summary>
        private static string GetColumnDefine(LianJie l, ZiDuan z, bool withComment)
        {
            var name = GetEscapeName(l, z.Name);
            var colType = string.IsNullOrWhiteSpace(z.Type) ? "varchar(50)" : z.Type.Trim();
            var sb = new StringBuilder();
            sb.Append(name).Append(' ').Append(colType);
            if (l.DataBaseType == ORM.DbTypeEnum.Oracle || l.DataBaseType == ORM.DbTypeEnum.Dameng)
            {
                if (z.Notnull)
                {
                    sb.Append(" NOT NULL");
                }
            }
            else
            {
                sb.Append(z.Notnull ? " NOT NULL" : " NULL");
            }
            var def = FormatDefault(l.DataBaseType, z.DefaultValue);
            if (def != null)
            {
                sb.Append(" DEFAULT ").Append(def);
            }
            if (withComment && l.DataBaseType == ORM.DbTypeEnum.MySql && !string.IsNullOrEmpty(z.Comment))
            {
                sb.Append(" COMMENT '").Append(z.Comment.Replace("'", "''")).Append("'");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 生成新建表的SQL(含表/字段注释)
        /// </summary>
        public static List<SqlItem> GetCreateTableSqlList(LianJie l, string tableName, string tableComment, List<ZiDuan> cols)
        {
            var list = new List<SqlItem>();
            var t = GetEscapeName(l, tableName);
            //主键列必须非空(可空列无法建成主键),按副本补齐,不改动界面上的输入
            cols = CoreConvert.DeepClone(cols ?? new List<ZiDuan>());
            foreach (var c in cols.Where(p => p.IsPK))
            {
                c.Notnull = true;
            }
            var sb = new StringBuilder();
            sb.Append("CREATE TABLE ").Append(t).Append(" (");
            for (var i = 0; i < cols.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(GetColumnDefine(l, cols[i], true));
            }
            var pks = cols.Where(p => p.IsPK).ToList();
            if (pks.Count > 0)
            {
                sb.Append(", PRIMARY KEY (").Append(string.Join(", ", pks.Select(p => GetEscapeName(l, p.Name)))).Append(")");
            }
            sb.Append(")");
            if (l.DataBaseType == ORM.DbTypeEnum.MySql && !string.IsNullOrEmpty(tableComment))
            {
                sb.Append(" COMMENT = '").Append(tableComment.Replace("'", "''")).Append("'");
            }
            sb.Append(";");
            list.Add(new SqlItem { Sql = sb.ToString() });

            //非 MySQL 的注释需要单独语句
            if (l.DataBaseType != ORM.DbTypeEnum.MySql)
            {
                list.AddRange(GetTableCommentSql(l, tableName, tableComment));
                foreach (var c in cols)
                {
                    if (!string.IsNullOrEmpty(c.Comment))
                    {
                        list.AddRange(GetColumnCommentSql(l, tableName, c.Name, c.Comment));
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 生成修改表结构的SQL(对比新旧字段)
        /// </summary>
        public static List<SqlItem> GetAlterTableSqlList(LianJie l, string tableName, List<ZiDuan> oldCols, List<ZiDuan> newCols, string oldComment, string newComment)
        {
            var list = new List<SqlItem>();
            var t = GetEscapeName(l, tableName);
            var oldList = oldCols ?? new List<ZiDuan>();
            //主键列必须非空(可空列无法建成主键),按副本补齐,不改动界面上的输入
            var newList = CoreConvert.DeepClone(newCols ?? new List<ZiDuan>());
            foreach (var n in newList.Where(p => p.IsPK))
            {
                n.Notnull = true;
            }

            //删除的字段(新集合中找不到原始名)
            foreach (var o in oldList)
            {
                if (newList.All(p => p.OldName != o.Name))
                {
                    list.Add(new SqlItem { Sql = $"ALTER TABLE {t} DROP COLUMN {GetEscapeName(l, o.Name)};" });
                }
            }
            //新增的字段(没有原始名)
            foreach (var n in newList)
            {
                if (string.IsNullOrEmpty(n.OldName))
                {
                    list.AddRange(GetAddColumnSql(l, tableName, n));
                }
            }
            //已存在的字段
            foreach (var n in newList)
            {
                if (string.IsNullOrEmpty(n.OldName))
                {
                    continue;
                }
                var o = oldList.FirstOrDefault(p => p.Name == n.OldName);
                if (o == null || SameColumn(o, n))
                {
                    continue;
                }
                list.AddRange(GetModifyColumnSql(l, tableName, o, n));
            }
            //表注释
            if (!string.Equals(oldComment ?? "", newComment ?? ""))
            {
                list.AddRange(GetTableCommentSql(l, tableName, newComment));
            }
            //主键变更(新增/删除/更换)
            var oldPk = oldList.Where(p => p.IsPK).Select(p => p.Name).ToList();
            var newPk = newList.Where(p => p.IsPK).Select(p => p.Name).ToList();
            if (!oldPk.SequenceEqual(newPk))
            {
                list.AddRange(GetPrimaryKeyChangeSql(l, tableName, oldPk, newPk));
            }
            return list;
        }

        /// <summary>
        /// 主键变更SQL(新增/删除/更换主键)
        /// </summary>
        private static List<SqlItem> GetPrimaryKeyChangeSql(LianJie l, string tableName, List<string> oldPk, List<string> newPk)
        {
            var list = new List<SqlItem>();
            var t = GetEscapeName(l, tableName);
            var pkCols = string.Join(", ", newPk.Select(p => GetEscapeName(l, p)));
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.MySql:
                    //删旧主键与加新主键合并成一条,避免主键列带自增时先删后加之间报错
                    var actions = new List<string>();
                    if (oldPk.Count > 0)
                    {
                        actions.Add("DROP PRIMARY KEY");
                    }
                    if (newPk.Count > 0)
                    {
                        actions.Add($"ADD PRIMARY KEY ({pkCols})");
                    }
                    list.Add(new SqlItem { Sql = $"ALTER TABLE {t} {string.Join(", ", actions)};" });
                    break;

                case ORM.DbTypeEnum.SqlServer:
                    if (oldPk.Count > 0)
                    {
                        //主键约束名未知,动态查询后删除
                        list.Add(new SqlItem
                        {
                            Sql = $"DECLARE @pk sysname = (SELECT name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('{GetSchema(l)}.{tableName.Replace("'", "''")}') AND type = 'PK'); IF @pk IS NOT NULL EXEC('ALTER TABLE {t} DROP CONSTRAINT [' + @pk + ']');"
                        });
                    }
                    if (newPk.Count > 0)
                    {
                        list.Add(new SqlItem { Sql = $"ALTER TABLE {t} ADD CONSTRAINT {GetEscapeName(l, "PK_" + tableName)} PRIMARY KEY ({pkCols});" });
                    }
                    break;

                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    if (oldPk.Count > 0)
                    {
                        //主键约束名未知,动态查询后删除
                        list.Add(new SqlItem
                        {
                            Sql = $"DO $$ DECLARE c text; BEGIN SELECT conname INTO c FROM pg_constraint WHERE conrelid = '{tableName.Replace("'", "''")}'::regclass AND contype = 'p'; IF c IS NOT NULL THEN EXECUTE 'ALTER TABLE {t} DROP CONSTRAINT ' || quote_ident(c); END IF; END $$;"
                        });
                    }
                    if (newPk.Count > 0)
                    {
                        list.Add(new SqlItem { Sql = $"ALTER TABLE {t} ADD PRIMARY KEY ({pkCols});" });
                    }
                    break;

                case ORM.DbTypeEnum.Oracle:
                case ORM.DbTypeEnum.Dameng:
                    if (oldPk.Count > 0)
                    {
                        list.Add(new SqlItem { Sql = $"ALTER TABLE {t} DROP PRIMARY KEY;" });
                    }
                    if (newPk.Count > 0)
                    {
                        list.Add(new SqlItem { Sql = $"ALTER TABLE {t} ADD PRIMARY KEY ({pkCols});" });
                    }
                    break;

                default:
                    //SqlIte 等不支持修改主键
                    Msg.Warning($"{l.DataBaseType} 不支持修改主键,请手工调整");
                    break;
            }
            return list;
        }

        private static bool SameColumn(ZiDuan a, ZiDuan b)
        {
            return string.Equals(a.Name, b.Name)
                   && string.Equals((a.Type ?? "").Trim(), (b.Type ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                   && a.Notnull == b.Notnull
                   && string.Equals((a.DefaultValue ?? "").Trim(), (b.DefaultValue ?? "").Trim())
                   && string.Equals(a.Comment ?? "", b.Comment ?? "");
        }

        private static List<SqlItem> GetAddColumnSql(LianJie l, string tableName, ZiDuan z)
        {
            var list = new List<SqlItem>();
            var t = GetEscapeName(l, tableName);
            var col = GetColumnDefine(l, z, true);
            var sql = IsPgLike(l.DataBaseType)
                ? $"ALTER TABLE {t} ADD COLUMN {col};"
                : $"ALTER TABLE {t} ADD {col};";
            list.Add(new SqlItem { Sql = sql });
            if (l.DataBaseType != ORM.DbTypeEnum.MySql)
            {
                list.AddRange(GetColumnCommentSql(l, tableName, z.Name, z.Comment));
            }
            return list;
        }

        private static List<SqlItem> GetModifyColumnSql(LianJie l, string tableName, ZiDuan old, ZiDuan now)
        {
            var list = new List<SqlItem>();
            var t = GetEscapeName(l, tableName);
            var renamed = !string.Equals(old.Name, now.Name);
            var type = string.IsNullOrWhiteSpace(now.Type) ? "varchar(50)" : now.Type.Trim();
            var def = FormatDefault(l.DataBaseType, now.DefaultValue);
            var newName = GetEscapeName(l, now.Name);

            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.MySql:
                    //CHANGE COLUMN 可同时完成改名与改属性
                    list.Add(new SqlItem
                    {
                        Sql = $"ALTER TABLE {t} CHANGE COLUMN {GetEscapeName(l, old.Name)} {GetColumnDefine(l, now, true)};"
                    });
                    break;

                case ORM.DbTypeEnum.SqlServer:
                    if (renamed)
                    {
                        list.Add(new SqlItem { Sql = $"EXEC sp_rename '{tableName}.{old.Name}', '{now.Name}', 'COLUMN';" });
                    }
                    list.Add(new SqlItem
                    {
                        Sql = $"ALTER TABLE {t} ALTER COLUMN {newName} {type}{(now.Notnull ? " NOT NULL" : " NULL")};"
                    });
                    if (!string.Equals((old.DefaultValue ?? "").Trim(), (now.DefaultValue ?? "").Trim()) && def != null)
                    {
                        var dfName = $"DF_{tableName}_{now.Name}";
                        list.Add(new SqlItem
                        {
                            Sql = $"ALTER TABLE {t} ADD CONSTRAINT {GetEscapeName(l, dfName)} DEFAULT {def} FOR {newName};",
                            IgnoreError = true
                        });
                    }
                    list.AddRange(GetColumnCommentSql(l, tableName, now.Name, now.Comment));
                    break;

                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                    if (renamed)
                    {
                        list.Add(new SqlItem { Sql = $"ALTER TABLE {t} RENAME COLUMN {GetEscapeName(l, old.Name)} TO {newName};" });
                    }
                    if (!string.Equals((old.Type ?? "").Trim(), type, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(new SqlItem { Sql = $"ALTER TABLE {t} ALTER COLUMN {newName} TYPE {type};" });
                    }
                    list.Add(new SqlItem { Sql = $"ALTER TABLE {t} ALTER COLUMN {newName} {(now.Notnull ? "SET" : "DROP")} NOT NULL;" });
                    if (def != null)
                    {
                        list.Add(new SqlItem { Sql = $"ALTER TABLE {t} ALTER COLUMN {newName} SET DEFAULT {def};" });
                    }
                    else if (!string.IsNullOrWhiteSpace(old.DefaultValue))
                    {
                        list.Add(new SqlItem { Sql = $"ALTER TABLE {t} ALTER COLUMN {newName} DROP DEFAULT;" });
                    }
                    list.AddRange(GetColumnCommentSql(l, tableName, now.Name, now.Comment));
                    break;

                case ORM.DbTypeEnum.Oracle:
                case ORM.DbTypeEnum.Dameng:
                    if (renamed)
                    {
                        list.Add(new SqlItem { Sql = $"ALTER TABLE {t} RENAME COLUMN {GetEscapeName(l, old.Name)} TO {newName};" });
                    }
                    var sb = new StringBuilder("ALTER TABLE ").Append(t).Append(" MODIFY (").Append(newName).Append(' ').Append(type);
                    if (now.Notnull)
                    {
                        sb.Append(" NOT NULL");
                    }
                    if (def != null)
                    {
                        sb.Append(" DEFAULT ").Append(def);
                    }
                    sb.Append(");");
                    list.Add(new SqlItem { Sql = sb.ToString() });
                    list.AddRange(GetColumnCommentSql(l, tableName, now.Name, now.Comment));
                    break;

                default:
                    Msg.Warning($"{l.DataBaseType} 不支持修改字段,请重建表");
                    break;
            }
            return list;
        }

        /// <summary>
        /// 表注释SQL
        /// </summary>
        private static List<SqlItem> GetTableCommentSql(LianJie l, string tableName, string comment)
        {
            var list = new List<SqlItem>();
            if (string.IsNullOrEmpty(comment))
            {
                return list;
            }
            var t = GetEscapeName(l, tableName);
            var cm = comment.Replace("'", "''");
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.MySql:
                    list.Add(new SqlItem { Sql = $"ALTER TABLE {t} COMMENT = '{cm}';" });
                    break;
                case ORM.DbTypeEnum.SqlServer:
                    var s = GetSchema(l);
                    list.Add(new SqlItem
                    {
                        Sql = $"EXEC sp_dropextendedproperty @name = N'MS_Description', @level0type = N'SCHEMA', @level0name = N'{s}', @level1type = N'TABLE', @level1name = N'{tableName}';",
                        IgnoreError = true
                    });
                    list.Add(new SqlItem
                    {
                        Sql = $"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{cm}', @level0type = N'SCHEMA', @level0name = N'{s}', @level1type = N'TABLE', @level1name = N'{tableName}';"
                    });
                    break;
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                case ORM.DbTypeEnum.Oracle:
                case ORM.DbTypeEnum.Dameng:
                    list.Add(new SqlItem { Sql = $"COMMENT ON TABLE {t} IS '{cm}';" });
                    break;
            }
            return list;
        }

        /// <summary>
        /// 字段注释SQL
        /// </summary>
        private static List<SqlItem> GetColumnCommentSql(LianJie l, string tableName, string columnName, string comment)
        {
            var list = new List<SqlItem>();
            if (string.IsNullOrEmpty(comment))
            {
                return list;
            }
            var t = GetEscapeName(l, tableName);
            var c = GetEscapeName(l, columnName);
            var cm = comment.Replace("'", "''");
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.MySql:
                    //MySQL 注释随列定义一起修改
                    break;
                case ORM.DbTypeEnum.SqlServer:
                    var s = GetSchema(l);
                    list.Add(new SqlItem
                    {
                        Sql = $"EXEC sp_dropextendedproperty @name = N'MS_Description', @level0type = N'SCHEMA', @level0name = N'{s}', @level1type = N'TABLE', @level1name = N'{tableName}', @level2type = N'COLUMN', @level2name = N'{columnName}';",
                        IgnoreError = true
                    });
                    list.Add(new SqlItem
                    {
                        Sql = $"EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'{cm}', @level0type = N'SCHEMA', @level0name = N'{s}', @level1type = N'TABLE', @level1name = N'{tableName}', @level2type = N'COLUMN', @level2name = N'{columnName}';"
                    });
                    break;
                case ORM.DbTypeEnum.Pgsql:
                case ORM.DbTypeEnum.Kingbase:
                case ORM.DbTypeEnum.GaussDB:
                case ORM.DbTypeEnum.Oracle:
                case ORM.DbTypeEnum.Dameng:
                    list.Add(new SqlItem { Sql = $"COMMENT ON COLUMN {t}.{c} IS '{cm}';" });
                    break;
            }
            return list;
        }

        /// <summary>
        /// 逐条执行SQL(不按分号拆分,便于支持含分号的存储过程调用)
        /// 该方法不弹提示(可在后台线程调用),失败原因通过 error 返回,由调用方在UI线程提示
        /// </summary>
        public static bool ExecuteSqlList(LianJie l, List<SqlItem> sqlList, out string error)
        {
            error = null;
            if (sqlList == null || sqlList.Count == 0)
            {
                error = "没有需要执行的语句";
                return false;
            }
            var db = DB(l);
            var conn = db.GetConnection();
            var info = $"[{l.Host}] {l.Name}{Environment.NewLine}[{l.DataBaseType}] {l.DataBaseName}";
            try
            {
                conn.Open();
                foreach (var item in sqlList)
                {
                    var st = DateTime.Now;
                    try
                    {
                        _ = db.GetCommand(item.Sql, conn).ExecuteNonQuery();
                        Config.AddSqlLog(info, st, item.Sql, 0, (DateTime.Now - st).TotalMilliseconds, true);
                    }
                    catch (Exception ex)
                    {
                        Config.AddSqlLog(info, st, item.Sql, 0, (DateTime.Now - st).TotalMilliseconds, false);
                        Log.Error($"{ex.Message}{Environment.NewLine}{item.Sql}");
                        if (!item.IgnoreError)
                        {
                            error = $"执行失败:{Environment.NewLine}{item.Sql}{Environment.NewLine}{ex.Message}";
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
            return true;
        }

        #endregion

        /// <summary>
        /// 获取前100条数据SQL
        /// </summary>
        /// <returns></returns>
        public static string GetData100(LianJie l, string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return string.Empty;
            }
            tableName = GetEscapeName(l, tableName);
            if (l.DataBaseType == ORM.DbTypeEnum.SqlServer)
            {
                return $"SELECT TOP 100 * FROM {tableName};";
            }
            if (l.DataBaseType == ORM.DbTypeEnum.Oracle)
            {
                return $"SELECT * FROM {tableName} WHERE ROWNUM <= 100;";
            }
            return $"SELECT * FROM {tableName} LIMIT 100;";
        }

        /// <summary>
        /// 拼接分页查询SQL(按数据库类型,offset 从0开始)
        /// </summary>
        /// <param name="l">连接</param>
        /// <param name="body">不含排序与行数限制的查询主体(SELECT ... FROM ... [WHERE ...])</param>
        /// <param name="orderBy">排序子句(形如 ORDER BY xxx,可为空)</param>
        /// <param name="offset">起始行(从0开始)</param>
        /// <param name="fetch">每页条数</param>
        public static string BuildPageSql(LianJie l, string body, string orderBy, int offset, int fetch)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }
            if (offset < 0)
            {
                offset = 0;
            }
            if (fetch <= 0)
            {
                fetch = 100;
            }
            var end = offset + fetch;
            var order = string.IsNullOrWhiteSpace(orderBy) ? string.Empty : " " + orderBy;
            switch (l.DataBaseType)
            {
                case ORM.DbTypeEnum.Oracle:
                case ORM.DbTypeEnum.Dameng:
                    //老版本 Oracle/达梦没有 OFFSET:用两层 ROWNUM 嵌套分页(会多出一个 rownum_ 辅助列,查询后由调用方移除)
                    return $"SELECT * FROM (SELECT row_limit.*, ROWNUM rownum_ FROM ({body}{order}) row_limit WHERE ROWNUM <= {end}) WHERE rownum_ > {offset};";
                case ORM.DbTypeEnum.SqlServer:
                    //ROW_NUMBER 必须带 ORDER BY,没有排序时用常量排序,避免生成非法SQL
                    var msOrder = string.IsNullOrWhiteSpace(orderBy) ? "(ORDER BY (SELECT 1))" : orderBy;
                    return $"SELECT * FROM (SELECT ROW_NUMBER() OVER({msOrder}) AS rowNumber, t.* FROM ({body}) t) tempTable WHERE rowNumber BETWEEN {offset + 1} AND {end};";
                case ORM.DbTypeEnum.MySql:
                    return $"{body}{order} LIMIT {offset},{fetch};";
                default:
                    //SqlIte/Pgsql/人大金仓/高斯等:limit offset
                    return $"{body}{order} LIMIT {fetch} OFFSET {offset};";
            }
        }

        /// <summary>
        /// 判断能否对这条SQL自动分页(查询页用):必须是单条 SELECT,且没有自己写行数限制。
        /// 能分页时给出分页所需的查询主体与排序子句(ORDER BY 要单独摘出来,分页时拼在最后)
        /// </summary>
        /// <param name="sql">原始SQL(可带结尾分号、注释)</param>
        /// <param name="body">不含排序与行数限制的查询主体</param>
        /// <param name="orderBy">排序子句(可为空)</param>
        public static bool TrySplitForPage(string sql, out string body, out string orderBy)
        {
            body = null;
            orderBy = null;
            if (string.IsNullOrWhiteSpace(sql))
            {
                return false;
            }
            //压缩会去掉注释并合并空白,与真正执行时用的是同一份处理逻辑
            var text = sql.CompressToString().Trim().TrimEnd(';').Trim();
            if (text.Length == 0 || !text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            //多语句或自己带了行数限制的:按原样执行,套上分页会生成非法SQL
            if (text.IndexOf(';') >= 0 || HasRowLimit(text))
            {
                return false;
            }
            var index = LastTopLevelOrderBy(text);
            if (index > 0)
            {
                body = text.Substring(0, index).Trim();
                orderBy = text.Substring(index).Trim();
            }
            else
            {
                body = text;
            }
            return body.Length > 0;
        }

        /// <summary>
        /// 是否已自带行数限制(LIMIT/OFFSET/FETCH/ROWNUM/TOP)
        /// </summary>
        private static bool HasRowLimit(string text)
        {
            if (Regex.IsMatch(text, @"\b(limit|offset|fetch|rownum)\b", RegexOptions.IgnoreCase))
            {
                return true;
            }
            return Regex.IsMatch(text, @"^SELECT\s+(?:DISTINCT\s+)?TOP\b", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 找最外层最后一个 ORDER BY 的位置(括号内与字符串内不算),找不到返回 -1
        /// </summary>
        private static int LastTopLevelOrderBy(string text)
        {
            var index = -1;
            var depth = 0;
            var quote = '\0';
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                    }
                    continue;
                }
                if (c == '\'' || c == '"' || c == '`')
                {
                    quote = c;
                    continue;
                }
                if (c == '[')
                {
                    quote = ']';
                    continue;
                }
                if (c == '(')
                {
                    depth++;
                    continue;
                }
                if (c == ')')
                {
                    depth--;
                    continue;
                }
                if (depth == 0 && (c == 'o' || c == 'O')
                    && Regex.IsMatch(text.Substring(i), @"^ORDER\s+BY\b", RegexOptions.IgnoreCase))
                {
                    index = i;
                }
            }
            return index;
        }

        /// <summary>
        /// 移除分页SQL带出来的辅助列(Oracle/达梦的 rownum_、SqlServer 的 rowNumber)
        /// </summary>
        public static void RemovePageHelperColumns(DataTable dt)
        {
            if (dt == null)
            {
                return;
            }
            foreach (var name in new[] { "ROWNUM_", "rowNumber" })
            {
                var col = dt.Columns.Cast<DataColumn>()
                    .FirstOrDefault(p => string.Equals(p.ColumnName, name, StringComparison.OrdinalIgnoreCase));
                if (col != null)
                {
                    dt.Columns.Remove(col);
                }
            }
        }

        /// <summary>
        /// 查询数据
        /// </summary>
        /// <param name="l"></param>
        /// <param name="sql"></param>
        /// <param name="isRet"></param>
        /// <param name="isErr"></param>
        /// <param name="silent">静默执行:不弹"执行成功,查到数据行数"提示(翻页等高频查询用)</param>
        /// <returns></returns>
        public static DataSet GetDataTable(LianJie l, string sql, out bool isRet, out bool isErr, bool silent = false)
        {
            var dataSet = new DataSet();

            isErr = false;
            isRet = true;
            var db = DB(l);
            var conn = db.GetConnection();
            try
            {
                conn.Open();
                int i = 1;
                foreach (var s in sql.Split(';'))
                {
                    var dt = new DataTable();
                    var es = s.CompressToString();
                    if (!string.IsNullOrEmpty(es))
                    {
                        var count = 0;
                        var st = DateTime.Now;
                        var info = $"[{l.Host}] {l.Name}{Environment.NewLine}[{l.DataBaseType}] {l.DataBaseName}";
                        try
                        {
                            if (es.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || es.StartsWith("SHOW", StringComparison.OrdinalIgnoreCase))
                            {
                                //pg时区问题
                                if (db.MDbType == ORM.DbTypeEnum.Pgsql)
                                {
                                    _ = db.GetCommand("SET TIME ZONE 'Asia/Shanghai';", conn).ExecuteNonQuery();
                                }
                                var cmd = db.GetCommand(es, conn);
                                var da = db.GetDataAdapter(cmd);

                                var ds = new DataSet();
                                try
                                {
                                    da.Fill(ds, "ds");
                                    cmd.Parameters.Clear();
                                }
                                catch (Exception ex)
                                {
                                    //高斯(openGauss)存在官方驱动没有内置映射的类型(如 tinyint 的 int1):
                                    //先只对这类字段做 CAST 重查(其余列保持原生强类型),
                                    //改写不可用或仍失败时再退化为"未知类型按字符串读取",非高斯库维持原有报错
                                    Log.Error($"{es}{Environment.NewLine}{ex.Message}");
                                    ds = new DataSet();
                                    if (!TryGaussQueryRepair(l, db, es, ds))
                                    {
                                        throw new Exception($"执行查询语句{Environment.NewLine}{ex.Message}");
                                    }
                                }
                                finally
                                {
                                    cmd.Dispose();
                                }
                                if (ds != null && ds.Tables.Count > 0)
                                {
                                    var tableName = GetFirstTableNameFromSql(es);
                                    dt = ds.Tables[0];
                                    count = dt.Rows.Count;
                                    dt.TableName = tableName + $" ({i++})";
                                }
                                DataTable newTable = dt.Copy();
                                dataSet.Tables.Add(newTable);
                                if (!silent)
                                {
                                    Oops.Ok("执行成功,查到数据行数: " + count);
                                }
                            }
                            else
                            {
                                count = db.ExecuteSqlParam(es);
                                if (!silent)
                                {
                                    Oops.Ok("执行成功,受影响行数: " + count);
                                }
                                isRet = false;
                            }
                            var et = DateTime.Now;
                            Config.AddSqlLog(info, st, es, count, (et - st).TotalMilliseconds, true);
                        }
                        catch (Exception ex)
                        {
                            var et = DateTime.Now;
                            Config.AddSqlLog(info, st, es, count, (et - st).TotalMilliseconds, false);
                            Msg.Warning(ex.Message);
                            isRet = false;
                            isErr = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Msg.Warning(ex.Message);

            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
            return dataSet;
        }

        /// <summary>
        /// 从 SELECT 语句里提取第一个表名
        /// </summary>
        public static string GetFirstTableNameFromSql(string sql)
        {
            try
            {
                sql = sql.Trim().ToLower();
                var fromIndex = sql.IndexOf("from ");
                if (fromIndex == -1) return "Table";

                var afterFrom = sql.Substring(fromIndex + 5).Trim();
                var tableName = afterFrom.Split(new[] { ' ', ',', '[', ']', '`', '"' }, StringSplitOptions.RemoveEmptyEntries)[0];
                return tableName;
            }
            catch
            {
                return "Table";
            }
        }

        /// <summary>
        /// 从 SELECT 语句里获取第一个表名的【结束索引位置】（原字符串位置，不转小写）
        /// </summary>
        /// <returns>表名最后一个字符的索引；找不到返回 -1</returns>
        public static int GetFirstTableNameEndIndex(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return 0;

            try
            {
                string lowerSql = sql.ToLower();
                //查找作为独立单词出现的第一个 from(避免匹配到 from_date、t_from 之类的列名)
                int fromIndex = FindKeyword(lowerSql, "from");

                if (fromIndex == -1)
                    return 0;

                // 跳过 from 本身（4个字符）
                int pos = fromIndex + 4;

                while (pos < sql.Length && char.IsWhiteSpace(sql[pos]))
                    pos++;

                if (pos >= sql.Length)
                    return 0;

                char firstChar = sql[pos];
                if (firstChar == '[' || firstChar == '`' || firstChar == '"')
                {
                    char endChar = firstChar == '[' ? ']' : firstChar;
                    int closePos = sql.IndexOf(endChar, pos + 1);
                    return closePos + 1;
                }

                char[] splitChars = { ' ', ',', ';', '\n', '\r', '\t' };
                while (pos < sql.Length && !splitChars.Contains(sql[pos]))
                    pos++;

                return pos;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 查找独立单词关键字(前后不能是字母/数字/下划线)第一次出现的位置
        /// </summary>
        /// <returns>找不到返回 -1</returns>
        private static int FindKeyword(string sql, string keyword)
        {
            var start = 0;
            while (start <= sql.Length - keyword.Length)
            {
                var index = sql.IndexOf(keyword, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    return -1;
                }
                var beforeOk = index == 0 || !IsWordChar(sql[index - 1]);
                var afterIndex = index + keyword.Length;
                var afterOk = afterIndex >= sql.Length || !IsWordChar(sql[afterIndex]);
                if (beforeOk && afterOk)
                {
                    return index;
                }
                start = index + keyword.Length;
            }
            return -1;
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        public static string GetSelect(Biao b, LianJie l)
        {
            var ziDuanList = GetZiDuans(b, l);
            if (ziDuanList.Count == 0)
            {
                return string.Empty;
            }
            var ret = "SELECT ";
            foreach (var zd in ziDuanList)
            {
                ret += zd.Name + ",";
            }
            ret = ret.TrimEnd(',') + " FROM " + b.Name;
            var where = BuildPkWhere(ziDuanList);
            if (!string.IsNullOrEmpty(where))
            {
                ret += " WHERE " + where;
            }
            return (ret + ";").SqlFormat();
        }

        public static string GetInsert(Biao b, LianJie l)
        {
            var ziDuanList = GetZiDuans(b, l);
            if (ziDuanList.Count == 0)
            {
                return string.Empty;
            }
            var ret = "INSERT INTO " + b.Name;
            var s = " (";
            var e = " (";
            foreach (var zd in ziDuanList)
            {
                s += zd.Name + ",";
                if (IsNumberType(zd.Type))
                {
                    e += "0,";
                }
                else
                {
                    e += "'',";
                }
            }
            s = s.TrimEnd(',');
            e = e.TrimEnd(',');
            ret = ret + s + ") VALUES" + e + ");";
            return ret.SqlFormat();

        }

        public static string GetUpdate(Biao b, LianJie l)
        {
            var ziDuanList = GetZiDuans(b, l);
            if (ziDuanList.Count == 0)
            {
                return string.Empty;
            }
            var ret = "UPDATE " + b.Name + " SET ";
            foreach (var zd in ziDuanList)
            {
                ret += zd.Name + " = ";
                if (IsNumberType(zd.Type))
                {
                    ret += "0,";
                }
                else
                {
                    ret += "'',";
                }
            }
            ret = ret.TrimEnd(',');
            var where = BuildPkWhere(ziDuanList);
            if (!string.IsNullOrEmpty(where))
            {
                ret += " WHERE " + where;
            }
            return (ret + ";").SqlFormat();
        }

        public static string GetDelete(Biao b, LianJie l)
        {
            var ziDuanList = GetZiDuans(b, l);
            var ret = "DELETE FROM " + b.Name;
            var where = BuildPkWhere(ziDuanList);
            if (!string.IsNullOrEmpty(where))
            {
                ret += " WHERE " + where;
            }
            return (ret + ";").SqlFormat();
        }

        /// <summary>
        /// 判断字段类型是否为数值类型(用于生成示例值)
        /// </summary>
        private static bool IsNumberType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return false;
            }
            var t = type.Trim().ToLowerInvariant();
            return t == "int" || t == "bigint" || t == "smallint" || t == "tinyint"
                   || t == "integer" || t == "int2" || t == "int4" || t == "int8"
                   || t.StartsWith("number", StringComparison.Ordinal)
                   || t.StartsWith("numeric", StringComparison.Ordinal)
                   || t.StartsWith("decimal", StringComparison.Ordinal);
        }

        /// <summary>
        /// 按主键拼接等值条件(无主键时返回空字符串,避免生成残缺的 WHERE)
        /// </summary>
        private static string BuildPkWhere(List<ZiDuan> ziDuanList)
        {
            return string.Join(" AND ", ziDuanList.Where(p => p.IsPK).Select(p => p.Name + " = ''"));
        }

        /// <summary>
        /// 根据数据库类型返回实际sql中使用的值
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetValueByType(object value, Type type)
        {
            string valueStr;
            if (value == DBNull.Value)
            {
                valueStr = "NULL";
            }
            else
            {
                if (type == typeof(int) || type == typeof(int?) || type == typeof(long) || type == typeof(long?))
                {
                    valueStr = value.ToString();
                }
                else if (type == typeof(DateTime) || type == typeof(DateTime?))
                {
                    valueStr = "'" + ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss") + "'";
                }
                else
                {
                    //拼接字符串值时转义单引号,避免生成非法SQL
                    valueStr = "'" + value.ToString().Replace("'", "''") + "'";
                }
            }
            return valueStr;
        }

        /// <summary>
        /// 获取 处理后的字段名
        /// </summary>
        public static string GetEscapeName(LianJie l, string name)
        {
            return DB(l).GetEscapeName(name);
        }

    }
}
