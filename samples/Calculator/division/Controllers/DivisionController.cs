// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using Microsoft.AspNetCore.Mvc;

namespace Division.Controllers
{
    [ApiController]
    public class DivisionController : ControllerBase
    {

        [HttpGet]
        [Route("{op1?}/{op2?}")]
        public decimal Divide(decimal op1, decimal op2)
        {
            Console.WriteLine($"Divide {op1} with {op2}");
            return op1 / op2;
        }
    }
}
