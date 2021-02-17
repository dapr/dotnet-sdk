// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// 

using IDemoActorInterface;

namespace DaprDemoActor
{
    public class BankService
    {
        // Allow overdraft of up to 50 (of whatever currency).
        private readonly decimal OverdraftThreshold = -50m;

        public decimal Withdraw(decimal balance, decimal amount)
        {
            // Imagine putting some complex auditing logic here in addition to the basics.

            var updated = balance - amount;
            if (updated < OverdraftThreshold)
            {
                throw new OverdraftException(balance, amount);
            }

            return updated;
        }
    }
}
