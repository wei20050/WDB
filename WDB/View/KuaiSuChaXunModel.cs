using NetFrameWork.Database;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using WDB.Common;
using WDB.Entity;

namespace WDB.View
{
    /// <summary>
    /// 快速查询:可视化选择字段/条件并拼出SQL
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class KuaiSuChaXunModel
    {
        /// <summary>
        /// 当前表(打开查询页时的表/视图)
        /// </summary>
        public Biao SelectedBiao { get; set; }

        /// <summary>
        /// 字段勾选列表
        /// </summary>
        public ObservableCollection<ZiDuanXuanZe> ZiDuanList { get; set; } = new ObservableCollection<ZiDuanXuanZe>();

        /// <summary>
        /// 条件列表
        /// </summary>
        public ObservableCollection<TiaoJianItem> TiaoJianList { get; set; } = new ObservableCollection<TiaoJianItem>();

        /// <summary>
        /// 排序列表
        /// </summary>
        public ObservableCollection<PaiXuItem> PaiXuList { get; set; } = new ObservableCollection<PaiXuItem>();

        /// <summary>
        /// 去重
        /// </summary>
        public bool QuChong { get; set; }

        /// <summary>
        /// 是否限制行数
        /// </summary>
        public bool ShiYongLimit { get; set; } = true;

        /// <summary>
        /// 限制行数
        /// </summary>
        public double LimitCount { get; set; } = 100;

        /// <summary>
        /// 没有条件行(用于显示兜底的添加按钮)
        /// </summary>
        public bool WuTiaoJian { get; set; } = true;

        /// <summary>
        /// 没有排序规则行(用于显示兜底的添加按钮)
        /// </summary>
        public bool WuPaiXu { get; set; } = true;

        public List<string> CaoZuoFuList { get; } = new List<string>
        {
            "=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IN", "NOT IN", "BETWEEN", "IS NULL", "IS NOT NULL"
        };

        public List<string> GuanXiList { get; } = new List<string> { "AND", "OR" };

        public List<string> PaiXuFangXiangList { get; } = new List<string> { "ASC", "DESC" };

        /// <summary>
        /// 新增一行条件
        /// </summary>
        public void AddTiaoJian()
        {
            TiaoJianList.Add(new TiaoJianItem
            {
                Field = ZiDuanList.Count > 0 ? ZiDuanList[0].Name : string.Empty,
                CaoZuoFu = "=",
                GuanXi = "AND"
            });
            RefreshShouHang();
        }

        /// <summary>
        /// 新增一行指定内容的条件(表格右键"插入一个过滤条件"用)
        /// </summary>
        public void AddTiaoJian(TiaoJianItem item)
        {
            if (item == null)
            {
                return;
            }
            TiaoJianList.Add(item);
            RefreshShouHang();
        }

        /// <summary>
        /// 删除一行条件
        /// </summary>
        public void RemoveTiaoJian(TiaoJianItem item)
        {
            if (item == null || !TiaoJianList.Contains(item))
            {
                return;
            }
            TiaoJianList.Remove(item);
            RefreshShouHang();
        }

        /// <summary>
        /// 新增一行排序
        /// </summary>
        public void AddPaiXu()
        {
            PaiXuList.Add(new PaiXuItem
            {
                Field = ZiDuanList.Count > 0 ? ZiDuanList[0].Name : string.Empty,
                FangXiang = "ASC"
            });
            RefreshShouHang();
        }

        /// <summary>
        /// 新增一行指定内容的排序(表格右键"插入一个排序条件"用)
        /// </summary>
        public void AddPaiXu(PaiXuItem item)
        {
            if (item == null)
            {
                return;
            }
            PaiXuList.Add(item);
            RefreshShouHang();
        }

        /// <summary>
        /// 重新计算首行标记(外部直接改动条件/排序集合后调用)
        /// </summary>
        public void Refresh()
        {
            RefreshShouHang();
        }

        /// <summary>
        /// 删除一行排序
        /// </summary>
        public void RemovePaiXu(PaiXuItem item)
        {
            if (item == null || !PaiXuList.Contains(item))
            {
                return;
            }
            PaiXuList.Remove(item);
            RefreshShouHang();
        }

        /// <summary>
        /// 清空条件与排序
        /// </summary>
        public void QingKong()
        {
            TiaoJianList = new ObservableCollection<TiaoJianItem>();
            PaiXuList = new ObservableCollection<PaiXuItem>();
            RefreshShouHang();
        }

        /// <summary>
        /// 标记首行:首行显示添加按钮,其余行显示 AND、OR
        /// </summary>
        private void RefreshShouHang()
        {
            WuTiaoJian = TiaoJianList.Count == 0;
            WuPaiXu = PaiXuList.Count == 0;
            for (var i = 0; i < TiaoJianList.Count; i++)
            {
                TiaoJianList[i].ShouHang = i == 0;
            }
            for (var i = 0; i < PaiXuList.Count; i++)
            {
                PaiXuList[i].ShouHang = i == 0;
            }
        }

        /// <summary>
        /// 根据当前选择拼接查询SQL
        /// </summary>
        public string BuildSql(LianJie lj, out string error)
        {
            var limit = (int)LimitCount;
            var hasLimit = ShiYongLimit && limit > 0;
            var isSqlServer = lj != null && lj.DataBaseType == ORM.DbTypeEnum.SqlServer;
            var isOracle = lj != null && lj.DataBaseType == ORM.DbTypeEnum.Oracle;

            var body = BuildBody(lj, hasLimit ? limit : 0, out error);
            if (body == null)
            {
                return null;
            }
            var sb = new StringBuilder(body);
            var orderBy = BuildOrderBy(lj);
            if (!string.IsNullOrEmpty(orderBy))
            {
                sb.Append(Environment.NewLine).Append(orderBy);
            }
            //行数限制
            if (hasLimit && !isSqlServer && !isOracle)
            {
                sb.Append(Environment.NewLine).Append("LIMIT ").Append(limit);
            }
            sb.Append(";");
            return sb.ToString().SqlFormat();
        }

        /// <summary>
        /// 分页查询SQL(查看数据页使用):offset 从0开始,fetch 为每页条数
        /// </summary>
        public string BuildPageSql(LianJie lj, int offset, int fetch, out string error)
        {
            var body = BuildBody(lj, 0, out error);
            if (body == null)
            {
                return null;
            }
            return Db.BuildPageSql(lj, body, BuildOrderBy(lj), offset, fetch);
        }

        /// <summary>
        /// 拼接查询主体:SELECT [DISTINCT] [TOP n] * FROM 表 [WHERE 条件](不含排序与行数限制)
        /// </summary>
        /// <param name="topCount">大于0时按数据库类型在主体里限制行数(Oracle 走 ROWNUM)</param>
        private string BuildBody(LianJie lj, int topCount, out string error)
        {
            error = null;
            if (lj == null)
            {
                error = "当前没有数据库连接";
                return null;
            }
            if (SelectedBiao == null)
            {
                error = "当前页没有关联的表/视图,无法使用快速查询";
                return null;
            }
            var hasLimit = topCount > 0;
            var isSqlServer = lj.DataBaseType == ORM.DbTypeEnum.SqlServer;
            var isOracle = lj.DataBaseType == ORM.DbTypeEnum.Oracle;

            var sb = new StringBuilder("SELECT ");
            if (QuChong)
            {
                sb.Append("DISTINCT ");
            }
            if (hasLimit && isSqlServer)
            {
                sb.Append("TOP ").Append(topCount).Append(" ");
            }
            sb.Append("*");
            sb.Append(Environment.NewLine).Append("FROM ").Append(Db.GetEscapeName(lj, SelectedBiao.Name));

            //条件:每行行末的 AND/OR 作为该行与下一行的连接符(第一行默认 AND)
            var wheres = new List<string>();
            TiaoJianItem prev = null;
            foreach (var item in TiaoJianList)
            {
                if (!item.Enable || string.IsNullOrWhiteSpace(item.Field))
                {
                    continue;
                }
                var part = BuildOneTiaoJian(lj, item, out var msg);
                if (!string.IsNullOrEmpty(msg))
                {
                    error = msg;
                    return null;
                }
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }
                if (prev == null)
                {
                    wheres.Add(part);
                }
                else
                {
                    wheres.Add((string.IsNullOrWhiteSpace(prev.GuanXi) ? "AND" : prev.GuanXi) + " " + part);
                }
                prev = item;
            }
            var whereSql = string.Join(Environment.NewLine + "  ", wheres);
            if (hasLimit && isOracle)
            {
                whereSql = string.IsNullOrEmpty(whereSql)
                    ? "ROWNUM <= " + topCount
                    : whereSql + Environment.NewLine + "  AND ROWNUM <= " + topCount;
            }
            if (!string.IsNullOrEmpty(whereSql))
            {
                sb.Append(Environment.NewLine).Append("WHERE ").Append(whereSql);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 拼接排序子句,没有有效排序规则时返回 null
        /// </summary>
        private string BuildOrderBy(LianJie lj)
        {
            var orders = PaiXuList
                .Where(p => p.Enable && !string.IsNullOrWhiteSpace(p.Field))
                .Select(p => Db.GetEscapeName(lj, p.Field) + " " + (p.FangXiang == "DESC" ? "DESC" : "ASC"))
                .ToList();
            return orders.Count == 0 ? null : "ORDER BY " + string.Join(", ", orders);
        }

        /// <summary>
        /// 拼一行条件
        /// </summary>
        private string BuildOneTiaoJian(LianJie lj, TiaoJianItem item, out string error)
        {
            error = null;
            var col = Db.GetEscapeName(lj, item.Field);
            var op = string.IsNullOrWhiteSpace(item.CaoZuoFu) ? "=" : item.CaoZuoFu.Trim();
            if (op == "IS NULL" || op == "IS NOT NULL")
            {
                return col + " " + op;
            }
            var value = item.Value == null ? string.Empty : item.Value.Trim();
            if (string.IsNullOrEmpty(value))
            {
                //值没填的行跳过,方便边填边预览
                return string.Empty;
            }
            var zd = ZiDuanList.FirstOrDefault(p => p.Name == item.Field);
            var isNumber = zd != null && IsNumberType(zd.Type);
            var v = GetValueSql(value, isNumber, op, out var msg);
            if (!string.IsNullOrEmpty(msg))
            {
                error = msg;
                return null;
            }
            return col + " " + op + " " + v;
        }

        /// <summary>
        /// 根据运算符处理值
        /// </summary>
        private string GetValueSql(string value, bool isNumber, string op, out string error)
        {
            error = null;
            if (op == "IN" || op == "NOT IN")
            {
                var list = value.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).Select(p => Quote(p, isNumber)).ToList();
                if (list.Count == 0)
                {
                    error = $"{op} 的值不能为空";
                    return null;
                }
                return "(" + string.Join(", ", list) + ")";
            }
            if (op == "LIKE" || op == "NOT LIKE")
            {
                //LIKE 默认前后补 %,已经自己写了通配符的不做处理
                if (value.IndexOf('%') < 0 && value.IndexOf('_') < 0)
                {
                    value = "%" + value + "%";
                }
                if (value.StartsWith("'") && value.EndsWith("'") && value.Length > 1)
                {
                    return value;
                }
                return "'" + value.Replace("'", "''") + "'";
            }
            if (op == "BETWEEN")
            {
                var arr = value.Split(new char[] { ',', ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);
                if (arr.Length < 2)
                {
                    error = "BETWEEN 需要两个值,用空格或逗号分隔";
                    return null;
                }
                return Quote(arr[0], isNumber) + " AND " + Quote(arr[1], isNumber);
            }
            return Quote(value, isNumber);
        }

        /// <summary>
        /// 值加引号(数字不加,已带引号/括号或NULL的原样使用)
        /// </summary>
        private string Quote(string value, bool isNumber)
        {
            if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                return "NULL";
            }
            if (isNumber || double.TryParse(value, out _))
            {
                return value;
            }
            if (value.StartsWith("(") || (value.StartsWith("'") && value.EndsWith("'")))
            {
                return value;
            }
            return "'" + value.Replace("'", "''") + "'";
        }

        /// <summary>
        /// 字段类型是否为数值类型
        /// </summary>
        private bool IsNumberType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return false;
            }
            var t = type.Trim().ToLowerInvariant();
            return t == "int" || t == "bigint" || t == "smallint" || t == "tinyint"
                   || t == "integer" || t == "int2" || t == "int4" || t == "int8"
                   || t == "real" || t == "money" || t == "float" || t == "double"
                   || t.StartsWith("number") || t.StartsWith("numeric") || t.StartsWith("decimal");
        }
    }

    /// <summary>
    /// 字段项(供条件/排序下拉选择)
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class ZiDuanXuanZe
    {
        public string Name { get; set; }
        public string Comment { get; set; }
        public string Type { get; set; }

        public string Display
        {
            get
            {
                return string.IsNullOrWhiteSpace(Comment) ? Name : Name + "(" + Comment + ")";
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// 一行WHERE条件
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class TiaoJianItem
    {
        /// <summary>
        /// 是否首行(首行前面显示 WHERE)
        /// </summary>
        public bool ShouHang { get; set; }

        /// <summary>
        /// 是否参与本次查询(勾选)
        /// </summary>
        public bool Enable { get; set; } = true;

        public string GuanXi { get; set; } = "AND";
        public string Field { get; set; }
        public string CaoZuoFu { get; set; } = "=";
        public string Value { get; set; }
    }

    /// <summary>
    /// 一行排序
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class PaiXuItem
    {
        /// <summary>
        /// 是否首行(首行前面显示 ORDER BY)
        /// </summary>
        public bool ShouHang { get; set; }

        /// <summary>
        /// 是否参与本次查询(勾选)
        /// </summary>
        public bool Enable { get; set; } = true;

        public string Field { get; set; }
        public string FangXiang { get; set; } = "ASC";
    }
}
