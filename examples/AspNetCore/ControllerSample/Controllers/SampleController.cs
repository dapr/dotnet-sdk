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

using System.Linq;

namespace ControllerSample.Controllers;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr;
using Dapr.AspNetCore;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sample showing Dapr integration with controller.
/// </summary>
[ApiController]
public class SampleController : ControllerBase
{
    /// <summary>
    /// SampleController Constructor with logger injection
    /// </summary>
    /// <param name="logger"></param>
    public SampleController(ILogger<SampleController> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// State store name.
    /// </summary>
    public const string StoreName = "statestore";

    private readonly ILogger<SampleController> logger;

    /// <summary>
    /// Gets the account information as specified by the id.
    /// </summary>
    /// <param name="account">Account information for the id from Dapr state store.</param>
    /// <returns>Account information.</returns>
    [HttpGet("{account}")]
    public ActionResult<Account> Get([FromState(StoreName)] StateEntry<Account> account)
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
    ///  "pubsub", the first parameter into the Topic attribute, is name of the default pub/sub configured by the Dapr CLI.
    [Topic("pubsub", "deposit", "amountDeadLetterTopic", false)]
    [HttpPost("deposit")]
    public async Task<ActionResult<Account>> Deposit(Transaction transaction, [FromServices] DaprClient daprClient)
    {
        // Example reading cloudevent properties from the headers
        var headerEntries = Request.Headers.Aggregate("", (current, header) => current + ($"------- Header: {header.Key} : {header.Value}" + Environment.NewLine));

        logger.LogInformation(headerEntries);

        logger.LogInformation("Enter deposit");
        var state = await daprClient.GetStateEntryAsync<Account>(StoreName, transaction.Id);
        state.Value ??= new Account() { Id = transaction.Id, };
        logger.LogInformation("Id is {0}, the amount to be deposited is {1}", transaction.Id, transaction.Amount);

        if (transaction.Amount < 0m)
        {
            return BadRequest(new { statusCode = 400, message = "bad request" });
        }

        state.Value.Balance += transaction.Amount;
        logger.LogInformation("Balance for Id {0} is {1}", state.Value.Id, state.Value.Balance);
        await state.SaveAsync();
        return state.Value;
    }

    /// <summary>
    /// Method for depositing multiple times to the account as specified in transaction.
    /// </summary>
    /// <param name="bulkMessage">List of entries of type BulkMessageModel received from dapr.</param>
    /// <param name="daprClient">State client to interact with Dapr runtime.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    ///  "pubsub", the first parameter into the Topic attribute, is name of the default pub/sub configured by the Dapr CLI.
    [Topic("pubsub", "multideposit", "amountDeadLetterTopic", false)]
    [BulkSubscribe("multideposit", 500, 2000)]
    [HttpPost("multideposit")]
    public async Task<ActionResult<BulkSubscribeAppResponse>> MultiDeposit([FromBody]
        BulkSubscribeMessage<BulkMessageModel<Transaction>>
            bulkMessage, [FromServices] DaprClient daprClient)
    {
        logger.LogInformation("Enter bulk deposit");

        List<BulkSubscribeAppResponseEntry> entries = new List<BulkSubscribeAppResponseEntry>();

        foreach (var entry in bulkMessage.Entries)
        {
            try
            {
                var transaction = entry.Event.Data;

                var state = await daprClient.GetStateEntryAsync<Account>(StoreName, transaction.Id);
                state.Value ??= new Account() { Id = transaction.Id, };
                logger.LogInformation("Id is {0}, the amount to be deposited is {1}",
                    transaction.Id, transaction.Amount);

                if (transaction.Amount < 0m)
                {
                    return BadRequest(new { statusCode = 400, message = "bad request" });
                }

                state.Value.Balance += transaction.Amount;
                logger.LogInformation("Balance is {0}", state.Value.Balance);
                await state.SaveAsync();
                entries.Add(
                    new BulkSubscribeAppResponseEntry(entry.EntryId, BulkSubscribeAppResponseStatus.SUCCESS));
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                entries.Add(new BulkSubscribeAppResponseEntry(entry.EntryId, BulkSubscribeAppResponseStatus.RETRY));
            }
        }

        return new BulkSubscribeAppResponse(entries);
    }

    /// <summary>
    /// Method for viewing the error message when the deposit/withdrawal amounts
    /// are negative.
    /// </summary>
    /// <param name="transaction">Transaction info.</param>
    [Topic("pubsub", "amountDeadLetterTopic")]
    [HttpPost("deadLetterTopicRoute")]
    public ActionResult<Account> ViewErrorMessage(Transaction transaction)
    {
        logger.LogInformation("The amount cannot be negative: {0}", transaction.Amount);
        return Ok();
    }

    /// <summary>
    /// Method for withdrawing from account as specified in transaction.
    /// </summary>
    /// <param name="transaction">Transaction info.</param>
    /// <param name="daprClient">State client to interact with Dapr runtime.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    ///  "pubsub", the first parameter into the Topic attribute, is name of the default pub/sub configured by the Dapr CLI.
    [Topic("pubsub", "withdraw", "amountDeadLetterTopic", false)]
    [HttpPost("withdraw")]
    public async Task<ActionResult<Account>> Withdraw(Transaction transaction, [FromServices] DaprClient daprClient)
    {
        logger.LogInformation("Enter withdraw method...");
        var state = await daprClient.GetStateEntryAsync<Account>(StoreName, transaction.Id);
        logger.LogInformation("Id is {0}, the amount to be withdrawn is {1}", transaction.Id, transaction.Amount);

        if (state.Value == null)
        {
            return this.NotFound();
        }

        if (transaction.Amount < 0m)
        {
            return BadRequest(new { statusCode = 400, message = "bad request" });
        }

        state.Value.Balance -= transaction.Amount;
        logger.LogInformation("Balance is {0}", state.Value.Balance);
        await state.SaveAsync();
        return state.Value;
    }

    /// <summary>
    /// Method for withdrawing from account as specified in transaction.
    /// </summary>
    /// <param name="transaction">Transaction info.</param>
    /// <param name="daprClient">State client to interact with Dapr runtime.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    ///  "pubsub", the first parameter into the Topic attribute, is name of the default pub/sub configured by the Dapr CLI.
    [Topic("pubsub", "withdraw", "event.type ==\"withdraw.v2\"", 1)]
    [HttpPost("withdraw.v2")]
    public async Task<ActionResult<Account>> WithdrawV2(TransactionV2 transaction,
        [FromServices] DaprClient daprClient)
    {
        logger.LogInformation("Enter withdraw.v2");
        if (transaction.Channel == "mobile" && transaction.Amount > 10000)
        {
            return this.Unauthorized("mobile transactions for large amounts are not permitted.");
        }

        var state = await daprClient.GetStateEntryAsync<Account>(StoreName, transaction.Id);

        if (state.Value == null)
        {
            return this.NotFound();
        }

        state.Value.Balance -= transaction.Amount;
        await state.SaveAsync();
        return state.Value;
    }

    /// <summary>
    /// Method for depositing to account as specified in transaction via a raw message.
    /// </summary>
    /// <param name="transaction">Transaction info.</param>
    /// <param name="daprClient">State client to interact with Dapr runtime.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    ///  "pubsub", the first parameter into the Topic attribute, is name of the default pub/sub configured by the Dapr CLI.
    [Topic("pubsub", "rawDeposit", true)]
    [HttpPost("rawDeposit")]
    public async Task<ActionResult<Account>> RawDeposit([FromBody] JsonDocument rawTransaction,
        [FromServices] DaprClient daprClient)
    {
        var transactionString = rawTransaction.RootElement.GetProperty("data_base64").GetString();
        logger.LogInformation(
            $"Enter deposit: {transactionString} - {Encoding.UTF8.GetString(Convert.FromBase64String(transactionString))}");
        var transactionJson = JsonSerializer.Deserialize<JsonDocument>(Convert.FromBase64String(transactionString));
        var transaction =
            JsonSerializer.Deserialize<Transaction>(transactionJson.RootElement.GetProperty("data").GetRawText());
        var state = await daprClient.GetStateEntryAsync<Account>(StoreName, transaction.Id);
        state.Value ??= new Account() { Id = transaction.Id, };
        logger.LogInformation("Id is {0}, the amount to be deposited is {1}", transaction.Id, transaction.Amount);

        if (transaction.Amount < 0m)
        {
            return BadRequest(new { statusCode = 400, message = "bad request" });
        }

        state.Value.Balance += transaction.Amount;
        logger.LogInformation("Balance is {0}", state.Value.Balance);
        await state.SaveAsync();
        return state.Value;
    }

    /// <summary>
    /// Method for returning a BadRequest result which will cause Dapr sidecar to throw an RpcException
    /// </summary>
    [HttpPost("throwException")]
    public async Task<ActionResult<Account>> ThrowException(Transaction transaction,
        [FromServices] DaprClient daprClient)
    {
        logger.LogInformation("Enter ThrowException");
        var task = Task.Delay(10);
        await task;
        return BadRequest(new { statusCode = 400, message = "bad request" });
    }

    /// <summary>
    /// <para>
    /// Method which uses <see cref="CustomTopicAttribute" /> for binding this endpoint to a subscription.
    /// </para>
    /// <para>
    /// This endpoint will be bound to a subscription where the topic name is the value of the environment variable 'CUSTOM_TOPIC'
    /// and the pubsub name is the value of the environment variable 'CUSTOM_PUBSUB'.
    /// </para>
    /// </summary>
    [CustomTopic("%CUSTOM_PUBSUB%", "%CUSTOM_TOPIC%")]
    [HttpPost("exampleCustomTopic")]
    public ActionResult<Account> ExampleCustomTopic(Transaction transaction)
    {
        return Ok();
    }

    /// <summary>
    /// Method which uses <see cref="TopicMetadataAttribute" /> for binding this endpoint to a subscription and adds routingkey metadata.
    /// </summary>
    /// <param name="transaction"></param>
    /// <returns></returns>
    [Topic("pubsub", "topicmetadata")]
    [TopicMetadata("routingKey", "keyA")]
    [HttpPost("examplecustomtopicmetadata")]
    public ActionResult<Account> ExampleCustomTopicMetadata(Transaction transaction)
    {
        return Ok();
    }
}