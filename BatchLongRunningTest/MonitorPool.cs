using System;
using System.Threading;
using log4net;

namespace BatchLongRunningTest
{
    public class MonitorPool
    {
        readonly ILog _logger = LogManager.GetLogger(typeof(MonitorPool));
        const string PoolName = "LongRunningPool1A";
        const int Expected = 1000;
        const int HeartBeatIntervalInSeconds = 600;

        DateTime _lastHeartBeat = DateTime.MinValue;

        public void StartMonitoringPool()
        {
            while (true)
            {
                DoMonitor();
                Thread.Sleep(TimeSpan.FromSeconds(HeartBeatIntervalInSeconds));
            }
        }

        void DoMonitor()
        {
            using (var batchClient = BatchHelper.GetBatchClient())
            {
                try
                {
                    var pool = batchClient.PoolOperations.GetPool(PoolName);
                    var allocation = pool.CurrentDedicated ?? 0;

                    if (IsUnderAllocated(allocation))
                    {
                        _logger.Info($"The pool is not fully allocated, we're looking to get {Expected}, but we're only getting {allocation}");
                    }
                    else if (HeartBeatIsDue())
                    {
                        _lastHeartBeat = DateTime.Now;
                        _logger.Info($"Pool is fully allocated");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    throw;
                }
            }
        }

        bool HeartBeatIsDue()
        {
            return (DateTime.Now - _lastHeartBeat).TotalSeconds > HeartBeatIntervalInSeconds;
        }

        bool IsUnderAllocated(int currentDedicated)
        {
            return currentDedicated < Expected;
        }
    }
}
