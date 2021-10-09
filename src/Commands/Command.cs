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
