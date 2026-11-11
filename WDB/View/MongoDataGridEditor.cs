using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using WDB.Common;
using WDB.Entity;

namespace WDB.View
{
    /// <summary>
    /// MongoDB 表格编辑器:把数据表格的改动翻译成 MongoDB 命令并执行
    /// SQL 连接的表格仍由 DataTableEditDataGridView 内部生成 SQL,这里只服务 NoDb(MongoDB)连接
    /// </summary>
    public class MongoDataGridEditor
    {
        /// <summary>
        /// 行 → 原始文档的映射键(由 Db.GetMongoDataTable 写入到 DataTable.ExtendedProperties)
        /// </summary>
        public const string DocMapKey = "MongoDocs";

        /// <summary>
        /// 连接上下文(需含数据库名)
        /// </summary>
        public LianJie Lj { get; set; }

        /// <summary>
        /// 集合名;为空表示该结果不支持回写
        /// </summary>
        public string Collection { get; set; }

        /// <summary>
        /// 是否支持保存
        /// </summary>
        public bool CanSave => Lj != null && !string.IsNullOrEmpty(Collection);

        /// <summary>
        /// 取 DataTable 上记录的行→原文档映射
        /// </summary>
        private static Dictionary<DataRow, BsonDocument> GetDocMap(DataTable dt)
        {
            if (dt == null || !dt.ExtendedProperties.Contains(DocMapKey))
            {
                return null;
            }
            return dt.ExtendedProperties[DocMapKey] as Dictionary<DataRow, BsonDocument>;
        }

        /// <summary>
        /// 取某行对应的原始文档(新增行返回 null)
        /// </summary>
        private static BsonDocument GetOrigin(DataRow row)
        {
            var map = GetDocMap(row?.Table);
            if (map != null && map.TryGetValue(row, out var doc))
            {
                return doc;
            }
            return null;
        }

        /// <summary>
        /// 取某字段的原值(用于按原类型还原单元格文本)
        /// </summary>
        private static BsonValue GetOriginValue(DataRow row, string field)
        {
            var origin = GetOrigin(row);
            if (origin != null && origin.Contains(field))
            {
                return origin[field];
            }
            return null;
        }

        /// <summary>
        /// 取某行文档的 _id(优先用原始文档,其次用表格里的 _id 列)
        /// </summary>
        private static BsonValue GetId(DataRow row, out string error)
        {
            error = null;
            var origin = GetOrigin(row);
            if (origin != null && origin.Contains("_id"))
            {
                return origin["_id"];
            }
            var text = row["_id"] == DBNull.Value ? null : row["_id"]?.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                return Db.ParseMongoId(text);
            }
            error = "该行没有 _id,无法定位文档";
            return null;
        }

        private static BsonValue ToBsonValue(object cellValue, BsonValue origin)
        {
            if (cellValue == null || cellValue == DBNull.Value)
            {
                return BsonNull.Value;
            }
            return Db.MongoTextToValue(cellValue, origin);
        }

        /// <summary>
        /// 把一行(新增行)组装成文档:未填写的单元格不写入,避免产生大量 null 字段
        /// </summary>
        private static BsonDocument BuildDocument(DataRow row)
        {
            var doc = new BsonDocument();
            foreach (DataColumn col in row.Table.Columns)
            {
                var value = row[col];
                if (col.ColumnName == "_id")
                {
                    if (value == null || value == DBNull.Value || string.IsNullOrEmpty(value.ToString()))
                    {
                        //留空表示交给 MongoDB 生成
                        continue;
                    }
                    doc["_id"] = Db.MongoTextToValue(value, null);
                    continue;
                }
                if (value == null || value == DBNull.Value)
                {
                    continue;
                }
                doc[col.ColumnName] = ToBsonValue(value, GetOriginValue(row, col.ColumnName));
            }
            return doc;
        }

        /// <summary>
        /// _id 的命令字面量
        /// </summary>
        private static string IdLiteral(BsonValue id)
        {
            if (id == null || id.IsBsonNull)
            {
                return "null";
            }
            if (id.IsObjectId)
            {
                return $"ObjectId(\"{id.AsObjectId}\")";
            }
            if (id.IsString)
            {
                return "\"" + id.AsString.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            }
            return id.ToJson();
        }

