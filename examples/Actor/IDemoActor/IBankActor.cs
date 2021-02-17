// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace IDemoActorInterface
{
    using System;
    using System.Threading.Tasks;
    using Dapr.Actors;

    public interface IBankActor : IActor
    {
        Task<AccountBalance> GetAccountBalance();

        Task Withdraw(WithdrawRequest withdraw);
    }

    public class AccountBalance
    {
        public string AccountId { get; set; }

        public decimal Balance { get; set; }
    }

    public class WithdrawRequest
    {
        public decimal Amount { get; set; }
    }

    public class OverdraftException : Exception
    {
        public OverdraftException(decimal balance, decimal amount)
            : base($"Your current balance is {balance:c} - that's not enough to withdraw {amount:c}.")
        {
        }
    }
}
