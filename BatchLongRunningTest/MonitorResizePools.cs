using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatchBreaker;
using log4net;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;

namespace BatchLongRunningTest
{
    public class MonitorResizePools
    {
        readonly ILog _logger = LogManager.GetLogger(typeof(MonitorResizePools));
        const string PoolName = "LongRunningPool1B";
        const int NextAttemptIntervalInSeconds = 60;
        const int HeartBeatIntervalInSeconds = 10;
        public void StartMonitoring()
        {
            Task.Run(() => BatchHelper.EnsureTasksJobScheduled("MonitorPool_running_tasks", PoolName, Commands.Wait, Commands.Wait, 1000));
            
            while (true)
            {
                DoResizing();
                Thread.Sleep(TimeSpan.FromSeconds(NextAttemptIntervalInSeconds));
            }
        }

        void DoResizing()
        {
            Scale(1000, "Up");
            Scale(0, "Down");
        }

        void Scale(int targetDedicated, string scaleDirection)
        {
            var stopWatch = new Stopwatch();

            _logger.Debug($"Starting to scale {scaleDirection} to {targetDedicated}");

            using (var batchClient = BatchHelper.GetBatchClient())
            {
                try
                {
                    var pool = batchClient.PoolOperations.GetPool(PoolName);

                    if (pool.AllocationState.Value != AllocationState.Steady)
                    {
                        return;
                    }

                    var notRunningTaskCount = pool.ListComputeNodes().Count(x => x.State != ComputeNodeState.Running);

                    if (notRunningTaskCount > 0)
                    {
                        _logger.Info($"Still not running {notRunningTaskCount} task nodes");
                        return;
                    }

                    stopWatch.Start();

                    if (pool.CurrentDedicated == targetDedicated)
                    {
                        return;
                    }

                    pool.Resize(targetDedicated);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    throw;
                }
            }

            CheckScaled(pool => pool.CurrentDedicated == targetDedicated);
            stopWatch.Stop();
            _logger.Info($"Completed scaling {scaleDirection} to {targetDedicated}, it took {stopWatch.Elapsed.ToString("c")}");
        }

        void CheckScaled(Func<CloudPool, bool> predicate)
        {
            bool evaluationResult = false;

            while (!evaluationResult)
            {
                using (var batchClient = BatchHelper.GetBatchClient())
                {
                    try
                    {
                        var pool = batchClient.PoolOperations.GetPool(PoolName);

                        evaluationResult = 
                            pool.AllocationState.Value == AllocationState.Steady 
                            && predicate(pool);

                        if (!evaluationResult)
                        {
                            _logger.Debug($"Current Dedicated is {pool.CurrentDedicated}");
                        }

                        if (pool.ResizeError?.Code != null)
                        {
                            _logger.Warn($"Resizing Error {pool.ResizeError.Message}, {pool.ResizeError.Code}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex);
                        throw;
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(HeartBeatIntervalInSeconds));
            }
        }
    }
}
