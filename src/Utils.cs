using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;


namespace IocpSharp.Socks5
{
    public class Utils
    {

        /// <summary>
        /// 连接远程服务器
        /// </summary>
        /// <param name="endpoint">待连接的远程结点</param>
        /// <param name="connectedEndPoint">连接成功的远程结点</param>
        /// <returns></returns>
        public static Stream ConnectRemote(EndPoint endpoint, out IPEndPoint connectedEndPoint)
        {
            if (endpoint is DnsEndPoint dnsEndPoint)
            {
                //解析主机
                endpoint = new IPEndPoint(ResolveDnsHost(dnsEndPoint.Host), dnsEndPoint.Port);
            }

            Socket remoteSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            remoteSocket.NoDelay = true;
            remoteSocket.Connect(endpoint);

            connectedEndPoint = endpoint as IPEndPoint;

            return new NetworkStream(remoteSocket, true);
        }

        /// <summary>
        /// 解析主机
        /// </summary>
        /// <param name="host">主机名</param>
        /// <param name="ipV6Prior">是否优先使用IPv6地址</param>
        /// <returns></returns>
        public static IPAddress ResolveDnsHost(string host, bool ipV6Prior = false)
        {
            IPAddress[] address = Dns.GetHostAddresses(host);
            if (address == null || address.Length == 0) throw new SocketException(11001); //SocketError.HostNotFound

            if (address.Length == 1) return address[0];

            if (ipV6Prior) return address.OrderByDescending(t => t.AddressFamily).First();

            return address.OrderBy(t => t.AddressFamily).First();
        }
    }
}
