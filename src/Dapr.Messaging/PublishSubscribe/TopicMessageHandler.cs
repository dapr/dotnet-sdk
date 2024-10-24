﻿// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// The handler delegate responsible for processing the topic message.
/// </summary>
/// <param name="request">The message request to process.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The acknowledgement behavior to report back to the pub/sub endpoint about the message.</returns>
public delegate Task<TopicResponseAction> TopicMessageHandler(TopicMessage request,
    CancellationToken cancellationToken = default);
