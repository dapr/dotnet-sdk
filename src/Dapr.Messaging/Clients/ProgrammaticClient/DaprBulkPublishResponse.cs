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

using Dapr.Messaging.PublishSubscribe;

namespace Dapr.Messaging.Clients.ProgrammaticClient;

/// <summary>
/// Represents the responses returned for failed bulk publishing events.
/// </summary>
/// <param name="FailedEntries">The list of entries that failed to be published.</param>
public record DaprBulkPublishResponse(IReadOnlyList<DaprBulkPublishResponseFailedEntry> FailedEntries);
