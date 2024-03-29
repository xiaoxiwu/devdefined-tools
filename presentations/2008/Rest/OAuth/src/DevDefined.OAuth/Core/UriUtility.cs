﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace DevDefined.OAuth.Core
{
    public static class UriUtility
    {
        private const string UnreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

        /// <summary>
        /// Internal function to cut out all non oauth query string parameters (all parameters not begining with "oauth_")
        /// </summary>
        /// <param name="parameters">The query string part of the Url</param>
        /// <returns>A list of QueryParameter each containing the parameter name and value</returns>
        public static List<QueryParameter> GetQueryParameters(string parameters)
        {
            if (parameters.StartsWith("?"))
            {
                parameters = parameters.Remove(0, 1);
            }

            var result = new List<QueryParameter>();

            if (!string.IsNullOrEmpty(parameters))
            {
                string[] p = parameters.Split('&');
                foreach (string s in p)
                {
                    if (!string.IsNullOrEmpty(s) && !s.StartsWith(Parameters.OAuthParameterPrefix))
                    {
                        if (s.IndexOf('=') > -1)
                        {
                            string[] temp = s.Split('=');
                            result.Add(new QueryParameter(temp[0], temp[1]));
                        }
                        else
                        {
                            result.Add(new QueryParameter(s, string.Empty));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// This is a different Url Encode implementation since the default .NET one outputs the percent encoding in lower case.
        /// While this is not a problem with the percent encoding spec, it is used in upper case throughout OAuth
        /// </summary>
        /// <param name="value">The value to Url encode</param>
        /// <returns>Returns a Url encoded string</returns>
        public static string UrlEncode(string value)
        {
            if (value == null) return null;

            var result = new StringBuilder();

            foreach (char symbol in value)
            {
                if (UnreservedChars.IndexOf(symbol) != -1)
                {
                    result.Append(symbol);
                }
                else
                {
                    result.Append('%' + String.Format("{0:X2}", (int) symbol));
                }
            }

            return result.ToString();
        }

        public static string FormatQueryStringWithUrlEncoding(NameValueCollection parameters)
        {
            var builder = new StringBuilder();

            foreach (string key in parameters.Keys)
            {
                if (builder.Length > 0) builder.Append("&");
                builder.Append(key).Append("=");

                string urlEncoded = UrlEncode(parameters[key]);
                if (!key.StartsWith("oauth_", StringComparison.InvariantCultureIgnoreCase))
                {
                    urlEncoded = UrlEncode(urlEncoded);
                }

                builder.Append(urlEncoded);
            }

            return builder.ToString();
        }

        public static string FormatQueryString(NameValueCollection parameters)
        {
            var builder = new StringBuilder();

            foreach (string key in parameters.Keys)
            {
                if (builder.Length > 0) builder.Append("&");
                builder.Append(key).Append("=").Append(UrlEncode(parameters[key]));
            }

            return builder.ToString();
        }

        public static string FormatParameters(string httpMethod, Uri url)
        {
            return FormatParameters(httpMethod, url, GetQueryParameters(url.Query));
        }

        public static string FormatParameters(string httpMethod, Uri url, List<QueryParameter> parameters)
        {
            string normalizedRequestParameters = NormalizeRequestParameters(parameters);

            var signatureBase = new StringBuilder();
            signatureBase.AppendFormat("{0}&", httpMethod.ToUpper());

            signatureBase.AppendFormat("{0}&", UrlEncode(NormalizeUri(url)));
            signatureBase.AppendFormat("{0}", UrlEncode(normalizedRequestParameters));

            return signatureBase.ToString();
        }

        /// <summary>
        /// Normalizes the request parameters according to the spec
        /// </summary>
        /// <param name="parameters">The list of parameters already sorted</param>
        /// <returns>a string representing the normalized parameters</returns>
        public static string NormalizeRequestParameters(IList<QueryParameter> parameters)
        {
            IEnumerable<QueryParameter> orderedParameters = parameters
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Value)
                .Select(
                x =>
                (x.Name.StartsWith(Parameters.OAuthParameterPrefix))
                    ? x
                    : new QueryParameter(x.Name, UrlEncode(x.Value)));

            var sb = new StringBuilder();

            foreach (QueryParameter p in orderedParameters)
            {
                if (sb.Length > 0) sb.Append("&");

                sb.Append(p.Name).Append("=").Append(p.Value);
            }

            return sb.ToString();
        }

        public static string NormalizeUri(Uri uri)
        {
            string normalizedUrl = string.Format("{0}://{1}", uri.Scheme, uri.Host);

            if (!((uri.Scheme == "http" && uri.Port == 80) ||
                  (uri.Scheme == "https" && uri.Port == 443)))
            {
                normalizedUrl += ":" + uri.Port;
            }

            return normalizedUrl + ((uri.AbsolutePath == "/") ? "" : uri.AbsolutePath);
        }

        public static IEnumerable<QueryParameter> ToQueryParameters(this NameValueCollection source)
        {
            foreach (string key in source.AllKeys)
            {
                yield return new QueryParameter(key, source[key]);
            }
        }

        public static NameValueCollection ToNameValueCollection(this IEnumerable<QueryParameter> source)
        {
            var collection = new NameValueCollection();

            foreach (QueryParameter parameter in source)
            {
                collection[parameter.Name] = parameter.Value;
            }

            return collection;
        }
    }
}