        /// <summary>
        /// 生成改动对应的 MongoDB 命令文本(供预览)
        /// </summary>
        public string Build(DataTableEditDataGrid.EditChangeResult changes)
        {
            if (changes == null)
            {
                return string.Empty;
            }
            var coll = string.IsNullOrEmpty(Collection) ? "?" : Collection;
            var sb = new StringBuilder();
            foreach (var row in changes.AddedRows)
            {
                sb.Append($"db.{coll}.insertOne(").Append(BuildDocument(row).ToJson()).Append(");").Append(Environment.NewLine);
            }
            foreach (var row in changes.DeletedRows)
            {
                var id = GetId(row, out _);
                sb.Append($"db.{coll}.deleteOne({{_id: {IdLiteral(id)}}});").Append(Environment.NewLine);
            }
            foreach (var item in changes.ModifiedCellsInfo)
            {
                var id = GetId(item.Key, out _);
                //_id 不参与修改(改了也无法定位文档)
                var sets = item.Value
                    .Where(field => field != "_id")
                    .Select(field => $"\"{field}\": {ToBsonValue(item.Key[field], GetOriginValue(item.Key, field)).ToJson()}")
                    .ToList();
                if (sets.Count == 0)
                {
                    continue;
                }
                sb.Append($"db.{coll}.updateOne({{_id: {IdLiteral(id)}}}, {{$set: {{{string.Join(", ", sets)}}}}});").Append(Environment.NewLine);
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 按选中的行生成一条 MongoDB 命令(对应表格右键的"创建SQL"菜单)
        /// </summary>
        /// <param name="kind">SELECT / INSERT / UPDATE / DELETE</param>
        /// <param name="row">选中行</param>
        /// <param name="selectedColumns">仅使用的列(为空表示全部列)</param>
        public string BuildRowCommand(string kind, DataRow row, IEnumerable<string> selectedColumns)
        {
            var coll = string.IsNullOrEmpty(Collection) ? "?" : Collection;
            var id = GetId(row, out _);
            var inScope = new Func<DataColumn, bool>(c => selectedColumns == null || selectedColumns.Contains(c.ColumnName));
            switch (kind)
            {
                case "SELECT":
                    {
                        var fields = row.Table.Columns.Cast<DataColumn>()
                            .Where(inScope)
                            .Select(c => $"\"{c.ColumnName}\": 1")
                            .ToList();
                        var projection = fields.Count > 0 ? ", {" + string.Join(", ", fields) + "}" : string.Empty;
                        return $"db.{coll}.find({{_id: {IdLiteral(id)}}}{projection});";
                    }
                case "SELECTWHERE":
                    {
                        //以选中的列与当前行内容作为查询条件(_id 不参与,避免出现 ObjectId 条件)
                        var conds = row.Table.Columns.Cast<DataColumn>()
                            .Where(c => c.ColumnName != "_id" && inScope(c))
                            .Select(c => $"\"{c.ColumnName}\": {ToBsonValue(row[c], GetOriginValue(row, c.ColumnName)).ToJson()}")
                            .ToList();
                        return $"db.{coll}.find({{{string.Join(", ", conds)}}});";
                    }
                case "INSERT":
                    return $"db.{coll}.insertOne({BuildDocument(row).ToJson()});";
                case "UPDATE":
                    {
                        var sets = row.Table.Columns.Cast<DataColumn>()
                            .Where(c => c.ColumnName != "_id" && inScope(c))
                            .Select(c => $"\"{c.ColumnName}\": {ToBsonValue(row[c], GetOriginValue(row, c.ColumnName)).ToJson()}")
                            .ToList();
                        return $"db.{coll}.updateOne({{_id: {IdLiteral(id)}}}, {{$set: {{{string.Join(", ", sets)}}}}});";
                    }
                case "DELETE":
                    return $"db.{coll}.deleteOne({{_id: {IdLiteral(id)}}});";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 执行保存,返回失败信息(null 表示成功)
        /// </summary>
        public string Save(DataTableEditDataGrid.EditChangeResult changes)
        {
            if (changes == null)
            {
                return "没有任何变更";
            }
            if (!CanSave)
            {
                return "该结果不支持保存(只有集合的 find 结果可以编辑保存)";
            }
            try
            {
                var added = 0;
                foreach (var row in changes.AddedRows)
                {
                    Db.InsertMongoDocument(Lj, Collection, BuildDocument(row));
                    added++;
                }
                var deletedIds = new List<BsonValue>();
                foreach (var row in changes.DeletedRows)
                {
                    var id = GetId(row, out var idError);
                    if (id == null)
                    {
                        throw new Exception(idError);
                    }
                    deletedIds.Add(id);
                }
                if (deletedIds.Count > 0)
                {
                    Db.DeleteMongoDocumentsByIds(Lj, Collection, deletedIds);
                }
                var modified = 0;
                foreach (var item in changes.ModifiedCellsInfo)
                {
                    var id = GetId(item.Key, out var idError);
                    if (id == null)
                    {
                        throw new Exception(idError);
                    }
                    var fields = new Dictionary<string, BsonValue>();
                    foreach (var field in item.Value)
                    {
                        if (field == "_id")
                        {
                            //_id 不参与修改
                            continue;
                        }
                        fields[field] = ToBsonValue(item.Key[field], GetOriginValue(item.Key, field));
                    }
                    if (fields.Count > 0)
                    {
                        Db.UpdateMongoDocumentFields(Lj, Collection, id, fields);
                        modified++;
                    }
                }
                Oops.Ok($"已保存 (新增 {added} / 修改 {modified} / 删除 {deletedIds.Count})");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return ex.Message;
            }
        }
    }
}
