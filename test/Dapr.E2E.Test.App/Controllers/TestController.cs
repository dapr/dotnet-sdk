// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.E2E.Test
{
    using System;
    using System.Threading.Tasks;
    using Dapr;
    using Dapr.Client;
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
    }
}
