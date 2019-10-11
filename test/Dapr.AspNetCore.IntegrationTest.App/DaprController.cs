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
        [HttpPost("/topic-b")]
        public void TopicB()
        {
        }

        [HttpPost("/controllerwithoutstateentry/{widget}")]
        public async Task AddOneWithoutStateEntry([FromServices]StateClient state, [FromState] Widget widget)
        {
            widget.Count++;
            await state.SaveStateAsync((string)this.HttpContext.Request.RouteValues["widget"], widget);
        }

        [HttpPost("/controllerwithstateentry/{widget}")]
        public async Task AddOneWithStateEntry(StateEntry<Widget> widget)
        {
            widget.Value.Count++;
            await widget.SaveAsync();
        }

        [HttpPost("/controllerwithstateentryandcustomkey/{widget}")]
        public async Task AddOneWithStateEntryAndCustomKey([FromState("widget")] StateEntry<Widget> state)
        {
            state.Value.Count++;
            await state.SaveAsync();
        }
    }
}