using System;
using System.IO;
using System.Net;
using System.Text;

namespace WDB.Common
{

    /// <summary>
    /// Http通信核心
    /// </summary>
    public static class CoreHttp
    {

        /// <summary>
        /// Http发送参数
        /// </summary>
        public class SendParam
        {

            /// <summary>
            /// 协议标头
            /// </summary>
            public WebHeaderCollection Headers { get; set; }

            /// <summary>
            /// 提交方法
            /// </summary>
            public string Method { get; set; } = "GET";

            /// <summary>
            /// 请求参数 例:{"value": "HttpSend"} C#格式(@"{""value"":""HttpSend""}")
            /// </summary>
            public string HttpData { get; set; } = null;

            /// <summary>
            /// 超时时间(单位:毫秒)
            /// </summary>
            public int TimeOut { get; set; } = 60000;

            /// <summary>
            /// 编码
            /// </summary>
            public string Encoding { get; set; } = "UTF-8";

            /// <summary>
            /// 内容类型(例:text/xml application/json)
            /// </summary>
            public string ContentType { get; set; } = "application/json";

            /// <summary>
            /// http标头Referer
            /// </summary>
            public string Referer { get; set; } = null;

        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="httpUri">请求地址</param>
        /// <param name="sendParam">发送参数</param>
        /// <returns></returns>
        public static string Send(string httpUri, SendParam sendParam = null)
        {
            HttpWebRequest httpWebRequest = null;
            HttpWebResponse httpWebResponse = null;
            Stream reqStream = null;
            Stream resStream = null;
            if (sendParam == null)
            {
                sendParam = new SendParam();
            }
            try
            {
                httpWebRequest = (HttpWebRequest)WebRequest.Create(httpUri);
                if (sendParam.Headers != null)
                {
                    httpWebRequest.Headers = sendParam.Headers;
                }
                httpWebRequest.Method = sendParam.Method;
                httpWebRequest.Timeout = sendParam.TimeOut;
                httpWebRequest.ContentType = sendParam.ContentType;
                if (!string.IsNullOrEmpty(sendParam.Referer))
                {
                    httpWebRequest.Referer = sendParam.Referer;
                }
                if (sendParam.Method != "GET")
                {
                    //这个在非GET的时候，一定要加上，如果服务器返回错误，他还会继续再去请求，不会使用之前的错误数据做返回数据
                    httpWebRequest.ServicePoint.Expect100Continue = false;
                    if (string.IsNullOrEmpty(sendParam.HttpData))
                    {
                        httpWebRequest.ContentLength = 0;
                    }
                    else
                    {
                        var data = Encoding.UTF8.GetBytes(sendParam.HttpData);
                        httpWebRequest.ContentLength = data.Length;
                        reqStream = httpWebRequest.GetRequestStream();
                        reqStream.Write(data, 0, data.Length);
                        reqStream.Close();
                    }
                }
                var webResponse = httpWebRequest.GetResponse();
                httpWebResponse = (HttpWebResponse)webResponse;
                resStream = httpWebResponse.GetResponseStream();
                string result;
                if (resStream != null)
                {
                    var streamReader = new StreamReader(resStream, Encoding.GetEncoding(sendParam.Encoding));
                    result = streamReader.ReadToEnd();
                    streamReader.Close();
                    resStream.Close();
                }
                else
                {
                    result = string.Empty;
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("CoreHttp.HttpSend Err: method=>" +
                                    sendParam.Method + " uri=>" + httpUri + " httpData=>" + sendParam.HttpData + "err=>" + ex);
            }
            finally
            {
                resStream?.Close();
                reqStream?.Close();
                httpWebResponse?.Close();
                httpWebRequest?.Abort();
            }
        }
    }
}
