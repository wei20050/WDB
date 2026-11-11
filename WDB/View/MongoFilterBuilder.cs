using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WDB.Common;

namespace WDB.View
{
    /// <summary>
    /// 把[筛选&amp;排序]面板的条件与排序翻译成 MongoDB 的过滤/排序 JSON
    /// 语义与 SQL 面板保持一致(AND 的优先级高于 OR)
    /// </summary>
    public static class MongoFilterBuilder
    {
        /// <summary>
        /// 翻译查询条件;_id 等字段的字面量按字段样本类型处理
        /// </summary>
        /// <param name="items">条件行</param>
        /// <param name="getFieldType">按字段名取样本类型(可为 null)</param>
        /// <param name="error">失败原因</param>
        /// <returns>过滤条件 JSON(无条件时返回 null)</returns>
        public static string Build(IEnumerable<TiaoJianItem> items, Func<string, string> getFieldType, out string error)
        {
            error = null;
            var valid = items == null
                ? new List<TiaoJianItem>()
                : items.Where(p => p.Enable && !string.IsNullOrWhiteSpace(p.Field)).ToList();
            if (valid.Count == 0)
            {
                return null;
            }
            //按行末的 AND/OR 分组:组内 AND,组间 OR(SQL 里 AND 优先级高于 OR)
            var orGroups = new List<List<BsonDocument>>();
            var current = new List<BsonDocument>();
            TiaoJianItem prev = null;
            foreach (var item in valid)
            {
                var part = BuildOne(item, getFieldType, out var msg);
                if (!string.IsNullOrEmpty(msg))
                {
                    error = msg;
                    return null;
                }
                if (part == null)
                {
                    //值没填的行跳过
                    continue;
                }
                if (prev != null && IsOr(prev.GuanXi))
                {
                    orGroups.Add(current);
                    current = new List<BsonDocument>();
                }
                current.Add(part);
                prev = item;
            }
            if (current.Count > 0)
            {
                orGroups.Add(current);
            }
            if (orGroups.Count == 0)
            {
                return null;
            }
            var groups = orGroups
                .Select(g => g.Count == 1 ? (BsonValue)g[0] : new BsonDocument("$and", new BsonArray(g)))
                .ToList();
            var filter = groups.Count == 1
                ? groups[0]
                : new BsonDocument("$or", new BsonArray(groups));
            return filter.ToJson();
        }

