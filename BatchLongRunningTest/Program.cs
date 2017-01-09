using System;
using System.Threading;
using System.Threading.Tasks;

namespace BatchLongRunningTest
{
    class Program
    {
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            Task.Run(() => new MonitorPool().StartMonitoring());
            Task.Run(() => new MonitorResizePools().StartMonitoring());
            Task.Run(() => new MonitorBatchBreakerJob().StartMonitoring(BatchBreaker.Commands.ThrowCommand, poolId: "batchBreaker1"));
            Task.Run(() => new MonitorBatchBreakerJob().StartMonitoring(BatchBreaker.Commands.ShutDown, poolId: "batchBreaker2"));

            Task.Run(() => new MonitorBatchBreakerJob().StartMonitoring(
                BatchBreaker.Commands.WaitForNodeReboot, 
                poolId: "batchBreaker3", 
                onJobScheduled: jobId => MonitorBatchBreakerJob.RebootFirstNodeWhenReady("batchBreaker3")));

            Console.WriteLine("Running");
            Console.ReadLine();
        }
    }
}

