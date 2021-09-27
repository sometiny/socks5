using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace IocpSharp.Socks5
{
    internal class Pac
    {

        private static string ReadHttpHeader(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(), false, 8192, true))
            {
                string firstLine = null;
                string line;
                while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                {
                    if (firstLine == null) firstLine = line;
                }
                return firstLine;
            }
        }
        /// <summary>
        /// 极简的HTTP响应。
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="hostListFile"></param>
        /// <param name="serveAt"></param>
        public static void Process(Stream stream, string hostListFile, string serveAt)
        {
            string firstLine = ReadHttpHeader(stream);
            byte[] body;
            byte[] header;
            if (!firstLine.StartsWith("GET /pac"))
            {
                body = Encoding.UTF8.GetBytes("<h4>404 Not Found</h4>");
                header = Encoding.UTF8.GetBytes($"HTTP/1.1 404 Not Found\r\nContent-Type: text/html\r\nServer: PacServer/1.0\r\nContent-Length:{body.Length}\r\nConnection: close\r\n\r\n");

                stream.Write(header, 0, header.Length);
                stream.Write(body, 0, body.Length);
                stream.Close();
                return;
            }
            string pac = GetPac(hostListFile, serveAt);

            body = Encoding.UTF8.GetBytes(pac);
            header = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nServer: PacServer/1.0\r\nContent-Length:{body.Length}\r\nConnection: close\r\n\r\n");

            stream.Write(header, 0, header.Length);
            stream.Write(body, 0, body.Length);
            stream.Close();
        }

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
