using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace IocpSharp.Socks5
{
    internal class PacLoader
    {
        public static string Script = $@"var proxy = 'SOCKS5 {{SERVE_AT}};';
var direct = 'DIRECT;';
var hasOwnProperty = Object.hasOwnProperty;

function FindProxyForURL(url, host) {{
    var suffix;
    var pos = host.lastIndexOf('.');
    pos = host.lastIndexOf('.', pos - 1);
    while(1) {{
        if (pos <= 0) {{
            if (hasOwnProperty.call(domains, host)) {{
                return proxy;
            }} else {{
                return direct;
            }}
        }}
        suffix = host.substring(pos + 1);
        if (hasOwnProperty.call(domains, suffix)) {{
            return proxy;
        }}
        pos = host.lastIndexOf('.', pos - 1);
    }}
}}";
        private static string _pacList = null;

        public static void Reset()
        {
            _pacList = null;
        }

        public static string GetPac(string file, string serveAt)
        {
            if (_pacList != null)
            {
                return _pacList;
            }
            var lines = File.ReadAllLines(file);
            lines = lines.Where(t => !string.IsNullOrEmpty(t)).Distinct().Select(t => "  \"" + t.ToLower() + "\" : 1").ToArray();
            StringBuilder sb = new StringBuilder();
            sb.Append("var domains = {\r\n");
            sb.Append(string.Join(", \r\n", lines));
            sb.Append("\r\n};\r\n");
            sb.Append(Script.Replace("{SERVE_AT}", serveAt));
            return _pacList = sb.ToString();
        }
    }
}
