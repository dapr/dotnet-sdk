// ------------------------------------------------------------------------
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
/// 
/// </summary>
public sealed record TopicRequest
{
    /// <summary>
    /// 
    /// </summary>
    public string Id { get; init; } = default!;
    
    /// <summary>
    /// 
    /// </summary>
    public string Source { get; init; } = default!;
    
    /// <summary>
    /// 
    /// </summary>
    public string Type { get; init; } = default!;
    
    /// <summary>
    /// 
    /// </summary>
    public string SpecVersion { get; init; } = default!;
    
    /// <summary>
    /// 
    /// </summary>
    public string DataContentType { get; init; } = default!;
    
    /// <summary>
    /// 
    /// </summary>
    public string Topic { get; init; } = default!;
    
    /// <summary>
    /// 
    /// </summary>
    public string PubSubName { get; init; } = default!;
    
    /// <summary>
    /// 
    /// </summary>
    public string? Path { get; init; }
    
    /// <summary>
    /// 
    /// </summary>
    public object? Extensions { get; init; } // TODO: Determine what this should look like.
}
