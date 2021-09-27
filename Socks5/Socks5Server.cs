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
        /// <summary>
        /// 实现NewClient方法，处理Socks5请求
        /// </summary>
        /// <param name="client"></param>
        protected override void NewClient(Socket client)
        {
            ProtocolExchanger exchanger = new ProtocolExchanger();
            try
            {
                //实例化NetWorkStream，让实例化NetWorkStream拥有基础Socket的处理权限
                exchanger.Start(new BufferedNetworkStream(client, true));
            }
            catch
            {
                //异常，销毁
                exchanger.Dispose();
            }
        }
    }
}
