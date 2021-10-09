using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace IocpSharp.Socks5.Commands
{

    internal class RemoteConnectArgs
    {
        public TcpSocketAsyncEventArgs TcpSocketAsyncEventArgs { get; set; }
        public Socket Client { get; set; }
    }
    /// <summary>
    /// 直接继承SocketAsyncEventArgs，作为端口映射服务器
    /// </summary>
    public class PortTransfer : SocketAsyncEventArgs
    {
        private Socket _socket = null;
        private IPEndPoint _localEndPoint = null;
        private EndPoint _remoteEndPoint = null;

        /// <summary>
        /// 本地监听终结点
        /// </summary>
        public IPEndPoint LocalEndPoint => _localEndPoint;

        /// <summary>
        /// 实例化服务器
        /// </summary>
        public PortTransfer() : base(){}

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <returns>true成功，false失败</returns>
        protected virtual void Start()
        {
            if (_localEndPoint == null) throw new ArgumentNullException("LocalEndPoint");

            _socket = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _socket.Bind(_localEndPoint);
            _socket.Listen(256);
            _localEndPoint = _socket.LocalEndPoint as IPEndPoint;

            StartAccept();
        }

        /// <summary>
        /// 使用本地终结点启动服务器
        /// </summary>
        /// <param name="localEndPoint">本地终结点</param>
        /// <returns></returns>
        public void Start(EndPoint localEndPoint, EndPoint remoteEndPoint)
        {
            _localEndPoint = localEndPoint as IPEndPoint;
            _remoteEndPoint = remoteEndPoint;
            Start();
        }
        /// <summary>
        /// 停止服务器
        /// </summary>
        public virtual void Stop()
        {
            try
            {
                _socket?.Close();
                Dispose();
            }
            catch { }
            InternalRelease();
        }

        private int _released = 0;
        private void InternalRelease() {
            if (Interlocked.CompareExchange(ref _released, 1, 0) == 1) return;
            Release();
        }
        protected virtual void Release(){ }
        /// <summary>
        /// 开始接受客户端请求
        /// </summary>
        private void StartAccept()
        {
            AcceptSocket = null;

            try
            {
                if (!_socket.AcceptAsync(this))
                {
                    OnCompleted(this);
                }
            }
            catch (SocketException e)
            {
                Stop();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// 重写OnCompleted方法
        /// </summary>
        /// <param name="e"></param>
        protected sealed override void OnCompleted(SocketAsyncEventArgs e)
        {
            if (SocketError != SocketError.Success)
            {
                if (SocketError == SocketError.OperationAborted) return;
                Stop();
                return;
            }

            Socket client = AcceptSocket;
            client.NoDelay = true;
            Stop();
            var connection = TcpSocketAsyncEventArgs.Pop();
            connection.ConnectAsync(_remoteEndPoint, AfterRemoteConnect, new RemoteConnectArgs { Client = client, TcpSocketAsyncEventArgs = connection });
        }

        private void AfterRemoteConnect(int err, Socket connectSocket, object state)
        {
            RemoteConnectArgs args = state as RemoteConnectArgs;
            try
            {
                if (err > 0)
                {
                    args.Client.Shutdown(SocketShutdown.Both);
                    args.Client.Close();
                    return;
                }
                _clientStream = new NetworkStream(args.Client, true);
                _remoteStream = new NetworkStream(connectSocket, true);
                _clientStream.CopyToAsync(_remoteStream).ContinueWith(CopyFinished);
                _remoteStream.CopyToAsync(_clientStream).ContinueWith(CopyFinished);
            }
            catch { }
            finally
            {

                args.Client = null;
                TcpSocketAsyncEventArgs.Push(args.TcpSocketAsyncEventArgs);
            }
        }
        private NetworkStream _clientStream = null;
        private NetworkStream _remoteStream = null;
        private int _completed = 0;

        private void CopyFinished(Task task)
        {
            if (Interlocked.Increment(ref _completed) == 2)
            {
                _clientStream?.Close();
                _remoteStream?.Close();
                _clientStream = null;
                _remoteStream = null;
            }
        }
    }
}
