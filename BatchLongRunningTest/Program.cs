using System;
using System.Threading.Tasks;

namespace BatchLongRunningTest
{
    class Program
    {
        static void Main(string[] args)
        {
            // TIMINGS FOCUS

            log4net.Config.XmlConfigurator.Configure();
            Task.Run(() => new MonitorPool().StartMonitoringPool());
            Task.Run(() => new ResizePools().StartResizing());
            Console.WriteLine("Running");
            Console.ReadLine();
        }
    }
}
