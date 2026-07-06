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

namespace Dapr.AspNetCore;

/// <summary>
/// Class representing an entry in the DaprBulkMessage.
/// </summary>
/// <typeparam name="TValue">The type of value contained in the data.</typeparam>
public class BulkMessageModel<TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkMessageModel{TValue}"/> class.
    /// </summary>
    public BulkMessageModel() {
    }
        
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkMessageModel{TValue}"/> class.
    /// </summary>
    /// <param name="id">Identifier of the message being processed.</param>
    /// <param name="source">Source for this event.</param>
    /// <param name="type">Type of event.</param>
    /// <param name="specversion">Version of the event spec.</param>
    /// <param name="datacontenttype">Type of the payload.</param>
    /// <param name="data">Payload.</param>
    public BulkMessageModel(string id, string source, string type, string specversion, string datacontenttype, 
        TValue data) {
        this.Id = id;
        this.Source = source;
        this.Type = type;
        this.Specversion = specversion;
        this.Datacontenttype = datacontenttype;
        this.Data = data;
    }

    /// <summary>
    /// Identifier of the message being processed.
    /// </summary>
    public string Id { get; set; }
        
    /// <summary>
    /// Source for this event.
    /// </summary>
    public string Source { get; set; }
        
    /// <summary>
    /// Type of event.
    /// </summary>
    public string Type { get; set; }
        
    /// <summary>
    /// Version of the event spec.
    /// </summary>
    public string Specversion { get; set; }
        
    /// <summary>
    /// Type of the payload.
    /// </summary>
    public string Datacontenttype { get; set; }
        
    /// <summary>
    /// Payload.
    /// </summary>
    public TValue Data { get; set; }
}
