using ICSharpCode.AvalonEdit.CodeCompletion;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WDB.Common
{
    /// <summary>
    /// MongoDB 命令的智能提示候选:
    /// 顶层给命令关键字/查询操作符/集合名,db. 后给集合名,db.集合. 后给方法名与字段名
    /// 库/集合/字段由宿主页面异步加载后注入,这里只做纯内存匹配
    /// </summary>
    public class MongoCompletionProvider
    {
        /// <summary>
        /// 服务器上的数据库
        /// </summary>
        public List<string> Databases { get; set; } = new List<string>();

        /// <summary>
        /// 当前库下的集合
        /// </summary>
        public List<string> Collections { get; set; } = new List<string>();

        /// <summary>
        /// 集合字段缓存(集合名 → 字段名)
        /// </summary>
        public Dictionary<string, List<string>> Fields { get; } =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<string> Keywords = new List<string>
        {
            "show dbs", "show collections", "show tables", "use ", "help",
            "db.stats()", "db.version()", "db.runCommand({", "db.getCollectionNames()", "db.dropDatabase()"
        };

        private static readonly List<string> Operators = new List<string>
        {
            "$gt", "$gte", "$lt", "$lte", "$ne", "$eq", "$in", "$nin", "$exists", "$regex", "$type",
            "$or", "$and", "$not", "$nor", "$set", "$unset", "$inc", "$mul", "$push", "$pull", "$addToSet", "$each", "$slice", "$rename"
        };

        private static readonly List<string> Methods = new List<string>
        {
            "find(", "findOne(", "count(", "countDocuments(", "distinct(", "aggregate(",
            "insert(", "insertOne(", "insertMany(", "updateOne(", "updateMany(", "replaceOne(",
            "deleteOne(", "deleteMany(", "drop(", "createIndex(", "getIndexes()", "limit(", "skip(", "sort("
        };

        /// <summary>
        /// 光标前整词 → 候选项
        /// </summary>
        public IList<ICompletionData> GetCompletions(string word)
        {
            var result = new List<ICompletionData>();
            word = word ?? string.Empty;
            var lastDot = word.LastIndexOf('.');

            //顶层:命令关键字 + 查询操作符 + 集合名
            if (lastDot < 0)
            {
                AddItems(result, Keywords, null, word, 30, "MongoDB 命令");
                AddItems(result, Operators, "Func", word, 20, "查询/更新操作符");
                AddItems(result, Collections, "Collection", word, 25, "集合");
                return result;
            }

            var head = word.Substring(0, lastDot);
            var prefix = word.Substring(lastDot + 1);

            //db. → 集合名
            if (head.Equals("db", StringComparison.OrdinalIgnoreCase))
            {
                AddItems(result, Collections, "Collection", prefix, 30, "集合");
                return result;
            }

            //db.集合. → 方法名 + 该集合的字段
            if (head.StartsWith("db.", StringComparison.OrdinalIgnoreCase) && head.IndexOf('.', 3) < 0)
            {
                var collection = head.Substring(3);
                AddItems(result, Methods, "Func", prefix, 30, "集合方法");
                AddFields(result, collection, prefix);
                return result;
            }

            //更深的层级:按字段提示
            AddFields(result, null, prefix);
            return result;
        }

        private void AddFields(List<ICompletionData> result, string collection, string prefix)
        {
            var names = new List<string>();
            if (!string.IsNullOrEmpty(collection) && Fields.TryGetValue(collection, out var list))
            {
                names.AddRange(list);
            }
            else
            {
                foreach (var item in Fields.Values)
                {
                    names.AddRange(item);
                }
            }
            AddItems(result, names.Distinct().ToList(), "Field", prefix, 25, "字段");
        }

        private static void AddItems(List<ICompletionData> result, IEnumerable<string> source, string objType, string prefix, double priority, string desc)
        {
            foreach (var text in source)
            {
                if (string.IsNullOrEmpty(text) || result.Any(p => p.Text == text))
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(prefix) && !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                result.Add(new SqlCompletionData(text, objType, desc, priority));
            }
        }
    }
}
