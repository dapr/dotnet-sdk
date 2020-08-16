﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest.App
{
    using System.Threading.Tasks;
    using Dapr;
    using Dapr.Client;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    public class DaprController : ControllerBase
    {
        [Topic("B")]
        [HttpPost("/B")]
        public void TopicB()
        {
        }

        [Topic("register-user")]
        [HttpPost("/register-user")]
        public ActionResult<UserInfo> RegisterUser(UserInfo user)
        {
            return user; // echo back the user for testing
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
    }
}
