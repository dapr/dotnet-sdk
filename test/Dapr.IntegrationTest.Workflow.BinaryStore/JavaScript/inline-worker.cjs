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
const { WorkflowRuntime } = require("@dapr/dapr");

// Deliberately no payload resolver, reference contracts, or binary-store client/configuration.
assert.equal(process.env.STORE_HOST, undefined);
assert.equal(process.env.STORE_NAME, undefined);
const runtime = new WorkflowRuntime({ daprHost: process.env.DAPR_HOST, daprPort: "50001" });
let activityCalls = 0;
let workflowCalls = 0;

function assertDocument(input) {
  assert.equal(Object.hasOwn(input, "$daprRef"), false);
  assert.equal(typeof input.content, "string");
  assert.equal(input.sequence, Number.MAX_SAFE_INTEGER);
  assert.equal(input.enabled, false);
  assert.equal(input.optional, null);
  assert.deepEqual(input.items, ["", "\u00e9", "YWJj"]);
}

runtime.registerActivityWithName("inline", (_ctx, input) => {
  assertDocument(input);
  activityCalls++;
  return { ...input, content: input.content + "|javascript" };
});
runtime.registerWorkflowByName("inline", function* (ctx, input) {
  assert.equal(ctx.hasPayloadResolver(), false);
  assertDocument(input);
  if (!ctx.isReplaying()) workflowCalls++;
  return yield ctx.callActivity("inline", input);
});

const server = http.createServer((request, response) => {
  if (request.method === "GET" && request.url === "/health") {
    response.writeHead(runtime.isReady ? 200 : 503, { "Content-Type": "application/json" })
      .end(JSON.stringify({ activityCalls, workflowCalls }));
  } else {
    response.writeHead(404).end();
  }
});

runtime.start().then(() => server.listen(8080, "0.0.0.0")).catch((error) => {
  console.error(error);
  process.exit(1);
});

process.on("SIGTERM", async () => {
  server.close();
  await runtime.stop();
});
