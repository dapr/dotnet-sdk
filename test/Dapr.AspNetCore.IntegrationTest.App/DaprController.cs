// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest.App
{
    using System.Threading.Tasks;
    using Dapr;
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

        [HttpPost("/controllerwithoutstateentry/{testStore}/{widget}")]
        public async Task AddOneWithoutStateEntry([FromServices] StateClient state, [FromState("testStore")] Widget widget)
        {
            widget.Count++;
            await state.SaveStateAsync("testStore", (string)this.HttpContext.Request.RouteValues["widget"], widget);
        }

        [HttpPost("/controllerwithstateentry/{testStore}/{widget}")]
        public async Task AddOneWithStateEntry([FromState("testStore")] StateEntry<Widget> widget)
        {
            widget.Value.Count++;
            await widget.SaveAsync();
        }

        [HttpPost("/controllerwithstateentryandcustomkey/{testStore}/{widget}")]
        public async Task AddOneWithStateEntryAndCustomKey([FromState("testStore", "widget")] StateEntry<Widget> state)
        {
            state.Value.Count++;
            await state.SaveAsync();
        }
    }
}