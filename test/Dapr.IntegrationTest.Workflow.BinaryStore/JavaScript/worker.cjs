/*
Copyright 2026 The Dapr Authors
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

const assert = require("node:assert/strict");
const http = require("node:http");
const {
  BinaryStoreWorkflowPayloadResolver,
  CommunicationProtocolEnum,
  DaprClient,
  DaprWorkflowClient,
  PayloadReference,
  PayloadWritePreference,
  WorkflowRuntime,
} = require("@dapr/dapr");

const binaryClient = new DaprClient({
  daprHost: process.env.STORE_HOST,
  daprPort: "50001",
  communicationProtocol: CommunicationProtocolEnum.GRPC,
});
const payloads = new BinaryStoreWorkflowPayloadResolver(binaryClient.binaryStore, {
  storeName: process.env.STORE_NAME,
  writePreference: PayloadWritePreference.PreferReference,
  offloadThreshold: 1,
});
const options = { daprHost: process.env.DAPR_HOST, daprPort: "50001", payloads };
const client = new DaprWorkflowClient(options);
const runtime = new WorkflowRuntime(options);

function transform(document) {
  assert.equal(typeof document.content, "string");
  assert.equal(document.sequence, Number.MAX_SAFE_INTEGER);
  return { ...document, content: document.content + "|javascript" };
}

runtime.registerActivityWithName("automatic", (_ctx, input) => transform(input));
runtime.registerActivityWithName(
  "reference",
  async (ctx, input) => {
    assert.ok(input instanceof PayloadReference);
    return payloads.store(transform(await payloads.resolve(input)), ctx.getWorkflowInstanceId());
  },
  { inputAsReference: true },
);
runtime.registerWorkflowByName("automatic", function* (ctx, input) {
  return yield ctx.callActivity("automatic", input);
});
runtime.registerWorkflowByName(
  "reference",
  function* (ctx, input) {
    assert.ok(input instanceof PayloadReference);
    // Exercise workflow resolution as well as activity resolution.
    transform(yield ctx.resolvePayload(input));
    return yield ctx.callActivity("reference", input, { resultAsReference: true });
  },
  { inputAsReference: true },
);
runtime.registerWorkflowByName("parent", function* (ctx, input) {
  const reference = PayloadReference.parse(JSON.stringify(input.reference));
  assert.ok(reference);
  const payload = input.explicitReference ? reference : yield ctx.resolvePayload(reference);
  const taskOptions = {
    targetAppId: input.targetAppId,
    allowPayloadReferences: true,
    resultAsReference: true,
  };
  const name = input.explicitReference ? "reference" : "automatic";
  const activity = yield ctx.callActivity(name, payload, taskOptions);
  const child = yield ctx.callChildWorkflow(name, payload, input.childId, taskOptions);
  assert.ok(activity instanceof PayloadReference);
  assert.ok(child instanceof PayloadReference);
  yield ctx.scheduleNewDetachedWorkflow(name, payload, input.detachedId, undefined, taskOptions);
  return {
    activity: yield ctx.resolvePayload(activity),
    child: yield ctx.resolvePayload(child),
  };
});

const server = http.createServer(async (request, response) => {
  try {
    if (request.method === "GET" && request.url === "/health") {
      response.writeHead(runtime.isReady ? 200 : 503).end();
    } else if (request.method === "POST" && request.url === "/start") {
      const chunks = [];
      for await (const chunk of request) chunks.push(chunk);
      const { instanceId, input } = JSON.parse(Buffer.concat(chunks).toString("utf8"));
      await client.scheduleNewWorkflow("parent", input, instanceId);
      response.writeHead(200, { "Content-Type": "application/json" }).end(JSON.stringify({ instanceId }));
    } else {
      response.writeHead(404).end();
    }
  } catch (error) {
    console.error(error);
    response.writeHead(500, { "Content-Type": "text/plain" }).end(String(error.stack ?? error));
  }
});

runtime.start().then(() => server.listen(8080, "0.0.0.0")).catch((error) => {
  console.error(error);
  process.exit(1);
});

process.on("SIGTERM", async () => {
  server.close();
  await runtime.stop();
  await client.stop();
  await binaryClient.stop();
});
