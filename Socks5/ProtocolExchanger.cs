using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Net.Sockets;

namespace IocpSharp.Socks5
{
    class ProtocolExchanger : IDisposable
    {
        private Stream _clientStream = null;
        private Stream _remoteStream = null;
        public void Start(Stream stream)
        {
            //开始进行协议交换，读取头部信息
            StartExchange(stream);

            //开始读取代理请求，返回一个需要代理的远程终结点
            EndPoint remoteEndPoint = StartReadRequest(stream);

            //连接远程服务器
            Stream remoteStream = ConnectRemote(remoteEndPoint, out IPEndPoint connectedEndPoint);


            //连接成功后，发送响应数据到客户端
            SendConnectResult(stream, connectedEndPoint);

            //协议结束，客户端接收到响应后，开始正常收发数据，服务器唯一需要做的就是转发数据
            //这里用异步方式处理
            _clientStream = stream;
            _remoteStream = remoteStream;
            stream.CopyToAsync(remoteStream).ContinueWith(clientCopyFinished);
            remoteStream.CopyToAsync(stream).ContinueWith(remoteCopyFinished);

        }

        public void Dispose()
        {
            _clientStream?.Close();
            _remoteStream?.Close();
        }

        private void clientCopyFinished(Task task)
        {
            _clientStream.Close();
        }
        private void remoteCopyFinished(Task task)
        {
            _remoteStream.Close();
        }

        /// <summary>
        /// 发送连接成功的响应到客户端
        /// </summary>
        /// <param name="stream">客户端流</param>
        /// <param name="endpoint">连接成功的终结点</param>
        private void SendConnectResult(Stream stream, IPEndPoint endpoint)
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

            stream.Write(response, 0, responseLength);
        }

        /// <summary>
        /// 连接远程服务器
        /// </summary>
        /// <param name="endpoint">待连接的远程结点</param>
        /// <param name="connectedEndPoint">连接成功的远程结点</param>
        /// <returns></returns>
        private Stream ConnectRemote(EndPoint endpoint, out IPEndPoint connectedEndPoint)
        {
            if(endpoint is DnsEndPoint dnsEndPoint)
            {
                //解析主机
                endpoint = new IPEndPoint( ResolveDnsHost(dnsEndPoint.Host), dnsEndPoint.Port) ;
            }

            Socket remoteSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            remoteSocket.NoDelay = true;
            remoteSocket.Connect(endpoint);

            connectedEndPoint = endpoint as IPEndPoint;

            return new NetworkStream(remoteSocket, true);
        }

        /// <summary>
        /// 读取代理请求
        /// </summary>
        /// <param name="stream">客户端数据流</param>
        /// <returns></returns>
        private EndPoint StartReadRequest(Stream stream)
        {
            //创建一个足够大的缓冲区
            byte[] buffer = new byte[512];
            ReadPackage(stream, buffer, 0, 5);

            //第一个字节，版本号
            byte version = buffer[0];

            /*
             * 第二个字节，命令类型，这里我们处理CONNECT方法
             * 0x01 CONNECT 连接上游服务器
             * 0x02 BIND 绑定，客户端会接收来自代理服务器的链接，著名的FTP被动模式
             * 0x03 UDP ASSOCIATE UDP中继
             */
            byte command = buffer[1];

            //保留字节
            byte rsv = buffer[2];

            /*
             * 地址类型
             * 0x01 IP V4地址
             * 0x03 域名
             * 0x04 IP V6地址
             * 
             * 目前为止，从版本号，到地理类型，我们用到了四个字节
             */
            byte addressType = buffer[3];
            int hostLength = 0;
            if(addressType == 0x03)
            {
                hostLength = buffer[4];
            }

            /*
             * 确认后续需要读取的数据长度
             * 地址类型为0x03 时，读取长度为hostLength
             * 地址类型为0x01 IPv4地址时，读取长度为3，其中第一个字节我们已经提前读取了，即buffer[4]。
             * 地址类型为0x04 IPv6地址时，读取长度为15，其中第一个字节我们已经提前读取了，即buffer[4]。
             */
            int remainLength = addressType == 0x03 ? hostLength : (addressType == 0x01 ? 3 : 15);

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
            if(addressType == 0x03)
            {
                return new DnsEndPoint(Encoding.ASCII.GetString(buffer, 5, hostLength), destPort);
            }

            /*确认IP地址长度，IPv4是4个字节，IPv6是16个字节*/
            byte[] ipBuffer = new byte[addressType == 0x01 ? 4 : 16];

            /*
             * 从缓冲区中把IP数据读出来
             * 注意，读取索引为4
             */
            Array.Copy(buffer, 4, ipBuffer, 0, ipBuffer.Length);

            return new IPEndPoint( new IPAddress(ipBuffer), destPort);
        }

        /// <summary>
        /// 开始认证
        /// </summary>
        /// <param name="stream">客户端数据流</param>
        private void StartExchange(Stream stream)
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
        /// 解析主机
        /// </summary>
        /// <param name="host">主机名</param>
        /// <param name="ipV6Prior">是否优先使用IPv6地址</param>
        /// <returns></returns>
        private static IPAddress ResolveDnsHost(string host, bool ipV6Prior = false) {
            IPAddress[] address = Dns.GetHostAddresses(host);
            if (address == null || address.Length == 0) throw new SocketException(11001); //SocketError.HostNotFound

            if (address.Length == 1) return address[0];

            if(ipV6Prior) return address.OrderByDescending(t => t.AddressFamily).First();

            return address.OrderBy(t => t.AddressFamily).First();
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
