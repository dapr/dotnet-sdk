using System.Reflection;
using Dapr.Actors.Next.Abstractions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Abstractions.Test;

public sealed class OptionsTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public void Validator_AcceptsDefaultOptions()
    {
        var result = new DaprActorsOptionsValidator().Validate(null, new DaprActorsOptions());

        Assert.True(result.Succeeded);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Validator_ReturnsEveryFailure()
    {
        var options = new DaprActorsOptions
        {
            DefaultContractVersion = 0,
            ActorIdleTimeout = TimeSpan.Zero,
            DrainRebalancedActorsTimeout = TimeSpan.Zero,
            MaxReentrantDepth = 0,
        };

        var result = new DaprActorsOptionsValidator().Validate("named", options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        var failures = result.Failures!;
        Assert.Contains("DefaultContractVersion must be greater than zero.", failures);
        Assert.Contains("ActorIdleTimeout must be greater than zero.", failures);
        Assert.Contains("DrainRebalancedActorsTimeout must be greater than zero.", failures);
        Assert.Contains("MaxReentrantDepth must be greater than zero.", failures);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActors_BindsOptionsAndValidatorWithoutLifetimeOverload()
    {
        var services = new ServiceCollection();

        services.AddDaprActors(options =>
        {
            options.DefaultContractVersion = 2;
            options.EnableReentrancy = true;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DaprActorsOptions>>().Value;
        var validators = provider.GetServices<IValidateOptions<DaprActorsOptions>>();

        Assert.Equal(2, options.DefaultContractVersion);
        Assert.True(options.EnableReentrancy);
        Assert.Contains(validators, validator => validator is DaprActorsOptionsValidator);

        var methods = typeof(DaprActorsServiceCollectionExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(DaprActorsServiceCollectionExtensions.AddDaprActors))
            .ToArray();

        var method = Assert.Single(methods);
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(IServiceCollection), parameters[0].ParameterType);
        Assert.Equal(typeof(Action<DaprActorsOptions>), parameters[1].ParameterType);
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType.FullName == "Microsoft.Extensions.DependencyInjection.ServiceLifetime");
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActors_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => DaprActorsServiceCollectionExtensions.AddDaprActors(null!, _ => { }));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActors_AllowsNullConfigureAndUsesDefaults()
    {
        var services = new ServiceCollection();

        services.AddDaprActors(null);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DaprActorsOptions>>().Value;

        Assert.Equal(1, options.DefaultContractVersion);
        Assert.True(options.EnableAutoActorRegistration);
        Assert.Contains(provider.GetServices<IValidateOptions<DaprActorsOptions>>(), validator => validator is DaprActorsOptionsValidator);
    }
}
