using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using WDB.Entity;

namespace WDB.Common
{
    /// <summary>
    /// AI 调用(OpenAI 兼容接口)与提示词拼装
    /// 提示词集中放在这里,方便统一调优
    /// </summary>
    public class AIHelp
    {
        #region 系统提示词

        /// <summary>
        /// 生成 SQL:只允许产出可执行语句,禁止解释与代码块
        /// </summary>
        public const string SqlSystem =
            "你是资深数据库工程师,只输出可直接执行的SQL。硬性要求:\n" +
            "1.只能使用用户给出的表和字段,禁止编造表名、字段名或函数;\n" +
            "2.只输出SQL本身,不要解释、寒暄、标题,也不要用```包裹;\n" +
            "3.多条语句用分号分隔并按执行顺序排列;\n" +
            "4.信息不足时按最常规的做法补全。";

        /// <summary>
        /// 生成 MongoDB 命令:一行 shell 命令
        /// </summary>
        public const string MongoSystem =
            "你是资深MongoDB工程师,只输出可直接在mongo shell执行的一行命令。硬性要求:\n" +
            "1.只能使用用户给出的集合与字段,禁止编造字段名;\n" +
            "2.只输出db.集合.xxx(...)命令本身,不要解释、寒暄,也不要用```包裹;\n" +
            "3.命令以分号结尾。";

        /// <summary>
        /// 普通问答:允许解释,但要求简洁
        /// </summary>
        public const string ChatSystem =
            "你是严谨的数据库技术助手,回答简洁、直击要点,不写客套话;涉及代码时直接给可运行的片段。";

        #endregion

        /// <summary>
        /// 发起一次单轮对话请求,返回正文(已去掉代码块围栏与首尾空白)
        /// </summary>
        /// <param name="system">系统提示词</param>
        /// <param name="user">用户提示词</param>
        /// <param name="temperature">采样温度,生成语句建议给低值(默认 0.2),普通问答可给 null 用服务端默认</param>
        /// <param name="maxTokens">最大输出长度,限制模型啰嗦以加快返回</param>
        public static string Ask(string url, string model, string apikey, string system, string user,
            double? temperature = 0.2, int? maxTokens = 1500)
        {
            return Chat(url, model, apikey, system,
                new List<message> { new message { role = "user", content = user } }, temperature, maxTokens);
        }

        /// <summary>
        /// 发起一次多轮对话请求,返回正文(已去掉代码块围栏与首尾空白)
        /// </summary>
        /// <param name="system">系统提示词</param>
        /// <param name="history">按时间顺序排列的历史消息(不含 system),最后一条是本次提问</param>
        public static string Chat(string url, string model, string apikey, string system, List<message> history,
            double? temperature = 0.2, int? maxTokens = 1500)
        {
            var messages = new List<message>();
            if (!string.IsNullOrWhiteSpace(system))
            {
                messages.Add(new message { role = "system", content = system });
            }
            if (history != null)
            {
                messages.AddRange(history.Where(p => p != null && !string.IsNullOrWhiteSpace(p.content)));
            }
            var r = new
            {
                model = model,
                temperature = temperature,
                max_tokens = maxTokens,
                //让大模型不思考
                reasoning_effort = "none",
                messages = messages.ToArray()
            };
            return Clean(Post(url, apikey, r));
        }

