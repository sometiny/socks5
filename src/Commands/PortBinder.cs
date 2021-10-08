using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IocpSharp.Socks5.Commands
{
    public sealed class PortBinder
    {
        private static int[] _allowedPorts = new int[0];
        private static ConcurrentStack<int> _portInStack = new ConcurrentStack<int>();

        public static void SetAllowedPorts(int[] ports)
        {
            ports = ports ?? throw new ArgumentNullException("ports");

            int[] newPorts = ports.Except(_allowedPorts).ToArray();

            _allowedPorts = ports;

            if (newPorts.Length == 0) return;

            _portInStack.PushRange(newPorts);

        }

        public static void Push(int port)
        {
            _portInStack.Push(port);
        }

        public static bool TryPop(out int port)
        {
            return _portInStack.TryPop(out port);
        }
    }
}
