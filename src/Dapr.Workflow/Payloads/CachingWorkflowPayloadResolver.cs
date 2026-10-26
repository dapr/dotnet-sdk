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

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Wraps an <see cref="IWorkflowPayloadResolver"/> with a bounded per-execution cache so that an
/// orchestrator replaying a long history does not fetch the same payload repeatedly. One instance
/// is created per orchestrator invocation and discarded afterwards.
/// </summary>
internal sealed class CachingWorkflowPayloadResolver : IWorkflowPayloadResolver
{
    private readonly IWorkflowPayloadResolver _inner;
    private readonly int _capacity;
    private readonly ConcurrentDictionary<(PayloadReference Reference, Type Type), object?> _cache = new();
 
    /// <summary>
    /// Initializes a new instance of the <see cref="CachingWorkflowPayloadResolver"/> class.
    /// </summary>
    /// <param name="inner">The resolver that performs the actual retrieval.</param>
    /// <param name="capacity">The maximum number of resolved payloads to retain. Zero disables caching.</param>
    public CachingWorkflowPayloadResolver(IWorkflowPayloadResolver inner, int capacity)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _capacity = capacity;
    }
 
    /// <inheritdoc />
    public async Task<T> ResolveAsync<T>(PayloadReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();
        var cacheKey = (reference, typeof(T));
        if (_capacity > 0 && _cache.TryGetValue(cacheKey, out var cached))
        {
            return (T)cached!;
        }
 
        var value = await _inner.ResolveAsync<T>(reference, cancellationToken).ConfigureAwait(false);
 
        if (_capacity > 0 && _cache.Count < _capacity)
        {
            _cache.TryAdd(cacheKey, value);
        }
 
        return value;
    }
 
    /// <inheritdoc />
    public Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default)
        => _inner.OpenAsync(reference, cancellationToken);
 
    /// <inheritdoc />
    public Task<PayloadReference<T>> StoreAsync<T>(T value, string instanceId, CancellationToken cancellationToken = default)
        => _inner.StoreAsync(value, instanceId, cancellationToken);
}
