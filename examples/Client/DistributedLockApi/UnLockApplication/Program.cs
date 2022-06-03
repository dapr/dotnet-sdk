using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Dapr.Client;

namespace DistributedLockApi
{
    public class Program2
    {
        [Obsolete]
        public static async Task Main(string[] args)
        {
            using var client = new DaprClientBuilder().Build();
            string StoreName = "redislock";
            string ResourceId = "resourceId";
            string LockOwner = "owner2";
            var lockResponse = await client.Unlock(StoreName, ResourceId, LockOwner);
            Console.WriteLine("Unlock API response when lock is acquired by a different process: " + lockResponse.Status);
        }
    }                                                                                                                        
}