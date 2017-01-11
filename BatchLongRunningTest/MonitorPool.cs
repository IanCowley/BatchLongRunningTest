using System;
using System.Linq;
using System.Threading;
using BatchBreaker;
using log4net;

namespace BatchLongRunningTest
{
    public class MonitorPool
    {
        readonly ILog _logger = LogManager.GetLogger(typeof(MonitorPool));
        public const string PoolId = "LongRunningPool1A";
        const int Expected = 1000;
        const int HeartBeatIntervalInSeconds = 600;
        const int UnderAllocatedCheckAgainTimeoutSeconds = 60;

        DateTime _lastHeartBeat = DateTime.MinValue;

        public void StartMonitoring()
        {
            try
            {
                BatchHelper.EnsureTasksJobScheduled("MonitorPool_running_tasks", PoolId, Commands.Wait, Commands.Wait, 1000);

                while (true)
                {
                    DoMonitor();
                    Thread.Sleep(TimeSpan.FromSeconds(HeartBeatIntervalInSeconds));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error monitoring pool {PoolId}", ex);
            }
        }

        void DoMonitor()
        {
            using (var batchClient = BatchHelper.GetBatchClient())
            {
                try
                {
                    var pool = batchClient.PoolOperations.GetPool(PoolId);
                    var allocation = pool.CurrentDedicated ?? 0;

                    if (IsUnderAllocated(allocation))
                    {
                        _logger.Info($"The pool is not fully allocated, we're looking to get {Expected}, but we're only getting {allocation}");
                        Thread.Sleep(TimeSpan.FromSeconds(UnderAllocatedCheckAgainTimeoutSeconds));
                        DoMonitor();
                    }
                    else
                    {
                        var runningTaskCount = pool.ListComputeNodes().ToList().Sum(x => x.RunningTasksCount);
                        _lastHeartBeat = DateTime.Now;
                        _logger.Info($"Pool is fully allocated");
                        _logger.Info($"Running task count is {runningTaskCount}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    throw;
                }
            }
        }

        bool IsUnderAllocated(int currentDedicated)
        {
            return currentDedicated < Expected;
        }
    }
}
