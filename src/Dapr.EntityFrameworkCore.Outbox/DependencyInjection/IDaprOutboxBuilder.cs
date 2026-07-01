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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.EntityFrameworkCore.Outbox.DependencyInjection;

/// <summary>
/// A fluent builder returned from <see cref="DaprOutboxServiceCollectionExtensions.AddDaprOutbox{TDbContext}(IServiceCollection, Action{DaprOutboxOptions}?)"/>
/// used to further customize the outbox registration.
/// </summary>
public interface IDaprOutboxBuilder
{
    /// <summary>
    /// The <see cref="IServiceCollection"/> receiving outbox registrations.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// The <see cref="DbContext"/> type this outbox is bound to.
    /// </summary>
    Type DbContextType { get; }
}
