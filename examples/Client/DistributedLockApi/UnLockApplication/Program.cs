using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Dapr.Client;

namespace DistributedLockApi
{
    public class Program
    {
        [Obsolete]
        public static async Task Main(string[] args)
        {
            using var client = new DaprClientBuilder().Build();
            string StoreName = "redislock";
            string ResourceId = "resourceId";
            string LockOwner = "owner2";
            Int32 ExpiryInSeconds = 25;
            string DAPR_STORE_NAME = "statestore";
            
            //This is the second process trying to acquire unexpired lock(Lock acquired by the first(TryLockApplication) process)
            while(true) 
            {
                try
                {
                    if(await client.TryLock(StoreName, ResourceId, LockOwner, ExpiryInSeconds)) {
                        try
                        {
                            await client.SaveStateAsync(DAPR_STORE_NAME, "deposit", "300");
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
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
    }                                                                                                                        
}