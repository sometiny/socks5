using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace IocpSharp.Socks5.Commands
{
    public class ConnectCommand : ICommand
    {
        private Stream _clientStream = null;
        private Stream _remoteStream = null;
        public void Handle(Stream requestStream, ProxyRequest request)
        {

            _clientStream = requestStream;
            //连接远程服务器
            Stream remoteStream = ConnectRemote(request.RemoteEndPoint, out IPEndPoint connectedEndPoint);


            //连接成功后，发送响应数据到客户端
            SendConnectResult(connectedEndPoint);

            //协议结束，客户端接收到响应后，开始正常收发数据，服务器唯一需要做的就是转发数据
            //这里用异步方式处理
            _remoteStream = remoteStream;

            //开始对拷，不需要缓冲区了，多此一举
            if (_clientStream is BufferedNetworkStream buffered) buffered.Buffered = false;

            _clientStream.CopyToAsync(_remoteStream).ContinueWith(copyFinished);
            _remoteStream.CopyToAsync(_clientStream).ContinueWith(copyFinished);
        }

        private int _completed = 0;

        private void copyFinished(Task task)
        {
            if (System.Threading.Interlocked.Increment(ref _completed) == 2)
            {
                _clientStream?.Close();
                _remoteStream?.Close();
                _clientStream = null;
                _remoteStream = null;
                GC.SuppressFinalize(this);
            }
        }
        /// <summary>
        /// 发送连接成功的响应到客户端
        /// </summary>
        /// <param name="stream">客户端流</param>
        /// <param name="endpoint">连接成功的终结点</param>
        private void SendConnectResult(IPEndPoint endpoint)
        {

            IPAddress ipaddress = endpoint.Address;
            byte[] addressBuffer = ipaddress.GetAddressBytes();
            int responseLength = 4 + 2 + addressBuffer.Length;

            byte[] response = new byte[responseLength];

            //协议版本
            response[0] = 0x05;

            //00代表连接成功
            response[1] = 0x00;

            //保留字段
            response[2] = 0;

            //地址类型，IPv4或IPv6
            response[3] = (byte)(ipaddress.AddressFamily == AddressFamily.InterNetwork ? 0x01 : 0x04);

            //写入IP以及端口，告诉客户端，服务器实际连接的IP和端口
            addressBuffer.CopyTo(response, 4);

            response[responseLength - 1] = (byte)(endpoint.Port & 0xff);
            response[responseLength - 2] = (byte)((endpoint.Port >> 8) & 0xff);

            _clientStream.Write(response, 0, responseLength);
        }

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
