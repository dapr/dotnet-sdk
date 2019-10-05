// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Dapr.AspNetCore.IntegrationTest.App
{
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
            await state.SaveStateAsync((string)HttpContext.Request.RouteValues["widget"], widget);
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