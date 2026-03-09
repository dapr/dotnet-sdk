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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dapr.Workflow.Versioning.Runtime.Test;

public class NumericVersionStrategyTests
{
    [Fact]
    public void TryParse_ShouldParseWithDefaultPrefix()
    {
        var strategy = new NumericVersionStrategy();

        var parsed = strategy.TryParse("MyWorkflowV1", out var canonical, out var version);

        Assert.True(parsed);
        Assert.Equal("MyWorkflow", canonical);
        Assert.Equal("1", version);
    }

    [Fact]
    public void TryParse_ShouldParseDefaultVersion_WhenNoSuffix()
    {
        var strategy = new NumericVersionStrategy();

        var parsed = strategy.TryParse("MyWorkflow", out var canonical, out var version);

        Assert.True(parsed);
        Assert.Equal("MyWorkflow", canonical);
        Assert.Equal("0", version);
    }

    [Fact]
    public void TryParse_ShouldRejectMissingPrefix_WhenDigitsPresent()
    {
        var strategy = new NumericVersionStrategy();

        var parsed = strategy.TryParse("MyWorkflow1", out _, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParse_ShouldParseWithWidth_WhenZeroPadEnabled()
    {
        var services = new ServiceCollection();
        services.AddOptions<NumericVersionStrategyOptions>(Options.DefaultName)
            .Configure(o =>
            {
                o.SuffixPrefix = string.Empty;
                o.ZeroPad = true;
                o.Width = 4;
                o.AllowNoSuffix = false;
            });

        using var provider = services.BuildServiceProvider();
        var factory = new DefaultWorkflowVersionStrategyFactory();
        var strategy = (NumericVersionStrategy)factory.Create(
            typeof(NumericVersionStrategy),
            canonicalName: "Orders",
            optionsName: null,
            services: provider);

        Assert.True(strategy.TryParse("Orders0007", out var canonical, out var version));
        Assert.Equal("Orders", canonical);
        Assert.Equal("0007", version);
    }

    [Fact]
    public void TryParse_ShouldRejectWrongWidth_WhenZeroPadEnabled()
    {
        var services = new ServiceCollection();
        services.AddOptions<NumericVersionStrategyOptions>(Options.DefaultName)
            .Configure(o =>
            {
                o.SuffixPrefix = string.Empty;
                o.ZeroPad = true;
                o.Width = 4;
                o.AllowNoSuffix = false;
            });

        using var provider = services.BuildServiceProvider();
        var factory = new DefaultWorkflowVersionStrategyFactory();
        var strategy = (NumericVersionStrategy)factory.Create(
            typeof(NumericVersionStrategy),
            canonicalName: "Orders",
            optionsName: null,
            services: provider);

        Assert.False(strategy.TryParse("Orders007", out _, out _));
    }

    [Theory]
    [InlineData("1", "2")]
    [InlineData("9", "10")]
    public void Compare_ShouldOrderNumerically(string older, string newer)
    {
        var strategy = new NumericVersionStrategy();

        Assert.True(strategy.Compare(older, newer) < 0);
        Assert.True(strategy.Compare(newer, older) > 0);
    }

    [Fact]
    public void Compare_ShouldPreferNumericOverNonNumeric()
    {
        var strategy = new NumericVersionStrategy();

        Assert.True(strategy.Compare("2", "beta") > 0);
        Assert.True(strategy.Compare("alpha", "3") < 0);
    }

    [Fact]
    public void TryParse_ShouldUseNamedOptionsFromFactory()
    {
        var services = new ServiceCollection();
        services.AddOptions<NumericVersionStrategyOptions>("custom")
            .Configure(o =>
            {
                o.SuffixPrefix = "v";
                o.IgnorePrefixCase = true;
                o.AllowNoSuffix = false;
            });

        using var provider = services.BuildServiceProvider();
        var factory = new DefaultWorkflowVersionStrategyFactory();
        var strategy = (NumericVersionStrategy)factory.Create(
            typeof(NumericVersionStrategy),
            canonicalName: "Orders",
            optionsName: "custom",
            services: provider);

        Assert.True(strategy.TryParse("Ordersv2", out var canonical, out var version));
        Assert.Equal("Orders", canonical);
        Assert.Equal("2", version);
        Assert.False(strategy.TryParse("Orders2", out _, out _));
    }
}