        /// <summary>
        /// 流式多轮对话(支持图片):边收边回调,适合对话界面"打字机"显示。
        /// onDelta 在后台线程被调用,界面层需自行切回 UI 线程。
        /// </summary>
        public static void ChatMixedStream(string url, string model, string apikey, string system,
            List<message> history, Action<string> onDelta, double? temperature = 0.7, int? maxTokens = 2000)
        {
            if (onDelta == null)
            {
                throw new ArgumentNullException(nameof(onDelta));
            }
            var r = new
            {
                model = model,
                temperature = temperature,
                max_tokens = maxTokens,
                //让大模型不思考
                reasoning_effort = "none",
                stream = true,
                messages = BuildMixedMessages(system, history)
            };
            var data = Newtonsoft.Json.JsonConvert.SerializeObject(
                r, new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(NormalizeUrl(url));
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Timeout = 60 * 1000;
            //流式响应是长连接:整体超时给宽松些,避免回答到一半被掐断
            req.ReadWriteTimeout = 180 * 1000;
            req.ServicePoint.Expect100Continue = false;
            req.Headers.Add("Authorization", "Bearer " + apikey);
            var bytes = Encoding.UTF8.GetBytes(data);
            req.ContentLength = bytes.Length;
            using (var reqStream = req.GetRequestStream())
            {
                reqStream.Write(bytes, 0, bytes.Length);
            }

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                var sawData = false;
                //有的网关不认 stream,会直接回一整段 JSON:先攒起来,最后兜底解析
                var raw = new StringBuilder();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        raw.AppendLine(line);
                        continue;
                    }
                    sawData = true;
                    var payload = line.Substring(5).Trim();
                    if (payload.Length == 0)
                    {
                        continue;
                    }
                    if (payload == "[DONE]")
                    {
                        break;
                    }
                    var delta = ParseDelta(payload);
                    if (!string.IsNullOrEmpty(delta))
                    {
                        onDelta(delta);
                    }
                }
                if (sawData)
                {
                    return;
                }
                //兜底:非流式返回
                var full = ParseFull(raw.ToString());
                if (string.IsNullOrWhiteSpace(full))
                {
                    throw new Exception("AI没有返回内容:" + raw);
                }
                onDelta(full);
            }
        }

        /// <summary>
        /// 拼装带图片的多模态消息数组(ChatMixed / ChatMixedStream 共用)
        /// </summary>
        private static object[] BuildMixedMessages(string system, List<message> history)
        {
            var msgs = new List<object>();
            if (!string.IsNullOrWhiteSpace(system))
            {
                msgs.Add(new { role = "system", content = system });
            }
            if (history != null)
            {
                foreach (var m in history)
                {
                    if (m == null)
                    {
                        continue;
                    }
                    var hasImage = m.images != null && m.images.Count > 0;
                    if (string.IsNullOrWhiteSpace(m.content) && !hasImage)
                    {
                        continue;
                    }
                    if (!hasImage)
                    {
                        msgs.Add(new { role = m.role, content = m.content });
                        continue;
                    }
                    var parts = new List<object>();
                    if (!string.IsNullOrWhiteSpace(m.content))
                    {
                        parts.Add(new { type = "text", text = m.content });
                    }
                    foreach (var img in m.images)
                    {
                        parts.Add(new { type = "image_url", image_url = new { url = img } });
                    }
                    msgs.Add(new { role = m.role, content = parts.ToArray() });
                }
            }
            return msgs.ToArray();
        }

        /// <summary>
        /// 取一条流式分片里的正文增量(部分网关还会在流里回错误)
        /// </summary>
        private static string ParseDelta(string payload)
        {
            try
            {
                var jobj = Newtonsoft.Json.Linq.JObject.Parse(payload);
                var err = jobj["error"];
                if (err != null)
                {
                    throw new Exception("AI返回错误:" + (err["message"]?.ToString() ?? err.ToString()));
                }
                var content = jobj["choices"]?[0]?["delta"]?["content"];
                if (content == null)
                {
                    return null;
                }
                if (content.Type == Newtonsoft.Json.Linq.JTokenType.String)
                {
                    return content.ToString();
                }
                //少数网关把 content 拆成 [{type:"text",text:"..."}] 数组
                if (content.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var part in content)
                    {
                        if (string.Equals(part["type"]?.ToString(), "text", StringComparison.OrdinalIgnoreCase))
                        {
                            sb.Append(part["text"]?.ToString());
                        }
                    }
                    return sb.ToString();
                }
                return null;
            }
            catch (Exception ex)
            {
                //错误信息往上抛,分片格式异常则跳过
                if (ex.Message.StartsWith("AI返回错误", StringComparison.Ordinal))
                {
                    throw;
                }
                return null;
            }
        }

        /// <summary>
        /// 兜底:把非流式响应体解析成正文
        /// </summary>
        private static string ParseFull(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }
            try
            {
                var jobj = Newtonsoft.Json.Linq.JObject.Parse(body);
                var err = jobj["error"];
                if (err != null)
                {
                    throw new Exception("AI返回错误:" + (err["message"]?.ToString() ?? err.ToString()));
                }
                return jobj["choices"]?[0]?["message"]?["content"]?.ToString();
            }
            catch (Exception ex)
            {
                if (ex.Message.StartsWith("AI返回错误", StringComparison.Ordinal))
                {
                    throw;
                }
                return null;
            }
        }

        /// <summary>
        /// 统一发送 OpenAI 兼容请求并取出回复正文(异常与"空回复"都在这里抛出)
        /// </summary>
        private static string Post(string url, string apikey, object payload)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            url = NormalizeUrl(url);
            var head = new WebHeaderCollection();
            head.Add("Authorization", "Bearer " + apikey);
            //null 的可选参数不发给服务端(部分网关不认未知字段)
            var data = Newtonsoft.Json.JsonConvert.SerializeObject(
                payload, new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });
            var ret = CoreHttp.Send(url, new CoreHttp.SendParam
            {
                Method = "POST",
                HttpData = data,
                TimeOut = 60 * 1000,
                Headers = head
            });
            var jobj = Newtonsoft.Json.Linq.JObject.Parse(ret);
            var content = jobj["choices"]?[0]?["message"]?["content"]?.ToString();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new Exception("AI没有返回内容:" + ret);
            }
            return content;
        }

        /// <summary>
        /// 归一化对话接口地址:各家服务商给的"接口地址"粒度不一(只给域名、只给到 /v1、或给完整端点),
        /// 这里统一补成 .../chat/completions,避免直接 POST 到 /v1 被网关当成未知路径返回 404
        /// </summary>
        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
            var u = url.Trim().TrimEnd('/');
            if (u.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return u;
            }
            if (u.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return u + "/chat/completions";
            }
            return u + "/v1/chat/completions";
        }

        /// <summary>
        /// 去掉 ``` / ```sql / ```javascript 这类代码块围栏(模型经常无视"不要代码块"的要求)
        /// </summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            var lines = text.Replace("\r\n", "\n").Split('\n')
                .Where(p => !p.TrimStart().StartsWith("```", StringComparison.Ordinal));
            return string.Join(Environment.NewLine, lines).Trim();
        }

        /// <summary>
        /// 语句起点:行首是这些关键词才当作语句开始(避免把解释文字当成语句抓到编辑器里)
        /// </summary>
        private static readonly Regex SqlStartRegex = new Regex(
            @"^\s*(select|insert|update|delete|merge|with|create|alter|drop|truncate|comment|grant|revoke|exec|execute|call|declare|set|use|show|explain|describe|desc|begin)\b",
            RegexOptions.IgnoreCase);

        /// <summary>
        /// 从 AI 回复里挑出可以直接执行的语句:优先取代码块内容(模型偶尔无视"不要```包裹"的要求),
        /// 否则从第一条语句所在行取到结尾;整段都没有语句时返回 null(调用方据此不动编辑器)
        /// </summary>
        public static string ExtractSql(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            var m = Regex.Match(text, "```[a-zA-Z]*\\s*\\r?\\n?([\\s\\S]*?)```");
            if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
            {
                return m.Groups[1].Value.Trim();
            }
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (SqlStartRegex.IsMatch(lines[i]))
                {
                    return string.Join(Environment.NewLine, lines.Skip(i)).Trim();
                }
            }
            return null;
        }

        /// <summary>
        /// 单个字段拼成一行"名称 类型 标记 -- 注释"(表结构与集合结构共用这一种格式,比字段 JSON 省 token 也不易看错)
        /// </summary>
        public static string BuildZiDuanLine(ZiDuan z)
        {
            var sb = new StringBuilder();
            sb.Append("  ").Append(z.Name);
            if (!string.IsNullOrWhiteSpace(z.Type))
            {
                sb.Append(' ').Append(z.Type.Trim());
            }
            if (z.IsPK)
            {
                sb.Append(" PK");
            }
            else if (z.Notnull)
            {
                sb.Append(" NOT NULL");
            }
            if (!string.IsNullOrWhiteSpace(z.Comment))
            {
                sb.Append(" -- ").Append(z.Comment.Trim());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 拼装"生成语句"的用户提示词:数据库方言 + 上下文(库/模式) + 表结构 + 需求
        /// 顺序固定,模型对结构化的输入更不容易漏看条件
        /// </summary>
        public static string BuildQueryPrompt(string dialect, string context, string schemaText, string need)
        {
            var sb = new StringBuilder();
            sb.Append("【数据库】").AppendLine(string.IsNullOrWhiteSpace(dialect) ? "未知" : dialect.Trim());
            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.Append("【上下文】").AppendLine(context.Trim());
            }
            if (!string.IsNullOrWhiteSpace(schemaText))
            {
                sb.AppendLine("【表结构】").AppendLine(schemaText.TrimEnd());
            }
            sb.Append("【需求】").AppendLine((need ?? string.Empty).Trim());
            return sb.ToString();
        }

        public class root
        {
            public string model { get; set; }
            public double? temperature { get; set; }
            public int? max_tokens { get; set; }
            public bool enable_thinking { get; set; }
            public message[] messages { get; set; }
        }

        public class message
        {
            public string role { get; set; }
            public string content { get; set; }

            /// <summary>
            /// 随消息一起发送的图片(data:image/xxx;base64,...)。
            /// 只有走 ChatMixed 时才会转成多模态 content 数组,普通 Chat 会忽略它
            /// </summary>
            public List<string> images { get; set; }
        }
    }
}
