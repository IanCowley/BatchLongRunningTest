using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using BatchBreaker;
using log4net;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Common;

namespace BatchLongRunningTest
{
    public class MonitorBatchBreakerJob
    {
        static readonly ILog _logger = LogManager.GetLogger(typeof(MonitorBatchBreakerJob));
        const string batchBreakerFileName = "batchBreaker.exe";
        const string jobManagerTaskId = "JM";
        const int HeartBeatIntervalInSeconds = 300;

        public void StartMonitoring(string taskType, string poolId)
        {
            StartMonitoring(taskType, poolId, onJobScheduled: null);
        }

        public void StartMonitoring(string taskType, string poolId, Action<string> onJobScheduled)
        {
            try
            {
                EnsureBatchBreakerUploaded();
                var jobId = EnsureJobScheduled(taskType, poolId);
                onJobScheduled?.Invoke(jobId);
                DoMonitoring(jobId, poolId, taskType);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
        }

        string EnsureJobScheduled(string taskType, string poolId)
        {
            var jobId = $"{taskType}_{poolId}";
            var job = BatchHelper.GetJob(jobId);

            if (job != null)
            {
                return jobId;
            }

            _logger.Info($"Scheduling job for task type {taskType} with job id {jobId} into pool {poolId}");

            using (var batchClient = BatchHelper.GetBatchClient())
            {

                job = batchClient.JobOperations.CreateJob(jobId, new PoolInformation
                {
                    PoolId = poolId
                });

                job.Constraints = new JobConstraints
                {
                    MaxTaskRetryCount = -1
                };

                job.JobManagerTask = CreateJobManagerTask(taskType);
                job.Commit();
            }

            return jobId;
        }

        JobManagerTask CreateJobManagerTask(string taskType)
        {
            var batchBreakerResourceFile = new ResourceFile(
                AzureStorageHelper.GetBlobSasUri(batchBreakerFileName).AbsoluteUri, 
                batchBreakerFileName);

            return new JobManagerTask
            {
                Id = jobManagerTaskId,
                CommandLine = $"{batchBreakerFileName} {taskType}",
                RunElevated = true,
                KillJobOnCompletion = true,
                Constraints = new TaskConstraints(null, null, -1),
                ResourceFiles = new List<ResourceFile>() { batchBreakerResourceFile },
                RunExclusive = false,
            };
        }

        void EnsureBatchBreakerUploaded()
        {
            if (!AzureStorageHelper.FileExists(batchBreakerFileName))
            {
                var batchBreakerAssembly = GetBatchBreakerAssemblyFileName();
                AzureStorageHelper.Upload(batchBreakerFileName, batchBreakerAssembly.Location);
            }
        }

        static Assembly GetBatchBreakerAssemblyFileName()
        {
            var batchBreakerAssembly = Assembly.GetAssembly(typeof(BatchBreakerReference));
            return batchBreakerAssembly;
        }

        void DoMonitoring(string jobId, string poolId, string taskType)
        {
            while (true)
            {
                CheckJobStatus(jobId, poolId, taskType);
                Thread.Sleep(TimeSpan.FromSeconds(HeartBeatIntervalInSeconds));
            }
        }

        void CheckJobStatus(string jobId, string poolId, string taskType)
        {
            var jobManagerTask = BatchHelper.GetTask(jobId, jobManagerTaskId);

            if (jobManagerTask == null)
            {
                _logger.Info($"Job Manager task is not coming back from the api");
            }
            else
            {
                var node = BatchHelper.GetFirstNodeInPool(poolId);
                _logger.Info($"Batch Breaker {taskType}, job id {jobId}, job manager state is {jobManagerTask.State}, task retry count is {jobManagerTask.ExecutionInformation.RetryCount}, node last booted {node.LastBootTime}");
            }
        }

        public static void RebootFirstNodeWhenReady(string poolId)
        {
            var nodeIsSteady = false;

            while (!nodeIsSteady)
            {
                nodeIsSteady = BatchHelper.GetFirstNodeInPool(poolId).State.Value == ComputeNodeState.Running;

                if (nodeIsSteady)
                {
                    BatchHelper.RebootFirstNodeInPool(poolId);
                }
            }
        }
    }
}
