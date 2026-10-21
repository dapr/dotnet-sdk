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

using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

// The actor callback stream is app-initiated over gRPC: the sidecar needs the
// HTTP/2 app channel configured in the project's appsettings.json (port 5058).
builder.AddProject<Projects.Timers_Next_Example08>("timers")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "actors-example-08",
        AppPort = 5058,
        AppProtocol = "grpc",
    });

builder.Build().Run();
