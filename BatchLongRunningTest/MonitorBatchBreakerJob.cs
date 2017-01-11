using System;
using System.Threading;
using log4net;

namespace BatchLongRunningTest
{
    public class MonitorBatchBreakerJob
    {
        static readonly ILog _logger = LogManager.GetLogger(typeof(MonitorBatchBreakerJob));
        const int HeartBeatIntervalInSeconds = 300;

        public class JobDefinition
        {
            public string JobName { get; set; }
            public string TaskType { get; set; }
            public string PoolId { get; set; }
            public string JobManagerTaskType { get; set; }
            public int NumberOfTasks { get; set; }
            public Action<string, string> OnMonitorCycle { get; set; }
        }

        public void StartMonitoringJob(JobDefinition jobDefinition)
        {
            try
            {
                var jobId = $"{jobDefinition.JobName}_{jobDefinition.PoolId}";

                if (!string.IsNullOrEmpty(jobDefinition.JobManagerTaskType))
                {
                    BatchHelper.EnsureJobManagerJobScheduled(jobId, jobDefinition.PoolId, jobDefinition.JobManagerTaskType);
                }

                if (!string.IsNullOrEmpty(jobDefinition.TaskType))
                {
                    BatchHelper.EnsureTasksJobScheduled(jobId, jobDefinition.PoolId, jobDefinition.TaskType, jobDefinition.JobManagerTaskType, jobDefinition.NumberOfTasks);
                }

                DoMonitoring(jobId, jobDefinition);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
        }

        void DoMonitoring(string jobId, JobDefinition jobDefinition)
        {
            while (true)
            {
                jobDefinition.OnMonitorCycle?.Invoke(jobId, jobDefinition.PoolId);
                CheckJobStatus(jobId, jobDefinition);
                Thread.Sleep(TimeSpan.FromSeconds(HeartBeatIntervalInSeconds));
            }
        }

        void CheckJobStatus(string jobId, JobDefinition jobDefinition)
        {
            var jobManagerTask = BatchHelper.GetTask(jobId, BatchHelper.JobManagerTaskId);

            if (jobManagerTask == null)
            {
                _logger.Info($"Job Manager task is not coming back from the api");
            }
            else
            {
                var node = BatchHelper.GetFirstNodeInPool(jobDefinition.PoolId);
                _logger.Info($"Batch Breaker {jobDefinition.JobName}, job id {jobId}, job manager state is {jobManagerTask.State}, task retry count is {jobManagerTask.ExecutionInformation.RetryCount}, node last booted {node.LastBootTime}");
            }
        }
    }
}
