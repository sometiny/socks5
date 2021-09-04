using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IocpSharp.Socks5
{
    class Program
    {
        static void Main(string[] args)
        {
            Socks5Server server = new Socks5Server();

            try
            {
                server.Start("0.0.0.0", 2088);
                Console.WriteLine("服务器启动成功，监听地址：" + server.LocalEndPoint.ToString());
            }catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.ReadLine();
        }
    }
}
