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
    public abstract class Command
    {
        private Stream _clientStream = null;
        private Stream _remoteStream = null;
        private int _completed = 0;

        /// <summary>
        /// 处理方法
        /// </summary>
        /// <param name="requestStream"></param>
        /// <param name="request"></param>
        public abstract void Handle(Stream requestStream, ProxyRequest request);

        /// <summary>
        /// 对拷数据
        /// </summary>
        /// <param name="localStream"></param>
        /// <param name="remoteStream"></param>
        protected void Copy(Stream localStream, Stream remoteStream)
        {
            _clientStream = localStream;
            _remoteStream = remoteStream;

            //开始对拷，不需要缓冲区了
            DisableBuffered(_clientStream);
            DisableBuffered(_remoteStream);

            _clientStream.CopyToAsync(_remoteStream).ContinueWith(CopyFinished);
            _remoteStream.CopyToAsync(_clientStream).ContinueWith(CopyFinished);
        }

        /// <summary>
        /// 禁用BufferedNetworkStream的Buffered功能
        /// </summary>
        /// <param name="stream"></param>
        private void DisableBuffered(Stream stream)
        {
            if (stream is BufferedNetworkStream buffered) buffered.Buffered = false;
        }

        /// <summary>
        /// 一方拷贝完成
        /// </summary>
        /// <param name="task"></param>
        private void CopyFinished(Task task)
        {
            if (System.Threading.Interlocked.Increment(ref _completed) == 2)
            {
                _clientStream?.Close();
                _remoteStream?.Close();
                _clientStream = null;
                _remoteStream = null;
            }
        }
        /// <summary>
        /// 发送响应到客户端
        /// </summary>
        /// <param name="endpoint">连接成功的终结点</param>
        /// <param name="requestStream">客户端流</param>
        /// <param name="status">状态</param>
        protected void SendConnectResult(IPEndPoint endpoint, Stream requestStream, byte status = 0x00)
        {

            IPAddress ipaddress = endpoint.Address;
            byte[] addressBuffer = ipaddress.GetAddressBytes();
            int responseLength = 4 + 2 + addressBuffer.Length;

            byte[] response = new byte[responseLength];

            //协议版本
            response[0] = 0x05;

            //00代表连接成功
            response[1] = status;

            //保留字段
            response[2] = 0;

            //地址类型，IPv4或IPv6
            response[3] = (byte)(ipaddress.AddressFamily == AddressFamily.InterNetwork ? 0x01 : 0x04);

            //写入IP以及端口，告诉客户端，服务器实际连接的IP和端口
            addressBuffer.CopyTo(response, 4);

            response[responseLength - 1] = (byte)(endpoint.Port & 0xff);
            response[responseLength - 2] = (byte)((endpoint.Port >> 8) & 0xff);

            requestStream.Write(response, 0, responseLength);
        }
    }
}
