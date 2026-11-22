using System;
using System.Collections.Generic;
using System.Globalization;

namespace HysteriaLite
{
    public sealed class HysteriaProfile
    {
        public string Name { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string Auth { get; set; }
        public string Sni { get; set; }
        public bool Insecure { get; set; }
        public string ObfsPassword { get; set; }

        public HysteriaProfile()
        {
            Port = 443;
            Sni = "www.apple.com";
            Insecure = true;
        }

        public string ServerText
        {
            get { return Host + ":" + Port.ToString(CultureInfo.InvariantCulture); }
        }

        public static HysteriaProfile Parse(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new FormatException("链接为空");
            url = url.Trim();
            if (!url.StartsWith("hysteria2://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("hy2://", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("只支持 hysteria2:// 链接");

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                throw new FormatException("链接格式不正确");

            string auth = Uri.UnescapeDataString(uri.UserInfo ?? string.Empty);
            if (string.IsNullOrWhiteSpace(auth))
                throw new FormatException("缺少密码");

            Dictionary<string, string> query = ParseQuery(uri.Query);
            string sni = Query(query, "sni");
            string insecure = Query(query, "insecure");
            string obfs = Query(query, "obfs");
            string obfsPassword = Query(query, "obfs-password");
            string name = Uri.UnescapeDataString((uri.Fragment ?? string.Empty).TrimStart('#'));

            if (string.IsNullOrWhiteSpace(sni))
                sni = "www.apple.com";

            return new HysteriaProfile
            {
                Name = string.IsNullOrWhiteSpace(name) ? uri.Host : name,
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 443,
                Auth = auth,
                Sni = sni,
                Insecure = string.IsNullOrEmpty(insecure) || insecure == "1" || insecure.Equals("true", StringComparison.OrdinalIgnoreCase),
                ObfsPassword = string.Equals(obfs, "salamander", StringComparison.OrdinalIgnoreCase) ? (obfsPassword ?? string.Empty) : string.Empty
            };
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
                return result;
            foreach (string part in query.TrimStart('?').Split('&'))
            {
                if (part.Length == 0)
                    continue;
                int index = part.IndexOf('=');
                string key = index >= 0 ? part.Substring(0, index) : part;
                string value = index >= 0 ? part.Substring(index + 1) : string.Empty;
                result[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
            }
            return result;
        }

        private static string Query(Dictionary<string, string> query, string key)
        {
            string value;
            return query.TryGetValue(key, out value) ? value : null;
        }
    }
}
