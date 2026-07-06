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

namespace Dapr.E2E.Test;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Test App invoked by the end-to-end tests
/// </summary>
[ApiController]
public class TestController : ControllerBase
{
    /// <summary>
    /// TestController Constructor with logger injection
    /// </summary>
    /// <param name="logger"></param>
    public TestController(ILogger<TestController> logger)
    {
        this.logger = logger;
    }

    private readonly ILogger<TestController> logger;

    /// <summary>
    /// Returns the account details
    /// </summary>
    /// <param name="transaction">Transaction to process.</param>
    /// <returns>Account</returns>
    [HttpPost("accountDetails")]
    public ActionResult<Account> AccountDetails(Transaction transaction)
    {
        var account = new Account()
        {
            Id = transaction.Id,
            Balance = transaction.Amount + 100
        };
        return account;
    }

    [Authorize("Dapr")]
    [HttpPost("accountDetails-requires-api-token")]
    public ActionResult<Account> AccountDetailsRequiresApiToken(Transaction transaction)
    {
        var account = new Account()
        {
            Id = transaction.Id,
            Balance = transaction.Amount + 100
        };
        return account;
    }

    [Authorize("Dapr")]
    [HttpGet("DelayedResponse")]
    public async Task<IActionResult> DelayedResponse()
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        return Ok();
    }

}