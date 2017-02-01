using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatchBreaker;
using log4net;
using Microsoft.Azure.Batch.Common;

namespace BatchLongRunningTest
{
    public class MonitorResizePools
    {
        readonly ILog _logger = LogManager.GetLogger(typeof(MonitorResizePools));
        const string PoolName = "LongRunningPoolF";
        const int NextAttemptIntervalInSeconds = 10;
        const int ScaleTarget = 1000;

        public void StartMonitoring()
        {
            Task.Run(() => BatchHelper.EnsureTasksJobScheduled("MonitorPool_running_tasks", PoolName, Commands.Wait, Commands.Wait, 1000));

            using (var batchClient = BatchHelper.GetBatchClient())
            {
                var pool = batchClient.PoolOperations.GetPool(PoolName);
                pool.Resize(ScaleTarget);
            }

            while (true)
            {
                Monitor();
                Thread.Sleep(TimeSpan.FromSeconds(NextAttemptIntervalInSeconds));
            }
        }

        void Monitor()
        {
            var stopWatch = new Stopwatch();

            _logger.Debug($"Starting to scale to {ScaleTarget}");

            using (var batchClient = BatchHelper.GetBatchClient())
            {
                try
                {
                    var pool = batchClient.PoolOperations.GetPool(PoolName);
                    var nodesRunning = pool.ListComputeNodes().Count(x => x.State == ComputeNodeState.Running);
                    _logger.Info($"Nodes running {nodesRunning}");

                    if (pool.AllocationState.Value != AllocationState.Steady)
                    {
                        return;
                    }

                    if (nodesRunning < ScaleTarget)
                    {
                        
                        return;
                    }

                    stopWatch.Start();

                    if (pool.CurrentDedicated == ScaleTarget)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    throw;
                }
            }
            
            stopWatch.Stop();
            _logger.Info($"Completed scaling to {ScaleTarget}, it took {stopWatch.Elapsed.ToString("c")}");
        }

    }
}
