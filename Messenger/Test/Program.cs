using Mikodev.Network;
using System;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var lis = new LinkListener();
            lis.Listen();
            lis.Broadcast().Wait();
        }
    }
}
