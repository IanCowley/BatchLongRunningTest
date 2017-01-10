using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;

namespace BatchLongRunningTest
{
    public static class BatchHelper
    {
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
    }
}
