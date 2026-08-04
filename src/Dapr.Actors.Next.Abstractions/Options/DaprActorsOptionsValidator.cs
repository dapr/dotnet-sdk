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

using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Abstractions.Options;

/// <summary>
/// Validates <see cref="DaprActorsOptions"/>.
/// </summary>
public sealed class DaprActorsOptionsValidator : IValidateOptions<DaprActorsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DaprActorsOptions options)
    {
        List<string>? failures = null;

        AddFailureIf(options.DefaultContractVersion <= 0, "DefaultContractVersion must be greater than zero.", ref failures);
        AddFailureIf(options.ActorIdleTimeout <= TimeSpan.Zero, "ActorIdleTimeout must be greater than zero.", ref failures);
        AddFailureIf(options.DrainOngoingCallTimeout <= TimeSpan.Zero, "DrainOngoingCallTimeout must be greater than zero.", ref failures);
        AddFailureIf(options.DrainRebalancedActorsTimeout <= TimeSpan.Zero, "DrainRebalancedActorsTimeout must be greater than zero.", ref failures);
        AddFailureIf(options.MaxReentrantDepth <= 0, "MaxReentrantDepth must be greater than zero.", ref failures);

        foreach (var registration in options.Actors.Registrations)
        {
            if (registration.TypeOptions is not { } typeOptions)
            {
                continue;
            }

            var actorName = registration.ActorImplementationType.Name;
            AddFailureIf(typeOptions.IdleTimeout <= TimeSpan.Zero, $"IdleTimeout for actor type '{actorName}' must be greater than zero.", ref failures);
            AddFailureIf(typeOptions.DrainOngoingCallTimeout <= TimeSpan.Zero, $"DrainOngoingCallTimeout for actor type '{actorName}' must be greater than zero.", ref failures);
            AddFailureIf(typeOptions.MaxReentrantDepth <= 0, $"MaxReentrantDepth for actor type '{actorName}' must be greater than zero.", ref failures);
        }

        return failures is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void AddFailureIf(bool condition, string message, ref List<string>? failures)
    {
        if (!condition)
            return;

        failures ??= [];
        failures.Add(message);
    }
}
