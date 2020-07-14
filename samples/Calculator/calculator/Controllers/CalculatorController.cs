// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Subtract.Controllers
{

    [ApiController]
    public class SubtractController : ControllerBase
    {
        static readonly HttpClient client = new HttpClient();
        [HttpGet]
        [Route("{operation}/{op1?}/{op2?}")]
        public async Task<decimal> Operation(string operation, decimal op1, decimal op2)
        {
            Console.WriteLine($"Operation: {operation} - op1: {op1} - op2: {op2}");

            var operationService = operation + "Service";
            HttpResponseMessage response = await client.GetAsync($"http://localhost:3501/v1.0/invoke/{operationService}/method/{op1}/{op2}");

            string responseBody = await response.Content.ReadAsStringAsync();

            return Decimal.Parse(responseBody);
        }
    }
}
