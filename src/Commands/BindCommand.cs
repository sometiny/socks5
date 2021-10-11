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
    public class BindCommand : Command
    {
        private Stream _clientStream = null;
        private Socket _listenSocket = null;
        private int _port = 0;
        public override void Handle(Stream requestStream, ProxyRequest request)
        {
            if (!PortManager.TryPop(out int port))
            {
                //没有可用端口
                SendConnectResult(new IPEndPoint(IPAddress.Any, port), requestStream, 0x05);
                requestStream.Close();
                return;
            }
            _port = port;
            _clientStream = requestStream;

            try
            {
                Listen();
                //连接成功后，发送响应数据到客户端
                SendConnectResult(new IPEndPoint(IPAddress.Parse("127.0.0.1"), port), requestStream);
            }
            catch
            {
                SendConnectResult(new IPEndPoint(IPAddress.Any, port), requestStream, 0x05);
                PortManager.Push(_port);
                Dispose();
            }
        }

        private void Listen()
        {
            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                _listenSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
                _listenSocket.Listen(256);
                _listenSocket.BeginAccept(AfterAccept, null);
            }
            catch { ListenSuccessOrFailed(); }
        }

        private void AfterAccept(IAsyncResult asyncResult)
        {
            Socket socket;
            try
            {
                socket = _listenSocket.EndAccept(asyncResult);
            }
            catch
            {
                Dispose();
                return;
            }
            finally
            {
                PortManager.Push(_port);
                ListenSuccessOrFailed();
            }

            try
            {
                SendConnectResult(socket.RemoteEndPoint as IPEndPoint, _clientStream);

                Copy(_clientStream, new NetworkStream(socket, true));
            }
            catch { 
                Dispose();
            }
        }

        private void ListenSuccessOrFailed()
        {
            _listenSocket?.Close();
            _listenSocket = null;
        }

        private void Dispose()
        {
            _clientStream?.Close();
            _clientStream = null;
        }
        
    }
}
