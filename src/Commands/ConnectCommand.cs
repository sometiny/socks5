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
    public class ConnectCommand : Command
    {
        private class RemoteConnectArgs
        {
            private TcpSocketAsyncEventArgs _tcpSocketAsyncEventArgs;
            private Stream _clientStream;
            private ProxyRequest _request;

            public TcpSocketAsyncEventArgs TcpSocketAsyncEventArgs => _tcpSocketAsyncEventArgs;
            public Stream ClientStream => _clientStream;
            public ProxyRequest Request => _request;
            public RemoteConnectArgs(TcpSocketAsyncEventArgs e, Stream clientStream, ProxyRequest request)
            {
                _tcpSocketAsyncEventArgs = e;
                _clientStream = clientStream;
                _request = request;
            }

            ~RemoteConnectArgs()
            {
                _tcpSocketAsyncEventArgs = null;
                _clientStream = null;
            }
        }
        public override void Handle(Stream requestStream, ProxyRequest request)
        {
            TcpSocketAsyncEventArgs connector = TcpSocketAsyncEventArgs.Pop();

            connector.ConnectAsync(request.RemoteEndPoint, AfterConnect, new RemoteConnectArgs(connector, requestStream, request));

        }

        /// <summary>
        ///异步连接结果
        /// </summary>
        /// <param name="error"></param>
        /// <param name="connectSocket"></param>
        /// <param name="state"></param>
        private void AfterConnect(int error, Socket connectSocket, object state) {
            RemoteConnectArgs args = state as RemoteConnectArgs;
            TcpSocketAsyncEventArgs.Push(args.TcpSocketAsyncEventArgs);
            if (error > 0)
            {
                SendConnectResult(new IPEndPoint(IPAddress.Any, 0), args.ClientStream, 0x05);
                args.ClientStream.Close();
                return;
            }
            IPEndPoint endpoint = connectSocket.RemoteEndPoint as IPEndPoint;
            if (endpoint.Address.IsIPv4MappedToIPv6) endpoint = new IPEndPoint(endpoint.Address.MapToIPv4(), endpoint.Port);

            //连接成功后，发送响应数据到客户端
            SendConnectResult(endpoint, args.ClientStream);

            //协议结束，客户端接收到响应后，开始正常收发数据，服务器唯一需要做的就是转发数据
            Copy(args.ClientStream, new IocpNetworkStream(connectSocket, true));
        }
    }
}
