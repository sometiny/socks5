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
            Stream remoteStream = Utils.ConnectRemote(request.RemoteEndPoint, out IPEndPoint connectedEndPoint);


            //连接成功后，发送响应数据到客户端
            SendConnectResult(connectedEndPoint);

            //协议结束，客户端接收到响应后，开始正常收发数据，服务器唯一需要做的就是转发数据
            //这里用异步方式处理
            _remoteStream = remoteStream;

            //开始对拷，不需要缓冲区了，多此一举
            if (_clientStream is BufferedNetworkStream buffered) buffered.Buffered = false;

            _clientStream.CopyToAsync(_remoteStream).ContinueWith(CopyFinished);
            _remoteStream.CopyToAsync(_clientStream).ContinueWith(CopyFinished);
        }

        private int _completed = 0;

        private void CopyFinished(Task task)
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
    }
}
