using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace BatchBreaker
{
    class Program
    {
        static int Main(string[] args)
        {
            var commandToRun = args.First();

            switch (commandToRun)
            {
                case Commands.Wait:
                {
                    while (true)
                    {
                        Console.WriteLine("Waiting...");
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                    }
                }
                case Commands.ThrowCommand:
                {
                    return -1;
                }
                case Commands.ShutDown:
                {
                    Process.Start("cmd.exe", "/C shutdown /r /t 0 ");
                    while (true)
                    {
                        Console.WriteLine("Waiting...");
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                    }
                }
            }

            Console.WriteLine("Command not found, could be you've renamed the command!");
            return 0;
        }
    }
}
