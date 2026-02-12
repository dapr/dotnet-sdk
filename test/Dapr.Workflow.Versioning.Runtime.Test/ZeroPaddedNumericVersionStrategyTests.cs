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

public class ZeroPaddedNumericVersionStrategyTests
{
    [Fact]
    public void TryParse_ShouldParseWithWidth()
    {
        var strategy = new ZeroPaddedNumericVersionStrategy();

        Assert.True(strategy.TryParse("Orders0007", out var canonical, out var version));
        Assert.Equal("Orders", canonical);
        Assert.Equal("0007", version);
    }

    [Fact]
    public void TryParse_ShouldRejectWrongWidth()
    {
        var strategy = new ZeroPaddedNumericVersionStrategy();

        Assert.False(strategy.TryParse("Orders007", out _, out _));
    }

    [Fact]
    public void TryParse_ShouldAllowNoSuffix_WhenConfigured()
    {
        var services = new ServiceCollection();
        services.AddOptions<ZeroPaddedNumericVersionStrategyOptions>(Options.DefaultName)
            .Configure(o => o.AllowNoSuffix = true);

        using var provider = services.BuildServiceProvider();
        var factory = new DefaultWorkflowVersionStrategyFactory();
        var strategy = (ZeroPaddedNumericVersionStrategy)factory.Create(
            typeof(ZeroPaddedNumericVersionStrategy),
            canonicalName: "Orders",
            optionsName: null,
            services: provider);

        Assert.True(strategy.TryParse("Orders", out var canonical, out var version));
        Assert.Equal("Orders", canonical);
        Assert.Equal("0", version);
    }
}
