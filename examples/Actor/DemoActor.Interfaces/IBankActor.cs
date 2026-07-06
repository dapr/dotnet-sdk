// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Dapr.Actors;

namespace IDemoActor;

public interface IBankActor : IActor
{
    Task<AccountBalance> GetAccountBalance();

    Task Withdraw(WithdrawRequest withdraw);
}

public sealed record AccountBalance(string AccountId, decimal Balance);

public sealed record WithdrawRequest(decimal Amount);

public class OverdraftException(decimal balance, decimal amount)
    : Exception($"Your current balance is {balance:c} - that's not enough to withdraw {amount:c}.");
