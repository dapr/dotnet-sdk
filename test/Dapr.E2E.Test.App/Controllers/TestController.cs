// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace ControllerSample.Controllers
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
        /// Echoes the input string
        /// </summary>
        /// <param name="name">string to echo.</param>
        /// <returns>string with the input name in it.</returns>
        [HttpGet("hello/{name}")]
        public ActionResult<string> Get(string name)
        {
            var result = string.Format($"Hello {name}!");
            logger.LogInformation(result);
            return result;
        }
    }
}
