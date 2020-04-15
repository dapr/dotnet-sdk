using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Twilio.AspNet.Common;
using Twilio.AspNet.Core;
using System.Text.Json;

namespace TestForControllers.Controllers
{


        /// <summary>
        /// Handles interactions from Twilio
        /// </summary>
  [ApiController]
  [Route("[controller]")]
  public class TwilioPostController : ControllerBase
  {

        /// <summary>
        /// Handles Post from Twilio
        /// </summary>
    [HttpPost]
    public string Post([FromForm] VoiceRequest voiceRequest)
    {
            if (this.ModelState.IsValid)
                return JsonSerializer.Serialize(voiceRequest);
            else
                return null;
    }
  }
}
