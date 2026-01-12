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
//  ------------------------------------------------------------------------

namespace Dapr.TestContainers.Common.Options;

/// <summary>
/// A configurable log level.
/// </summary>
public enum DaprLogLevel
{
    /// <summary>
    /// Represents a "debug" log level.
    /// </summary>
	Debug,
    /// <summary>
    /// Represents an "info" log level.
    /// </summary>
	Info,
    /// <summary>
    /// Represents a "warn" log level.
    /// </summary>
	Warn,
    /// <summary>
    /// Represents an "error" log level.
    /// </summary>
	Error,
    /// <summary>
    /// Represents a "fatal" log level.
    /// </summary>
	Fatal,
    /// <summary>
    /// Represents a "panic" log level.
    /// </summary>
	Panic
}
