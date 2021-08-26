// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest.App
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Dapr;
    using Dapr.Client;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.WebUtilities;

    [ApiController]
    public class DaprController : ControllerBase
    {
        [Topic("pubsub", "B")]
        [HttpPost("/B")]
        public void TopicB()
        {
        }

        [CustomTopic("pubsub", "C")]
        [HttpPost("/C")]
        public void TopicC()
        {
        }

        [Topic("pubsub", "D", true)]
        [HttpPost("/D")]
        public void TopicD()
        {
        }

        [Topic("pubsub", "E", false)]
        [HttpPost("/E")]
        public void TopicE()
        {
        }

        [Topic("pubsub", "register-user")]
        [HttpPost("/register-user")]
        public ActionResult<UserInfo> RegisterUser(UserInfo user)
        {
            return user; // echo back the user for testing
        }

        [Topic("pubsub", "register-user-plaintext")]
        [HttpPost("/register-user-plaintext")]
        public async Task<ActionResult> RegisterUserPlaintext()
        {
            using var reader = new HttpRequestStreamReader(Request.Body, Encoding.UTF8);
            var user = await reader.ReadToEndAsync();
            return Content(user, "text/plain"); // echo back the user for testing
        }

        [HttpPost("/controllerwithoutstateentry/{widget}")]
        public async Task AddOneWithoutStateEntry([FromServices] DaprClient state, [FromState("testStore")] Widget widget)
        {
            widget.Count++;
            await state.SaveStateAsync("testStore", (string)this.HttpContext.Request.RouteValues["widget"], widget);
        }

        [HttpPost("/controllerwithstateentry/{widget}")]
        public async Task AddOneWithStateEntry([FromState("testStore")] StateEntry<Widget> widget)
        {
            widget.Value.Count++;
            await widget.SaveAsync();
        }

        [HttpPost("/controllerwithstateentryandcustomkey/{widget}")]
        public async Task AddOneWithStateEntryAndCustomKey([FromState("testStore", "widget")] StateEntry<Widget> state)
        {
            state.Value.Count++;
            await state.SaveAsync();
        }

        [HttpPost("/echo-user")]
        public ActionResult<UserInfo> EchoUser([FromQuery]UserInfo user)
        {
            // To simulate an action where there's no Dapr attribute, yet MVC still checks the list of available model binder providers.
            return user;
        }

        [HttpGet("controllerwithoutstateentry/{widget}")]
        public ActionResult<Widget> Get([FromState("testStore")] Widget widget)
        {
            return widget;
        }

        [HttpGet("controllerwithstateentry/{widgetStateEntry}")]
        public ActionResult<Widget> Get([FromState("testStore")] StateEntry<Widget> widgetStateEntry)
        {
            return widgetStateEntry.Value;
        }

        [Authorize("Dapr")]
        [HttpPost("/requires-api-token")]
        public ActionResult<UserInfo> RequiresApiToken(UserInfo user)
        {
            return user;
        }
    }
}
