using System;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;

namespace AuthServer1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RequestAuthenticationController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
        "User authentication successful", 
    };

        private readonly ILogger<RequestAuthenticationController> _logger;

        public RequestAuthenticationController(ILogger<RequestAuthenticationController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetApproval")]
        public AuthenticationCall Get()
        {
            Thread.Sleep(3000);
            return new AuthenticationCall
            {
                Approved = true,
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            };
        }
    }
}
