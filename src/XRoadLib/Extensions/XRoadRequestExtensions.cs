using System.Linq;

#if NETSTANDARD1_5
using Microsoft.AspNetCore.Http;
using System.Net;
#else
using System.Collections.Specialized;
#endif

namespace XRoadLib.Extensions
{
    public static class XRoadRequestExtensions
    {
#if NETSTANDARD1_5
        public static string GetContentTypeHeader(this IHeaderDictionary headers)
        {
            var contentTypeKey = headers?.Keys.FirstOrDefault(key => key.Trim().ToLower().Equals("content-type"));
            return contentTypeKey == null ? "text/xml; charset=UTF-8" : headers?[contentTypeKey].ToString();
        }

        public static string GetContentTypeHeader(this WebHeaderCollection headers)
        {
            var contentTypeKey = headers?.AllKeys.FirstOrDefault(key => key.Trim().ToLower().Equals("content-type"));
            return contentTypeKey == null ? "text/xml; charset=UTF-8" : headers?[contentTypeKey].ToString();
        }
#else
        public static string GetContentTypeHeader(this NameValueCollection headers)
        {
            var contentTypeKey = headers?.AllKeys.FirstOrDefault(key => key.Trim().ToLower().Equals("content-type"));
            return contentTypeKey == null ? "text/xml; charset=UTF-8" : headers?[contentTypeKey];
        }
#endif
    }
}