        /// <summary>
        /// 翻译排序规则(形如 {"_id":-1,"name":1})
        /// </summary>
        public static string BuildSort(IEnumerable<PaiXuItem> items)
        {
            var list = items == null
                ? new List<PaiXuItem>()
                : items.Where(p => p.Enable && !string.IsNullOrWhiteSpace(p.Field)).ToList();
            if (list.Count == 0)
            {
                return null;
            }
            var doc = new BsonDocument();
            foreach (var item in list)
            {
                doc[item.Field.Trim()] = string.Equals(item.FangXiang, "DESC", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
            }
            return doc.ToJson();
        }

        private static bool IsOr(string guanXi)
        {
            return string.Equals((guanXi ?? "AND").Trim(), "OR", StringComparison.OrdinalIgnoreCase);
        }

        private static BsonDocument BuildOne(TiaoJianItem item, Func<string, string> getFieldType, out string error)
        {
            error = null;
            var field = item.Field.Trim();
            var op = string.IsNullOrWhiteSpace(item.CaoZuoFu) ? "=" : item.CaoZuoFu.Trim().ToUpperInvariant();
            var type = getFieldType?.Invoke(field);
            var value = item.Value == null ? string.Empty : item.Value.Trim();

            if (op == "IS NULL")
            {
                return new BsonDocument(field, BsonNull.Value);
            }
            if (op == "IS NOT NULL")
            {
                return new BsonDocument(field, new BsonDocument("$ne", BsonNull.Value));
            }
            if (value.Length == 0)
            {
                //值没填的行跳过,方便边填边查询
                return null;
            }
            switch (op)
            {
                case "IN":
                case "NOT IN":
                    var list = value.Split(',')
                        .Select(p => p.Trim())
                        .Where(p => p.Length > 0)
                        .Select(p => Literal(p, type))
                        .ToList();
                    if (list.Count == 0)
                    {
                        error = $"{op} 的值不能为空";
                        return null;
                    }
                    return new BsonDocument(field, new BsonDocument(op == "IN" ? "$in" : "$nin", new BsonArray(list)));

                case "BETWEEN":
                    var arr = value.Split(new[] { ',', ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);
                    if (arr.Length < 2)
                    {
                        error = "BETWEEN 需要两个值,用空格或逗号分隔";
                        return null;
                    }
                    return new BsonDocument(field, new BsonDocument
                    {
                        { "$gte", Literal(arr[0], type) },
                        { "$lte", Literal(arr[1], type) }
                    });

                case "LIKE":
                case "NOT LIKE":
                    var pattern = TrimQuotes(value);
                    //默认前后补 %,已经自己写了通配符的不做处理(与 SQL 面板一致)
                    if (pattern.IndexOf('%') < 0 && pattern.IndexOf('_') < 0)
                    {
                        pattern = "%" + pattern + "%";
                    }
                    var regex = new BsonDocument("$regex", LikeToRegex(pattern));
                    return op == "LIKE"
                        ? new BsonDocument(field, regex)
                        : new BsonDocument(field, new BsonDocument("$not", regex));

                case "=":
                    return new BsonDocument(field, Literal(value, type));

                case "<>":
                    return new BsonDocument(field, new BsonDocument("$ne", Literal(value, type)));

                case ">":
                case ">=":
                case "<":
                case "<=":
                    var mongoOp = op == ">" ? "$gt" : op == ">=" ? "$gte" : op == "<" ? "$lt" : "$lte";
                    return new BsonDocument(field, new BsonDocument(mongoOp, Literal(value, type)));

                default:
                    error = $"不支持的运算符:{item.CaoZuoFu}";
                    return null;
            }
        }

        /// <summary>
        /// 条件值转 BsonValue:优先按字段样本类型解析,其次支持 JSON 与纯数字
        /// </summary>
        private static BsonValue Literal(string value, string type)
        {
            var v = TrimQuotes((value ?? string.Empty).Trim());
            var t = (type ?? string.Empty).ToLowerInvariant();
            if (t == "bool" && bool.TryParse(v, out var b))
            {
                return b;
            }
            if (t == "int32" && int.TryParse(v, out var i))
            {
                return i;
            }
            if (t == "int64" && long.TryParse(v, out var l))
            {
                return l;
            }
            if ((t == "double" || t == "decimal") && double.TryParse(v, out var d))
            {
                return d;
            }
            if (t == "objectId" && ObjectId.TryParse(v, out var oid))
            {
                return oid;
            }
            var text = Db.NormalizeMongoJson(v);
            if (v.Length > 1 && v.StartsWith("{"))
            {
                try
                {
                    return BsonDocument.Parse(text);
                }
                catch
                {
                    //解析失败按字符串处理
                }
            }
            if (v.Length > 1 && v.StartsWith("["))
            {
                try
                {
                    return MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonArray>(text);
                }
                catch
                {
                    //解析失败按字符串处理
                }
            }
            //字段没有样本(集合为空)时,纯数字按数字匹配,更符合直觉
            if (string.IsNullOrEmpty(t) && v.Length > 0 && long.TryParse(v, out var num))
            {
                return num;
            }
            return v;
        }

        /// <summary>
        /// SQL LIKE 通配符转正则(% → .*, _ → .)
        /// </summary>
        private static string LikeToRegex(string pattern)
        {
            var sb = new StringBuilder();
            foreach (var c in pattern ?? string.Empty)
            {
                if (c == '%')
                {
                    sb.Append(".*");
                }
                else if (c == '_')
                {
                    sb.Append('.');
                }
                else
                {
                    sb.Append(Regex.Escape(c.ToString()));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 去掉用户按 SQL 习惯写的单/双引号
        /// </summary>
        private static string TrimQuotes(string value)
        {
            var v = value ?? string.Empty;
            if (v.Length > 1 && ((v.StartsWith("'") && v.EndsWith("'")) || (v.StartsWith("\"") && v.EndsWith("\""))))
            {
                return v.Substring(1, v.Length - 2);
            }
            return v;
        }
    }
}
