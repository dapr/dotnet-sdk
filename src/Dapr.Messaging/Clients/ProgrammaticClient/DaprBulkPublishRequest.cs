// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

namespace Dapr.Messaging.Clients.ProgrammaticClient;

/// <summary>
/// Information about the type being published via the bulk publish operation. 
/// </summary>
/// <param name="Payload">The data to serialize in the event.</param>
/// <param name="DataContentType">The optional data content type. This defaults to "application/json" is not set.</param>
/// <typeparam name="TValue">The type to serialize.</typeparam>
public sealed record DaprBulkPublishRequest<TValue>(TValue Payload, string DataContentType = "application/json");
