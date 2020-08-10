// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace ControllerSample.Controllers
{
    using System.Threading.Tasks;
    using Dapr;
    using Dapr.Client;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Sample showing Dapr integration with controller.
    /// </summary>
    [ApiController]
    public class SampleController : ControllerBase
    {
        /// <summary>
        /// State store name.
        /// </summary>
        public const string StoreName = "statestore";

        /// <summary>
        /// Gets the account information as specified by the id.
        /// </summary>
        /// <param name="account">Account information for the id from Dapr state store.</param>
        /// <returns>Account information.</returns>
        [HttpGet("{account}")]
        public ActionResult<Account> Get([FromState(StoreName)]StateEntry<Account> account)
        {
            if (account.Value is null)
            {
                return this.NotFound();
            }

            return account.Value;
        }

        /// <summary>
        /// Method for depositing to account as specified in transaction.
        /// </summary>
        /// <param name="transaction">Transaction info.</param>
        /// <param name="daprClient">State client to interact with Dapr runtime.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        [Topic("pubsub", "deposit")]
        [HttpPost("deposit")]
        public async Task<ActionResult<Account>> Deposit(Transaction transaction, [FromServices] DaprClient daprClient)
        {
            var state = await daprClient.GetStateEntryAsync<Account>(StoreName, transaction.Id);
            state.Value ??= new Account() { Id = transaction.Id, };
            state.Value.Balance += transaction.Amount;
            await state.SaveAsync();
            return state.Value;
        }

        /// <summary>
        /// Method for withdrawing from account as specified in transaction.
        /// </summary>
        /// <param name="transaction">Transaction info.</param>
        /// <param name="daprClient">State client to interact with Dapr runtime.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        [Topic("pubsub", "withdraw")]
        [HttpPost("withdraw")]
        public async Task<ActionResult<Account>> Withdraw(Transaction transaction, [FromServices] DaprClient daprClient)
        {
            var state = await daprClient.GetStateEntryAsync<Account>(StoreName, transaction.Id);

            if (state.Value == null)
            {
                return this.NotFound();
            }

            state.Value.Balance -= transaction.Amount;
            await state.SaveAsync();
            return state.Value;
        }
    }
}
