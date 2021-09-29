using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace IocpSharp.Socks5.Commands
{
    public interface ICommand
    {
        public void Handle(Stream requestStream, ProxyRequest request);
    }
}
