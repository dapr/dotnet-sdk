// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Examples.Timers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprActors();

var app = builder.Build();

app.MapGet("/", () => Results.Content(HomePage(), "text/html; charset=utf-8"));

app.MapPost("/actors/{actorId}/timers", async (
    string actorId,
    ScheduleTimerRequest request,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateActor(proxies, actorId).Schedule(request, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/actors/{actorId}/timers", (
    string actorId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateActor(proxies, actorId).List(cancellationToken));

app.MapGet("/actors/{actorId}/timers/{name}", async (
    string actorId,
    string name,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    var lookup = await CreateActor(proxies, actorId).Get(name, cancellationToken);
    return lookup.Timer is null ? Results.NotFound() : Results.Ok(lookup.Timer);
});

app.MapDelete("/actors/{actorId}/timers/{name}", async (
    string actorId,
    string name,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateActor(proxies, actorId).Cancel(name, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/actors/{actorId}/fired", (
    string actorId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateActor(proxies, actorId).GetFired(cancellationToken));

await app.RunAsync();
return;

static ITimerDemoActor CreateActor(IActorProxyFactory proxies, string actorId) =>
    proxies.Create<ITimerDemoActor>(ActorId.Create(actorId), TimerDemoActor.ActorType);

static string HomePage() => """
    <!doctype html>
    <html lang="en">
    <head>
      <meta charset="utf-8">
      <title>Timers sample</title>
      <style>
        body { font-family: system-ui, sans-serif; max-width: 48rem; margin: 2rem auto; padding: 0 1rem; line-height: 1.5; }
        fieldset { margin: 1rem 0; }
        label { display: inline-block; margin-right: .8rem; }
        input { width: 7rem; }
        button { margin: .2rem .4rem .2rem 0; }
        code { background: #eef; padding: .1rem .3rem; border-radius: 3px; }
        pre { background: #f6f6f6; border: 1px solid #ddd; padding: .6rem; min-height: 6rem; white-space: pre-wrap; }
      </style>
    </head>
    <body>
      <h1>Timers sample</h1>
      <p>Every button calls the actor, which uses <code>IActorTimerScheduler</code> on its own sidecar.
         <strong>Get</strong> and <strong>List</strong> need Dapr runtime 1.19 or later.</p>

      <fieldset>
        <legend>Actor</legend>
        <label>Actor id <input id="actorId" value="demo"></label>
      </fieldset>

      <fieldset>
        <legend>Schedule (create or replace)</legend>
        <label>Name <input id="name" value="heartbeat"></label>
        <label>Due (s) <input id="due" type="number" value="5"></label>
        <label>Period (s) <input id="period" type="number" placeholder="none"></label>
        <label>TTL (s) <input id="ttl" type="number" placeholder="none"></label>
        <label>Message <input id="message" value="beat"></label>
        <div><button onclick="schedule()">Schedule</button></div>
      </fieldset>

      <fieldset>
        <legend>Inspect and cancel</legend>
        <button onclick="list()">List timers</button>
        <button onclick="get()">Get timer by name</button>
        <button onclick="cancel()">Cancel timer by name</button>
        <button onclick="fired()">Show fired messages</button>
      </fieldset>

      <pre id="out">Output appears here.</pre>

      <script>
        const v = id => document.getElementById(id).value.trim();
        const num = id => v(id) === "" ? null : Number(v(id));
        const base = () => `/actors/${encodeURIComponent(v("actorId"))}/timers`;
        async function call(method, url, body) {
          const out = document.getElementById("out");
          out.textContent = `${method} ${url} ...`;
          try {
            const res = await fetch(url, { method, headers: { "content-type": "application/json" }, body: body && JSON.stringify(body) });
            const text = await res.text();
            let pretty = text;
            try { pretty = JSON.stringify(JSON.parse(text), null, 2); } catch {}
            out.textContent = `${method} ${url}\n${res.status} ${res.statusText}\n\n${pretty}`;
          } catch (e) {
            out.textContent = `${method} ${url}\nfailed: ${e}`;
          }
        }
        const schedule = () => call("POST", base(), { name: v("name"), dueSeconds: num("due"), periodSeconds: num("period"), ttlSeconds: num("ttl"), message: v("message") });
        const list = () => call("GET", base());
        const get = () => call("GET", `${base()}/${encodeURIComponent(v("name"))}`);
        const cancel = () => call("DELETE", `${base()}/${encodeURIComponent(v("name"))}`);
        const fired = () => call("GET", `/actors/${encodeURIComponent(v("actorId"))}/fired`);
      </script>
    </body>
    </html>
    """;
