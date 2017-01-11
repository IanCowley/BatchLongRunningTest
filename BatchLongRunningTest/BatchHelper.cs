using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BatchBreaker;
using log4net;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;

namespace BatchLongRunningTest
{
    public static class BatchHelper
    {
        public const string JobManagerTaskId = "JM";
        const string batchBreakerFileName = "batchBreaker.exe";
        static readonly ILog _logger = LogManager.GetLogger(typeof(BatchHelper));

        public static BatchClient GetBatchClient()
        {
            var credentials = new BatchSharedKeyCredentials($"https://icowleybatch.northeurope.batch.azure.com", "icowleybatch", "8wtEaN+qAsD6ahlbGQBj/aGxqaXw1lOU4DgTwEB+E13jFfHrNJ4pOZjNvMU6/LVw6JQ9CpdvwyNd/Ii8k1n8MA==");
            return BatchClient.Open(credentials);
        }

        public static CloudTask GetTask(string jobId, string taskId)
        {
            using (var batchClient = GetBatchClient())
            {
                return batchClient.JobOperations.GetTask(jobId, taskId);
            }
        }

        public static ComputeNode GetFirstNodeInPool(string poolId)
        {
            using (var batchClient = GetBatchClient())
            {
                var pool = batchClient.PoolOperations.GetPool(poolId);
                var node = pool.ListComputeNodes().FirstOrDefault();
                if (node == null) throw new InvalidOperationException($"Couldn't find any nodes for poolId {poolId}");
                return node;
            }
        }

        public static void RebootFirstNodeInPool(string poolId)
        {
            using (var batchClient = GetBatchClient())
            {
                var pool = batchClient.PoolOperations.GetPool(poolId);
                var node = pool.ListComputeNodes().FirstOrDefault();
                node.Reboot(ComputeNodeRebootOption.Requeue);
            }
        }

        public static void EnsureTasksJobScheduled(string jobId, string poolId, string taskType, string jobManagerTaskType, int numberOfTasks)
        {
            EnsureBatchBreakerUploaded();

          

            using (var batchClient = GetBatchClient())
            {
                _logger.Info($"Scheduling job for task type {taskType} with job id {jobId} into pool {poolId}");

                var job = GetJob(jobId);

                if (job == null)
                {
                    job = batchClient.JobOperations.CreateJob(jobId, new PoolInformation
                    {
                        PoolId = poolId
                    });

                    job.Constraints = new JobConstraints
                    {
                        MaxTaskRetryCount = -1
                    };

                    job.JobManagerTask = CreateJobManagerTask(jobManagerTaskType);
                    job.Commit();
                }

                for (var taskIndex = 0; taskIndex < numberOfTasks; taskIndex++)
                {
                    var taskId = $"{taskType}_{taskIndex}";
                    try
                    {
                        var task = batchClient.JobOperations.GetTask(jobId, taskId);

                        if (task == null)
                        {
                            batchClient.JobOperations.AddTask(jobId, CreateTask(taskId, taskType));
                        }
                    }
                    catch (Exception)
                    {
                        batchClient.JobOperations.AddTask(jobId, CreateTask(taskId, taskType));
                    }
                }
            }
        }

        public static void EnsureJobManagerJobScheduled(string jobId, string poolId, string taskType)
        {
            EnsureBatchBreakerUploaded();

            var job = GetJob(jobId);

            if (job != null) return;

            using (var batchClient = BatchHelper.GetBatchClient())
            {
                _logger.Info($"Scheduling job for task type {taskType} with job id {jobId} into pool {poolId}");

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
        }

        static CloudTask CreateTask(string taskId, string taskType)
        {
            var batchBreakerResourceFile = new ResourceFile(
                  AzureStorageHelper.GetBlobSasUri(batchBreakerFileName).AbsoluteUri,
                  batchBreakerFileName);

            return new CloudTask(taskId, $"{batchBreakerFileName} {taskType}")
            {
                RunElevated = true,
                Constraints = new TaskConstraints(null, null, -1),
                ResourceFiles = new List<ResourceFile>() { batchBreakerResourceFile },
            };
        }

        static JobManagerTask CreateJobManagerTask(string taskType)
        {
            var batchBreakerResourceFile = new ResourceFile(
                AzureStorageHelper.GetBlobSasUri(batchBreakerFileName).AbsoluteUri,
                batchBreakerFileName);

            return new JobManagerTask
            {
                Id = JobManagerTaskId,
                CommandLine = $"{batchBreakerFileName} {taskType}",
                RunElevated = true,
                KillJobOnCompletion = true,
                Constraints = new TaskConstraints(null, null, -1),
                ResourceFiles = new List<ResourceFile>() { batchBreakerResourceFile },
                RunExclusive = false,
            };
        }

        public static CloudJob GetJob(string jobId)
        {
            using (var batchClient = GetBatchClient())
            {
                try
                {
                    return batchClient.JobOperations.GetJob(jobId);
                }
                catch (Exception)
                {
                    return null;
                }
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
                    _logger.Info($"Rebooting node for poolId {poolId}");
                    BatchHelper.RebootFirstNodeInPool(poolId);
                }
            }
        }

        static void EnsureBatchBreakerUploaded()
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
    }
}
