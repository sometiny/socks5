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
            if (!firstLine.StartsWith("GET /pac"))
            {
                SendResponse(stream, 404, "Not Found", "<h4>404 Not Found</h4>");
                return;
            }
            string pac = PacLoader.GetPac(hostListFile, serveAt);

            SendResponse(stream, 200, "OK", pac, "text/plain");
        }

        private static void SendResponse(Stream stream, int statusCode, string statusText, string body, string contentType = "text/html")
        {
            SendResponse(stream, statusCode, statusText, Encoding.UTF8.GetBytes(body), contentType);
        }

        private static void SendResponse(Stream stream, int statusCode, string statusText, byte[] body, string contentType = "text/html")
        {
            byte[] header = Encoding.UTF8.GetBytes($"HTTP/1.1 {statusCode} {statusText}\r\nContent-Type: {contentType}\r\nServer: PacServer/1.0\r\nContent-Length:{body.Length}\r\nConnection: close\r\n\r\n");

            stream.Write(header, 0, header.Length);
            stream.Write(body, 0, body.Length);
            stream.Close();
        }
    }
}
