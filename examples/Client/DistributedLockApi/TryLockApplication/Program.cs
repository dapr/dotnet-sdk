using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.Client;

namespace DistributedLockApi
{
    public class Program
    {
        [Obsolete]
        public static async Task Main(string[] args)
        {
            string StoreName = "redislock";
            string ResourceId = "resourceId";
            string LockOwner = "owner1";
            Int32 ExpiryInSeconds = 1000;

            using var client = new DaprClientBuilder().Build();
            var tryLockResponse = await client.TryLock(StoreName, ResourceId, LockOwner, ExpiryInSeconds);
            
            string DAPR_STORE_NAME = "statestore";
            await client.SaveStateAsync(DAPR_STORE_NAME, "deposit", "200");
            var result = await client.GetStateAsync<string>(DAPR_STORE_NAME, "deposit");
            Console.WriteLine("Getting deposited value: " + result);

            var unlockResponse = await client.Unlock(StoreName, ResourceId, LockOwner);
            Console.WriteLine("Unlock API response: " + unlockResponse.Status);

            //Checking if the lock exists.
            var lockUnexistResponse = await client.Unlock(StoreName, ResourceId, LockOwner);
            Console.WriteLine("Unlock API response when lock is not acquired: " + lockUnexistResponse.Status);

            //Testing
            ResourceId = "resourceId";
            LockOwner = "owner1";
            ExpiryInSeconds = 1000;
            var tryLockResponse1 = await client.TryLock(StoreName, ResourceId, LockOwner, ExpiryInSeconds);
            Console.WriteLine("Acquired Lock? " + tryLockResponse1.Success);

            System.Threading.Thread.Sleep(20000);
        }
    }
}