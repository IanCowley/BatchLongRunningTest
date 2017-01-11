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

            Task.Run(() => new MonitorBatchBreakerJob().StartMonitoringJob(
                new MonitorBatchBreakerJob.JobDefinition
                {
                    JobName = BatchBreaker.Commands.ThrowCommand,
                    JobManagerTaskType = BatchBreaker.Commands.ThrowCommand,
                    PoolId = "batchBreaker1"
                }));

            Task.Run(() => new MonitorBatchBreakerJob().StartMonitoringJob(
                new MonitorBatchBreakerJob.JobDefinition
                {
                    JobName = BatchBreaker.Commands.ShutDown,
                    JobManagerTaskType = BatchBreaker.Commands.ShutDown,
                    PoolId = "batchBreaker2"
                }));

            Task.Run(() => new MonitorBatchBreakerJob().StartMonitoringJob(
               new MonitorBatchBreakerJob.JobDefinition
               {
                   JobName = "Reboot",
                   JobManagerTaskType = BatchBreaker.Commands.Wait,
                   PoolId = "batchBreaker3",
                   OnMonitorCycle = (jobId, poolId) => BatchHelper.RebootFirstNodeInPool(poolId)
               }));

            Console.WriteLine("Running");
            Console.ReadLine();
        }
    }
}

