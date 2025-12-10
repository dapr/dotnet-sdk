// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Worker.Internal;

/// <summary>
/// Logger that only logs when not replaying workflow history.
/// </summary>
internal sealed class ReplaySafeLogger(ILogger innerLogger, Func<bool> isReplaying) : ILogger
{
    private readonly ILogger _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
    private readonly Func<bool> _isReplaying = isReplaying ?? throw new ArgumentNullException(nameof(isReplaying));

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _innerLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => !_isReplaying() && _innerLogger.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? ex,
        Func<TState, Exception?, string> formatter)
    {
        // Only log if not replaying
        if (!_isReplaying())
        {
            _innerLogger.Log(logLevel, eventId, state, ex, formatter);
        }
    }
}
