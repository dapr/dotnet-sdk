// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using Microsoft.AspNetCore.Mvc;

namespace Subtract.Controllers
{
    [ApiController]
    public class SubtractController : ControllerBase
    {

        [HttpGet]
        [Route("{op1?}/{op2?}")]
        public decimal Subtract(decimal op1, decimal op2)
        {
            Console.WriteLine($"Subtracting {op1} from {op2}");
            return op1 - op2;
        }
    }
}
