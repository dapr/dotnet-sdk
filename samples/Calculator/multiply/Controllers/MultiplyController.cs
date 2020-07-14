using System;
using Microsoft.AspNetCore.Mvc;

namespace Multiply.Controllers
{
    [ApiController]
    public class MultiplyController : ControllerBase
    {

        [HttpGet]
        [Route("{op1?}/{op2?}")]
        public decimal Multiply(decimal op1, decimal op2)
        {
            Console.WriteLine($"Multiplying {op1} with {op2}");
            return op1 * op2;
        }
    }
}
