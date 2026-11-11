using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using WDB.Entity;

namespace WDB.Common
{
    public class Config
    {
        #region 核心封装

        private static string GetPath(string fileName)
        {
            return Path.Combine(Core.GetAppPath, "cfg", fileName);
        }

        public static string ConfigGet(string key, string defaultValue = null)
        {
            return CoreConfig.Read(key, defaultValue, GetPath("wdb.cfg"));
        }

        public static void ConfigSet(string key, string value)
        {
            CoreConfig.Write(key, value, GetPath("wdb.cfg"));
        }

        public static List<LianJie> GetLianjie(string path = null)
        {
            try
            {
                if (path == null)
                {
                    path = GetPath("wdb.connect");
                }
                if (File.Exists(path))
                {
                    var str = File.ReadAllText(path) ?? string.Empty;
                    if (!string.IsNullOrEmpty(str))
                    {
                        var json = CoreEncrypt.AesDecrypt(str, GConst.KEY);
                        return JsonConvert.DeserializeObject<List<LianJie>>(json);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            return null;
        }

        public static void SetLianjie(List<LianJie> value, string path = null)
        {
            if (value != null)
            {
                if (path == null)
                {
                    path = GetPath("wdb.connect");
                }
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, CoreEncrypt.AesEncrypt(JsonConvert.SerializeObject(value), GConst.KEY));
            }
        }

        public static List<SqlLog> GetSqlLog()
        {
            try
            {
                var path = GetPath("wdb.slog");
                if (File.Exists(path))
                {
                    var str = File.ReadAllText(path) ?? string.Empty;
                    if (!string.IsNullOrEmpty(str))
                    {
                        var json = CoreEncrypt.AesDecrypt(str, GConst.KEY);
                        return JsonConvert.DeserializeObject<List<SqlLog>>(json);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            return new List<SqlLog>();
        }

        /// <summary>
        /// SQL日志文件的同步锁(后台线程可能并发执行SQL)
        /// </summary>
        private static readonly object SqlLogLock = new object();

        public static void AddSqlLog(string info, DateTime time, string sql, int count, double ms, bool status)
        {
            var sqlLog = new SqlLog
            {
                Info = info,
                Time = time,
                Sql = sql,
                Count = count,
                Ms = StrHelp.ToTimeString(ms),
                Status = status
            };
            var path = GetPath("wdb.slog");
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            //日志为"读-改-写"整体操作,多线程并发时会互相覆盖,需加锁串行化
            lock (SqlLogLock)
            {
                var sqlLogList = GetSqlLog();
                sqlLogList.Add(sqlLog);
                while (sqlLogList.Count > 1000)
                {
                    sqlLogList.RemoveAt(0);
                }
                File.WriteAllText(path, CoreEncrypt.AesEncrypt(JsonConvert.SerializeObject(sqlLogList), GConst.KEY));
            }
        }

        private static void Write(object value, [CallerMemberName] string key = "")
        {
            ConfigSet(key, CoreEncrypt.AesEncrypt(JsonConvert.SerializeObject(value), GConst.KEY));
        }

        private static T ReadObj<T>(T defaultValue = default, [CallerMemberName] string key = "")
        {
            var def = CoreEncrypt.AesEncrypt(JsonConvert.SerializeObject(defaultValue), GConst.KEY);
            var json = CoreEncrypt.AesDecrypt(ConfigGet(key, def), GConst.KEY);
            return JsonConvert.DeserializeObject<T>(json);
        }

        private static string Read(string defaultValue = "", [CallerMemberName] string key = "")
        {
            return ReadObj(defaultValue, key);
        }

        private static int ReadInt(int defaultValue = 0, [CallerMemberName] string key = "")
        {
            return ReadObj(defaultValue, key);
        }

        private static bool ReadBool(bool defaultValue = false, [CallerMemberName] string key = "")
        {
            return ReadObj(defaultValue, key);
        }

        #endregion

        public static bool IsMax
        {
            get => ReadBool();
            set => Write(value);
        }
        public static int X
        {
            get => ReadInt();
            set => Write(value);
        }
        public static int Y
        {
            get => ReadInt();
            set => Write(value);
        }
        public static int W
        {
            get => ReadInt();
            set => Write(value);
        }
        public static int H
        {
            get => ReadInt();
            set => Write(value);
        }
        public static bool IsClose
        {
            get => ReadBool();
            set => Write(value);
        }
        public static int TextFontSize
        {
            get => ReadInt();
            set => Write(value);
        }
        /// <summary>
        /// 旧版单模型配置(接口地址),仅作首次迁移来源,迁移后清空
        /// </summary>
        public static string Url
        {
            get => Read();
            set => Write(value);
        }

        /// <summary>
        /// 旧版单模型配置(模型名),仅作首次迁移来源,迁移后清空
        /// </summary>
        public static string Model
        {
            get => Read();
            set => Write(value);
        }

        /// <summary>
        /// 旧版单模型配置(密钥),仅作首次迁移来源,迁移后清空
        /// </summary>
        public static string ApiKey
        {
            get => Read();
            set => Write(value);
        }

        /// <summary>
        /// AI 模型列表(可配置多套)。
        /// 首次读取时若为空而旧版单模型配置有值,自动迁移成一条"默认",避免升级后要重填;
        /// 迁移后清空旧字段,防止用户删光模型后旧值又"复活"出一个配置
        /// </summary>
        public static List<AiModel> AiModels
        {
            get
            {
                var list = ReadObj<List<AiModel>>();
                if (list != null && list.Count > 0)
                {
                    return list;
                }
                if (string.IsNullOrWhiteSpace(Url) && string.IsNullOrWhiteSpace(Model) && string.IsNullOrWhiteSpace(ApiKey))
                {
                    return new List<AiModel>();
                }
                list = new List<AiModel>
                {
                    new AiModel
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = "默认",
                        Url = Url,
                        Model = Model,
                        ApiKey = ApiKey
                    }
                };
                AiModels = list;
                Url = string.Empty;
                Model = string.Empty;
                ApiKey = string.Empty;
                return list;
            }
            set => Write(value);
        }

        /// <summary>
        /// 最后一次使用的模型 Id:下次启动默认选中它
        /// </summary>
        public static string AiModelId
        {
            get => Read();
            set => Write(value);
        }

        /// <summary>
        /// AI 助手面板"技能"下拉的选中项(0:普通对话 1:生成语句)。
        /// 两个宿主页下拉项都是"普通对话 + 生成语句",文案不同(如"MongoDB技能"),所以存序号而不是名称;
        /// 没配过时默认"生成语句"(1)
        /// </summary>
        public static int AiChatSkill
        {
            get => ReadInt(1);
            set => Write(value);
        }

        /// <summary>
        /// 当前该用的模型:优先"最后一次使用的",找不到(或没记录)时回退到第一个,一个都没有返回 null
        /// </summary>
        public static AiModel AiModelCurrent()
        {
            var list = AiModels ?? new List<AiModel>();
            return list.Find(p => p.Id == AiModelId) ?? (list.Count > 0 ? list[0] : null);
        }

    }
}
