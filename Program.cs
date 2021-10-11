using System;

namespace IocpSharp.Socks5
{
    class Program
    {
        static void Main(string[] args)
        {
            Socks5Server server = new Socks5Server(AppDomain.CurrentDomain.BaseDirectory + "pac.lst");
            try
            {
                server.Start("0.0.0.0", 4088);
                Console.WriteLine("服务器启动成功，监听地址：" + server.LocalEndPoint.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            Console.ReadLine();
        }
    }
}
