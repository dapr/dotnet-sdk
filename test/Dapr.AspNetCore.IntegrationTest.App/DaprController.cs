// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest.App;

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

    [Topic("pubsub", "E", false, "event.type == \"critical\"", 1)]
    [HttpPost("/E-Critical")]
    public void TopicECritical()
    {
    }

    [Topic("pubsub", "E", false, "event.type == \"important\"", 2)]
    [HttpPost("/E-Important")]
    public void TopicEImportant()
    {
    }

    [BulkSubscribe("F")]
    [Topic("pubsub", "F")]
    [Topic("pubsub", "F.1", true)]
    [HttpPost("/multiTopicAttr")]
    public void MultipleTopics()
    {
    }

    [BulkSubscribe("G", 300)]
    [Topic("pubsub", "G", "deadLetterTopicName", false)]
    [HttpPost("/G")]
    public void TopicG()
    {
    }

    [BulkSubscribe("metadata.1", 500, 2000)]
    [Topic("pubsub", "metadata", new string[1] { "id1" })]
    [Topic("pubsub", "metadata.1", true)]
    [HttpPost("/multiMetadataTopicAttr")]
    [TopicMetadata("n1", "v1")]
    [TopicMetadata("id1", "n2", "v2")]
    [TopicMetadata("id1", "n2", "v3")]
    public void MultipleMetadataTopics()
    {
    }

    [Topic("pubsub", "metadataseparator", metadataSeparator: "|")]
    [HttpPost("/topicmetadataseparatorattr")]
    [TopicMetadata("n1", "v1")]
    [TopicMetadata("n1", "v2")]
    public void TopicMetadataSeparator()
    {
    }

    [Topic("pubsub", "metadataseparatorbyemptytring")]
    [HttpPost("/topicmetadataseparatorattrbyemptytring")]
    [TopicMetadata("n1", "v1")]
    [TopicMetadata("n1", "")]
    public void TopicMetadataSeparatorByemptytring ()
    {
    }

    [Topic("pubsub", "splitTopicAttr", true)]
    [HttpPost("/splitTopics")]
    public void SplitTopic()
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
    public ActionResult<UserInfo> EchoUser([FromQuery] UserInfo user)
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