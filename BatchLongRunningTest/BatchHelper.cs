using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;

namespace BatchLongRunningTest
{
    public static class BatchHelper
    {
        public static BatchClient GetBatchClient()
        {
            var credentials = new BatchSharedKeyCredentials($"https://icdev4.northeurope.batch.azure.com", "icdev4", "/7GczRNZF3uzocCOsGyckyOiFnaOjbpjurPmvMpyMWliKNpjmZFHOrx9wv8yKb/YlD7enhqF7bVoSFa7VjAHFw==");
            return BatchClient.Open(credentials);
        }
    }
}
