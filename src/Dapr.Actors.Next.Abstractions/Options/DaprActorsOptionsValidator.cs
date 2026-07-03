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
