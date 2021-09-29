using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace IocpSharp.Socks5
{
    public enum RequestCommand
    {
        CONNECT = 0x01,
        BIND = 0x02,
        UDP_ASSOCIATE = 0x03,
    }
    public enum AddressType
    {
        IpV4 = 0x01,
        Host = 0x03,
        IpV6 = 0x04,
    }

    public class ProxyRequest
    {
        public byte Version { get; set; }
        public RequestCommand Command { get; set; }
        public byte Rsv { get; set; }

        public AddressType AddressType { get;set;}

        public EndPoint RemoteEndPoint { get; set; }
    }
}
