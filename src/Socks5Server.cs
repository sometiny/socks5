using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Net.Sockets;
using IocpSharp.Server;

namespace IocpSharp.Socks5
{
    public class Socks5Server : TcpIocpServer
    {
        private string _hostListFile = null;
        private string _serveAt = null;

        public string HostListFile => _hostListFile;
        public string ServeAt => _serveAt;

        public Socks5Server(string hostListFile, string serveAt = null) : base()
        {
            _hostListFile = hostListFile ?? throw new ArgumentNullException(hostListFile);
            _serveAt = serveAt;
        }

        /// <summary>
        /// 实现NewClient方法，处理Socks5请求
        /// </summary>
        /// <param name="client"></param>
        protected override void NewClient(Socket client)
        {
            if (string.IsNullOrEmpty(_serveAt))
            {
                _serveAt = $"127.0.0.1:{LocalEndPoint.Port}";
            }

            //实例化的是BufferedNetworkStream，可控制数据的消费和缓冲区
            BufferedNetworkStream stream = new BufferedNetworkStream(client, true);
            try
            {
                //先设置基础流不消费读取到的缓冲区
                stream.Consume = false;
                int firstByte = stream.ReadByte();

                //开始消费缓冲区
                stream.Consume = true;

                //第一个字节不为0x05，代表不是Socks5协议，我们全部作为PAC服务器处理
                if (firstByte != 0x05)
                {
                    Pac.Handle(stream, _hostListFile, _serveAt);
                    return;
                }

                Negotiator.Handle(stream);
            }
            catch
            {
                stream.Close();
            }
        }
    }
}
