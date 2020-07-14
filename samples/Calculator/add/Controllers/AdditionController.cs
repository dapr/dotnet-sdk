// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using Microsoft.AspNetCore.Mvc;

namespace Addition.Controllers
{
    [ApiController]
    public class AdditionController : ControllerBase
    {

        [HttpGet]
        [Route("{op1?}/{op2?}")]
        public decimal Add(decimal op1, decimal op2)
        {
            Console.WriteLine($"Add {op1} to {op2}");
            return op1 + op2;
        }
    }
}
