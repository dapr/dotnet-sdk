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
            string DAPR_STORE_NAME = "statestore";

            using var client = new DaprClientBuilder().Build();
            while(true) 
            {
                try{
                    if(await client.TryLock(StoreName, ResourceId, LockOwner, ExpiryInSeconds)) {
                        try
                        {
                            await client.SaveStateAsync(DAPR_STORE_NAME, "deposit", "200");
                            var result = await client.GetStateAsync<string>(DAPR_STORE_NAME, "deposit");
                            Console.WriteLine("Getting deposited value: " + result);
                        }
                        finally
                        {
                            await client.Unlock(StoreName, ResourceId, LockOwner);
                        }
                        break;
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"ERROR: Got exception while acquiring the lock. Exception: {ex}");
                }
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            //This is the 2nd scenario showing the lock is acquired by one process(TryLockApplication) for 50 seconds 
            //and the other process(UnLockApplication) is trying to acquire this lock. The second process(UnLockApplication) 
            //keeps on waiting for the lock to expire and it acquires lock once the lock acquired by the first process(TryLockApplication) is expired.

            ExpiryInSeconds = 50;
            while(true)
            {
                try{
                   if(await client.TryLock(StoreName, ResourceId, LockOwner, ExpiryInSeconds)) {
                      Console.WriteLine("Acquired Lock for 50 seconds");
                      break;    
                   }
                   Console.WriteLine("Lock not acquired and sleeping for 5 seconds");      
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"ERROR: Got exception while acquiring the lock. Exception: {ex}");
                }
                await Task.Delay(TimeSpan.FromSeconds(5));  
            }
        }
    }
}