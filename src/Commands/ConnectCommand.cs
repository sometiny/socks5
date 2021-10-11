using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;

namespace IocpSharp.Socks5.Commands
{
    public class ConnectCommand : Command
    {
        private Stream _clientStream = null;
        private Stream _remoteStream = null;
        public override void Handle(Stream requestStream, ProxyRequest request)
        {

            _clientStream = requestStream;
            //连接远程服务器
            Stream remoteStream = Utils.ConnectRemote(request.RemoteEndPoint, out IPEndPoint connectedEndPoint);


            //连接成功后，发送响应数据到客户端
            SendConnectResult(connectedEndPoint, _clientStream);

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
    }
}
