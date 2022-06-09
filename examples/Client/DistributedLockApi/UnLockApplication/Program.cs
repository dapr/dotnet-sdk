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
            Int32 ExpiryInSeconds = 25;
            
            //Trying to acquire the lock which is being used by the other process(TryLockApplication)
            var tryLockResponse = await client.TryLock(StoreName, ResourceId, LockOwner, ExpiryInSeconds);
            Console.WriteLine("Acquired Lock? " + tryLockResponse.Success);
            Console.WriteLine("Lock cannot be acquired as it belongs to the other process");

            var lockResponse = await client.Unlock(StoreName, ResourceId, LockOwner);
            Console.WriteLine("Unlock API response when lock is acquired by a different process: " + lockResponse.status);

            System.Threading.Thread.Sleep(25000);

            //Trying to acquire the lock after the other process lock is expired
            tryLockResponse = await client.TryLock(StoreName, ResourceId, LockOwner, ExpiryInSeconds);
            Console.WriteLine("Acquired lock after the lock from the other process expired? " + tryLockResponse.Success);

            lockResponse = await client.Unlock(StoreName, ResourceId, LockOwner);
            Console.WriteLine("Unlock API response when lock is released after the expiry time: " + lockResponse.status);

            System.Threading.Thread.Sleep(25000);

            //Trying to unlock the lock after the lock used by this process(UnLockApplication) is expired
            lockResponse = await client.Unlock(StoreName, ResourceId, LockOwner);
            Console.WriteLine("Unlock API response when lock is released after the expiry time and lock does not exist: " + lockResponse.status);
        }
    }                                                                                                                        
}