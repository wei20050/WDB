using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WDB.Common
{
    /// <summary>
    /// 转换核心
    /// </summary>
    public static class CoreConvert
    {
        /// <summary>
        /// 自动转换配置
        /// </summary>
        public class MapConfig
        {
            /// <summary>
            /// 从 字段
            /// </summary>
            public string MapFrom { get; set; }

            /// <summary>
            /// 到 字段
            /// </summary>
            public string MapTo { get; set; }
        }

        /// <summary>
        /// 深拷贝对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T DeepClone<T>(T obj)
        {
            if (obj == null) return default;
            string json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// 自动转换类
        /// </summary>
        /// <typeparam name="T">转换后的类型</typeparam>
        /// <param name="objFrom">从 数据源</param>
        /// <param name="isVague">字段是否模糊匹配(不区分大小写去掉下划线)</param>
        /// <param name="mapConfigs">字段转换配置</param>
        /// <returns></returns>
        public static T Map<T>(object objFrom, bool isVague = false, params MapConfig[] mapConfigs) where T : new()
        {
            if (objFrom == null) return default;
            var jsonStr = JsonConvert.SerializeObject(objFrom, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            jsonStr = JsonMember(jsonStr, mapConfigs);
            if (isVague)
            {
                var regexs = Regex.Matches(jsonStr, "\"\\w+\":");
                foreach (Match regex in regexs)
                {
                    foreach (var p in typeof(T).GetProperties())
                    {
                        var tmp = $"\"{p.Name}\":";
                        if (regex.Value.ToUpper().Replace("_", string.Empty) != tmp.ToUpper().Replace("_", string.Empty)) continue;
                        jsonStr = jsonStr.Replace(regex.Value, tmp);
                        break;
                    }
                }
            }
            var ret = JsonConvert.DeserializeObject<T>(jsonStr);
            return ret;
        }

        /// <summary>
        /// 自动转换类
        /// </summary>
        /// <param name="objFrom">从 数据源</param>
        /// <param name="objTo">到 数据源</param>
        /// <param name="isVague">字段是否模糊匹配(不区分大小写去掉下划线)</param>
        /// <param name="mapConfigs">字段转换配置</param>
        /// <returns></returns>
        public static T Map<T>(object objFrom, T objTo, bool isVague = false, params MapConfig[] mapConfigs)
        {
            if (objTo == null) return default;
            if (objFrom == null) return objTo;
            var typeFrom = objFrom.GetType();
            var typeTo = typeof(T);
            var propertiesFrom = typeFrom.GetProperties();
            var propertiesTo = typeTo.GetProperties();
            foreach (var tFrom in propertiesFrom)
            {
                var tFromValue = tFrom.GetValue(objFrom, null);
                var tFromValueJson = JsonConvert.SerializeObject(tFromValue);
                var name = tFrom.Name;
                var mc = mapConfigs?.FirstOrDefault(p => p.MapFrom == name);
                if (mc != null) name = mc.MapTo;
                foreach (var tTo in propertiesTo)
                {
                    if (isVague)
                    {
                        if (name.ToUpper().Replace("_", string.Empty) == tTo.Name.ToUpper().Replace("_", string.Empty))
                        {
                            tTo.SetValue(objTo, JsonConvert.DeserializeObject(tFromValueJson, tTo.PropertyType), null);
                        }
                    }
                    else
                    {
                        if (name == tTo.Name)
                        {
                            tTo.SetValue(objTo, JsonConvert.DeserializeObject(tFromValueJson, tTo.PropertyType), null);
                        }
                    }
                }
            }
            return objTo;
        }

        /// <summary>
        /// 自动转换类(排除源数据中Null的值)
        /// </summary>
        /// <param name="objFrom">从 数据源</param>
        /// <param name="objTo">到 数据源</param>
        /// <param name="isVague">字段是否模糊匹配(不区分大小写去掉下划线)</param>
        /// <param name="mapConfigs">字段转换配置</param>
        /// <returns></returns>
        public static T MapNull<T>(object objFrom, T objTo, bool isVague = false, params MapConfig[] mapConfigs)
        {
            if (objTo == null) return default;
            if (objFrom == null) return objTo;
            var typeFrom = objFrom.GetType();
            var typeTo = typeof(T);
            var propertiesFrom = typeFrom.GetProperties();
            var propertiesTo = typeTo.GetProperties();
            foreach (var tFrom in propertiesFrom)
            {
                var tFromValue = tFrom.GetValue(objFrom, null);
                if (tFromValue == null) continue;
                var tFromValueJson = JsonConvert.SerializeObject(tFromValue);
                var name = tFrom.Name;
                var mc = mapConfigs?.FirstOrDefault(p => p.MapFrom == name);
                if (mc != null) name = mc.MapTo;
                foreach (var tTo in propertiesTo)
                {
                    if (isVague)
                    {
                        if (name.ToUpper().Replace("_", string.Empty) == tTo.Name.ToUpper().Replace("_", string.Empty))
                        {
                            tTo.SetValue(objTo, JsonConvert.DeserializeObject(tFromValueJson, tTo.PropertyType), null);
                        }
                    }
                    else
                    {
                        if (name == tTo.Name)
                        {
                            tTo.SetValue(objTo, JsonConvert.DeserializeObject(tFromValueJson, tTo.PropertyType), null);
                        }
                    }
                }
            }
            return objTo;
        }

        /// <summary>
        /// 替换json字段
        /// </summary>
        /// <param name="jsonStr"></param>
        /// <param name="mapConfigs"></param>
        /// <returns></returns>
        private static string JsonMember(string jsonStr, params MapConfig[] mapConfigs)
        {
            var json = jsonStr;
            foreach (var m in mapConfigs)
            {
                if (m == null || string.IsNullOrEmpty(m.MapFrom) || string.IsNullOrEmpty(m.MapTo))
                {
                    continue;
                }
                json = jsonStr.Replace($"\"{m.MapFrom}\":", $"\"{m.MapTo}\":");
            }
            return json;
        }

        /// <summary>
        /// 判断两个对象的属性值是否全部相等
        /// </summary>
        /// <param name="t1">对象1</param>
        /// <param name="t2">对象2</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool EqualsObj<T>(T t1, T t2)
        {
            return t1.GetType().GetProperties().All(item =>
                Equals(item.GetValue(t1, null),
                    t2.GetType().GetProperty(item.Name)?.GetValue(t2, null)));
        }

        /// <summary>
        /// 将JSON字符串转化为对应类型的对象
        /// </summary>
        /// <typeparam name="T">要转换的类型</typeparam>
        /// <param name="json">json字符串</param>
        /// <returns>转换后的对象</returns>
        public static T JsonToObj<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// 将JSON字符串转化为对应类型的对象
        /// </summary>
        /// <param name="json">json字符串</param>
        /// <param name="type">要转化的类型</param>
        /// <returns>转换后的对象</returns>
        public static object JsonToObj(string json, Type type)
        {
            return JsonConvert.DeserializeObject(json, type);
        }

        /// <summary>
        /// 将任意类型对象转化为JSON字符串
        /// </summary>
        /// <param name="obj">要转换的对象</param>
        /// <returns>json字符串</returns>
        public static string ObjToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        /// <summary>
        /// 将任意类型对象转化为JSON字符串(排除null字段)
        /// </summary>
        /// <param name="obj">要转换的对象</param>
        /// <returns>json字符串</returns>
        public static string ObjToJsonNull(object obj)
        {
            return JsonConvert.SerializeObject(obj, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        /// <summary>
        /// 将任意类型对象转化为JSON字符串(时间处理成普通格式)
        /// </summary>
        /// <param name="obj">要转换的对象</param>
        /// <returns>json字符串</returns>
        public static string ObjToJsonTime(object obj)
        {
            //这里使用自定义日期格式，如果不使用的话，默认是ISO8601格式    
            return JsonConvert.SerializeObject(obj, new IsoDateTimeConverter { DateTimeFormat = "yyyy-MM-dd HH:mm:ss" });
        }

        /// <summary>
        /// 将任意类型对象转化为JSON字符串(排除null字段,时间处理成普通格式)
        /// </summary>
        /// <param name="obj">要转换的对象</param>
        /// <returns>json字符串</returns>
        public static string ObjToJsonNullTime(object obj)
        {
            var jsonNull = ObjToJsonNull(obj);
            var objNull = JsonConvert.DeserializeObject(jsonNull);
            return ObjToJsonTime(objNull);
        }

        /// <summary>
        /// XML序列化
        /// </summary>
        public static string ObjToXml(object obj)
        {
            using (var sw = new StringWriter())
            {
                var serializer = new XmlSerializer(obj.GetType());
                serializer.Serialize(sw, obj);
                sw.Close();
                return sw.ToString();
            }
        }

        /// <summary>
        /// XML反序列化
        /// </summary>
        public static object XmlToObj(string xmlStr, Type t)
        {
            using (var sr = new StringReader(xmlStr))
            {
                var serializer = new XmlSerializer(t);
                return serializer.Deserialize(sr);
            }
        }

        /// <summary>
        /// XML反序列化
        /// </summary>
        public static T XmlToObj<T>(string xmlStr) where T : new()
        {
            using (var sr = new StringReader(xmlStr))
            {
                var serializer = new XmlSerializer(typeof(T));
                return (T)serializer.Deserialize(sr);
            }
        }

        /// <summary>
        /// 将DataTable对象转化为实体集合
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static List<T> DataTableToListEntity<T>(DataTable table)
        {
            var parentRow = new List<Dictionary<string, object>>();
            foreach (DataRow row in table.Rows)
            {
                var childRow = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    childRow.Add(col.ColumnName, row[col]);
                }
                parentRow.Add(childRow);
            }
            var json = JsonConvert.SerializeObject(parentRow);
            return JsonConvert.DeserializeObject<List<T>>(json);
        }

        /// <summary>
        /// 将DataTable对象转化为实体集合
        /// </summary>
        /// <param name="table"></param>
        /// <param name="type">实体类型</param>
        /// <returns></returns>
        public static object DataTableToListEntity(DataTable table, Type type)
        {
            var parentRow = new List<Dictionary<string, object>>();
            foreach (DataRow row in table.Rows)
            {
                var childRow = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                {
                    childRow.Add(col.ColumnName, row[col]);
                }
                parentRow.Add(childRow);
            }
            var json = JsonConvert.SerializeObject(parentRow);
            return JsonConvert.DeserializeObject(json, type);
        }

        /// <summary>
        /// 文件转Base64字符串
        /// </summary>
        /// <param name="fileName">文件路径</param>
        /// <returns>Base64字符串</returns>
        public static string FileToBase64(string fileName)
        {
            var fs = File.OpenRead(fileName);
            var br = new BinaryReader(fs);
            var bt = br.ReadBytes(Convert.ToInt32(fs.Length));
            br.Dispose();
            fs.Close();
            fs.Dispose();
            return Convert.ToBase64String(bt);
        }

        /// <summary>
        /// Base64字符串转文件
        /// </summary>
        /// <param name="base64Str">Base64字符串</param>
        /// <param name="fileName">文件路径</param>
        public static void Base64ToFile(string base64Str, string fileName)
        {
            var directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var contents = Convert.FromBase64String(base64Str);
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                fs.Write(contents, 0, contents.Length);
                fs.Flush();
            }
        }

        /// <summary>
        /// 文件转字节集
        /// </summary>
        /// <param name="fileName">文件路径</param>
        /// <returns>Base64字符串</returns>
        public static byte[] FileToBytes(string fileName)
        {
            var fs = File.OpenRead(fileName);
            var br = new BinaryReader(fs);
            var bt = br.ReadBytes(Convert.ToInt32(fs.Length));
            br.Dispose();
            fs.Close();
            fs.Dispose();
            return bt;
        }

        /// <summary>
        /// 字节集转文件
        /// </summary>
        /// <param name="bytes">Base64字符串</param>
        /// <param name="fileName">文件路径</param>
        public static void BytesToFile(byte[] bytes, string fileName)
        {
            var directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush();
            }
        }

    }
}
