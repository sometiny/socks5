using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Net.Sockets;
using IocpSharp.Socks5.Commands;

namespace IocpSharp.Socks5
{
    class Negotiator
    {
        public static void Handle(Stream stream)
        {
            //开始进行协议交换，读取头部信息
            StartNegotiate(stream);

            //开始读取代理请求，返回一个需要代理的远程终结点
            ProxyRequest request = ReadRequest(stream);

            Command command = request.Command switch
            {
                RequestCommand.CONNECT => new ConnectCommand(),
                _ => null
            };

            if (command != null)
            {
                command.Handle(stream, request);
                return;
            }
            stream.Close();
        }
        /// <summary>
        /// 读取代理请求
        /// </summary>
        /// <param name="stream">客户端数据流</param>
        /// <returns></returns>
        private static ProxyRequest ReadRequest(Stream stream)
        {
            //创建一个足够大的缓冲区
            byte[] buffer = new byte[512];
            ReadPackage(stream, buffer, 0, 5);
            ProxyRequest request = new ProxyRequest();
            //第一个字节，版本号
            request.Version = buffer[0];

            /*
             * 第二个字节，命令类型，这里我们处理CONNECT方法
             * 0x01 CONNECT 连接上游服务器
             * 0x02 BIND 绑定，客户端会接收来自代理服务器的链接，著名的FTP被动模式
             * 0x03 UDP ASSOCIATE UDP中继
             */
            request.Command = (RequestCommand)buffer[1];

            //保留字节
            request.Rsv = buffer[2];

            /*
             * 地址类型
             * 0x01 IP V4地址
             * 0x03 域名
             * 0x04 IP V6地址
             * 
             * 目前为止，从版本号，到地理类型，我们用到了四个字节
             */
            AddressType addressType = request.AddressType = (AddressType)buffer[3];
            int hostLength = 0;
            if (addressType == AddressType.Host)
            {
                hostLength = buffer[4];
            }

            /*
             * 确认后续需要读取的数据长度
             * 地址类型为0x03 时，读取长度为hostLength
             * 地址类型为0x01 IPv4地址时，读取长度为3，其中第一个字节我们已经提前读取了，即buffer[4]。
             * 地址类型为0x04 IPv6地址时，读取长度为15，其中第一个字节我们已经提前读取了，即buffer[4]。
             */
            int remainLength = addressType == AddressType.Host ? hostLength : (addressType == AddressType.IpV4 ? 3 : 15);

            //同时，端口号为固定两位，可以直接读取
            remainLength += 2;

            /*从偏移为5开始，读取所有需要的数据*/
            ReadPackage(stream, buffer, 5, remainLength);

            /*至此，总读取的数据长度如下*/
            int totalLength = remainLength + 5;

            /*
             * 端口号为最后两个字节，我们先读出来。
             * 传输顺序为网络字节序，高位在前
             */
            int destPort = (buffer[totalLength - 2] << 8) | buffer[totalLength - 1];

            //地址类型为主机，返回DnsHostEndPoint
            if (addressType == AddressType.Host)
            {
                request.RemoteEndPoint = new DnsEndPoint(Encoding.ASCII.GetString(buffer, 5, hostLength), destPort);
                return request;
            }

            /*确认IP地址长度，IPv4是4个字节，IPv6是16个字节*/
            byte[] ipBuffer = new byte[addressType == AddressType.IpV4 ? 4 : 16];

            /*
             * 从缓冲区中把IP数据读出来
             * 注意，读取索引为4
             */
            Array.Copy(buffer, 4, ipBuffer, 0, ipBuffer.Length);

            request.RemoteEndPoint = new IPEndPoint(new IPAddress(ipBuffer), destPort);
            return request;
        }

        /// <summary>
        /// 开始认证
        /// </summary>
        /// <param name="stream">客户端数据流</param>
        private static void StartNegotiate(Stream stream)
        {
            //从客户端读取数据，20字节为保守大小
            byte[] header = new byte[20];

            //首先读取两字节
            ReadPackage(stream, header, 0, 2);

            //第一个字节为版本号，固定为5
            byte version = header[0];

            //第二个字节代表支持的方法数量
            byte nMethods = header[1];

            if (nMethods > 18)
            {
                //缓冲区不够用的话，扩展
                byte[] tempBuffer = new byte[nMethods + 2];
                Array.Copy(header, 0, tempBuffer, 0, 2);
                header = tempBuffer;
            }

            /*
             * 读取所有客户端支持的方法
             * 0x00 不需要认证（常用）
             * 0x01 GSSAPI认证
             * 0x02 账号密码认证（常用）
             * 0x03 - 0x7F IANA分配
             * 0x80 - 0xFE 私有方法保留
             * 0xFF 无支持的认证方法
             */
            ReadPackage(stream, header, 2, nMethods);


            //服务器选择不需要认证，发送响应数据到客户端
            byte[] response = new byte[] {
                0x5, /*版本号*/
                0x00 /*00代表无需认证，客户端可以继续发送代理请求*/
            };

            stream.Write(response, 0, 2);

        }

        /// <summary>
        /// 从流中读取指定长度的数据到缓冲区
        /// </summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移</param>
        /// <param name="size">长度</param>
        private static void ReadPackage(Stream source, byte[] buffer, int offset, int size)
        {

            int received = 0;
            int rec;
            while ((rec = source.Read(buffer, offset + received, size - received)) > 0)
            {
                received += rec;
                if (received == size) return;
            }
            if (received != size) throw new IOException("流被关闭，数据无法完整读取");
        }
    }
}
