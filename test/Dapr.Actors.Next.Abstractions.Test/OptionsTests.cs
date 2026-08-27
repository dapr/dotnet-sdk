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

    [MinimumDaprRuntimeFact("1.18")]
    public void RegisterActor_WithTypeOptions_StoresConfiguredValues()
    {
        var options = new DaprActorsOptions();

        options.Actors.RegisterActor<OptionsTestActor>(typeOptions =>
        {
            typeOptions.IdleTimeout = TimeSpan.FromMinutes(2);
            typeOptions.DrainRebalancedActorsTimeout = TimeSpan.FromSeconds(10);
            typeOptions.DrainRebalancedActors = false;
            typeOptions.EnableReentrancy = true;
            typeOptions.MaxReentrantDepth = 4;
            typeOptions.DisableStateMigration = true;
        });

        var registration = options.Actors.Find(typeof(OptionsTestActor));

        Assert.NotNull(registration);
        Assert.Null(registration!.ActorTypeName);
        var typeOptions = registration.TypeOptions;
        Assert.NotNull(typeOptions);
        Assert.Equal(TimeSpan.FromMinutes(2), typeOptions!.IdleTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), typeOptions.DrainOngoingCallTimeout);
        Assert.False(typeOptions.DrainRebalancedActors);
        Assert.True(typeOptions.EnableReentrancy);
        Assert.Equal(4, typeOptions.MaxReentrantDepth);
        Assert.True(typeOptions.DisableStateMigration);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void RegisterActor_NullLiteral_ResolvesToNameOverload()
    {
        var options = new DaprActorsOptions();

        options.Actors.RegisterActor<OptionsTestActor>(null);

        var registration = options.Actors.Find(typeof(OptionsTestActor));

        Assert.NotNull(registration);
        Assert.Null(registration!.ActorTypeName);
        Assert.Null(registration.TypeOptions);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Options_DrainRebalancedActorsTimeout_AliasesDrainOngoingCallTimeout()
    {
        var options = new DaprActorsOptions();
        Assert.Null(options.DrainOngoingCallTimeout);
        Assert.Null(options.DrainRebalancedActorsTimeout);

        options.DrainRebalancedActorsTimeout = TimeSpan.FromSeconds(7);
        Assert.Equal(TimeSpan.FromSeconds(7), options.DrainOngoingCallTimeout);
        Assert.Equal(TimeSpan.FromSeconds(7), options.DrainRebalancedActorsTimeout);

        options.DrainOngoingCallTimeout = null;
        Assert.Null(options.DrainRebalancedActorsTimeout);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void TypeOptions_DrainRebalancedActorsTimeout_AliasesDrainOngoingCallTimeout()
    {
        var typeOptions = new DaprActorTypeOptions();

        typeOptions.DrainRebalancedActorsTimeout = TimeSpan.FromSeconds(7);
        Assert.Equal(TimeSpan.FromSeconds(7), typeOptions.DrainOngoingCallTimeout);
        Assert.Equal(TimeSpan.FromSeconds(7), typeOptions.DrainRebalancedActorsTimeout);

        typeOptions.DrainRebalancedActorsTimeout = null;
        Assert.Null(typeOptions.DrainOngoingCallTimeout);
        Assert.Null(typeOptions.DrainRebalancedActorsTimeout);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void RegisterActor_WithNameAndTypeOptions_StoresBoth()
    {
        var options = new DaprActorsOptions();

        options.Actors.RegisterActor<OptionsTestActor>("Renamed", typeOptions => typeOptions.IdleTimeout = TimeSpan.FromMinutes(5));

        var registration = options.Actors.Find(typeof(OptionsTestActor));

        Assert.NotNull(registration);
        Assert.Equal("Renamed", registration!.ActorTypeName);
        Assert.Equal(TimeSpan.FromMinutes(5), registration.TypeOptions?.IdleTimeout);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void RegisterActor_RejectsNullConfigureAndEmptyName()
    {
        var options = new DaprActorsOptions();

        Assert.Throws<ArgumentNullException>(() => options.Actors.RegisterActor<OptionsTestActor>((Action<DaprActorTypeOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => options.Actors.RegisterActor<OptionsTestActor>("Named", null!));
        Assert.Throws<ArgumentException>(() => options.Actors.RegisterActor<OptionsTestActor>("   ", _ => { }));
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void RegisterActor_LastRegistrationWins_ReplacesTypeOptions()
    {
        var options = new DaprActorsOptions();

        options.Actors.RegisterActor<OptionsTestActor>(typeOptions => typeOptions.IdleTimeout = TimeSpan.FromMinutes(1));
        options.Actors.RegisterActor<OptionsTestActor>("Plain");

        Assert.Null(options.Actors.Find(typeof(OptionsTestActor))!.TypeOptions);

        options.Actors.RegisterActor<OptionsTestActor>(typeOptions => typeOptions.MaxReentrantDepth = 2);

        var registration = options.Actors.Find(typeof(OptionsTestActor));
        Assert.Null(registration!.ActorTypeName);
        Assert.Equal(2, registration.TypeOptions?.MaxReentrantDepth);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Validator_AcceptsValidTypeOptions()
    {
        var options = new DaprActorsOptions();
        options.Actors.RegisterActor<OptionsTestActor>(typeOptions =>
        {
            typeOptions.IdleTimeout = TimeSpan.FromSeconds(30);
            typeOptions.DrainRebalancedActorsTimeout = TimeSpan.FromSeconds(5);
            typeOptions.MaxReentrantDepth = 1;
        });

        var result = new DaprActorsOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Validator_ReturnsEveryTypeOptionsFailure()
    {
        var options = new DaprActorsOptions
        {
            DefaultContractVersion = 0,
        };
        options.Actors.RegisterActor<OptionsTestActor>(typeOptions =>
        {
            typeOptions.IdleTimeout = TimeSpan.Zero;
            typeOptions.DrainRebalancedActorsTimeout = TimeSpan.FromSeconds(-1);
            typeOptions.MaxReentrantDepth = 0;
        });

        var result = new DaprActorsOptionsValidator().Validate("named", options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        var failures = result.Failures!;
        Assert.Contains("DefaultContractVersion must be greater than zero.", failures);
        Assert.Contains("IdleTimeout for actor type 'OptionsTestActor' must be greater than zero.", failures);
        Assert.Contains("DrainOngoingCallTimeout for actor type 'OptionsTestActor' must be greater than zero.", failures);
        Assert.Contains("DrainRebalancedActorsTimeout for actor type 'OptionsTestActor' must be greater than zero.", failures);
        Assert.Contains("MaxReentrantDepth for actor type 'OptionsTestActor' must be greater than zero.", failures);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Validator_UsesExplicitActorTypeNameInFailures()
    {
        var options = new DaprActorsOptions();
        options.Actors.RegisterActor<OptionsTestActor>("Renamed", typeOptions =>
        {
            typeOptions.IdleTimeout = TimeSpan.Zero;
            typeOptions.MaxReentrantDepth = 0;
        });

        var result = new DaprActorsOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        var failures = result.Failures!;
        Assert.Contains("IdleTimeout for actor type 'Renamed' must be greater than zero.", failures);
        Assert.Contains("MaxReentrantDepth for actor type 'Renamed' must be greater than zero.", failures);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void Validator_IgnoresUnsetTypeOptionsFields()
    {
        var options = new DaprActorsOptions();
        options.Actors.RegisterActor<OptionsTestActor>(typeOptions => typeOptions.DrainRebalancedActors = false);

        var result = new DaprActorsOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void AddDaprActors_PreservesTypeOptionsThroughOptionsBinding()
    {
        var services = new ServiceCollection();

        services.AddDaprActors(options => options.Actors.RegisterActor<OptionsTestActor>(typeOptions =>
        {
            typeOptions.IdleTimeout = TimeSpan.FromSeconds(90);
            typeOptions.EnableReentrancy = true;
        }));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DaprActorsOptions>>().Value;
        var registration = options.Actors.Find(typeof(OptionsTestActor));

        Assert.NotNull(registration);
        var typeOptions = registration!.TypeOptions;
        Assert.NotNull(typeOptions);
        Assert.Equal(TimeSpan.FromSeconds(90), typeOptions!.IdleTimeout);
        Assert.True(typeOptions.EnableReentrancy);
        Assert.Null(typeOptions.DrainOngoingCallTimeout);
        Assert.Null(typeOptions.DrainRebalancedActors);
        Assert.Null(typeOptions.DrainRebalancedActorsTimeout);
        Assert.Null(typeOptions.MaxReentrantDepth);
        Assert.Null(typeOptions.DisableStateMigration);
    }

    private sealed class OptionsTestActor : IActor;
}
