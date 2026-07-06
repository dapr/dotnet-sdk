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

public class SemVerVersionStrategyTests
{
    [Fact]
    public void TryParse_ShouldParseWithDefaultPrefix()
    {
        var strategy = new SemVerVersionStrategy();

        var parsed = strategy.TryParse("Ordersv1.2.3", out var canonical, out var version);

        Assert.True(parsed);
        Assert.Equal("Orders", canonical);
        Assert.Equal("1.2.3", version);
    }

    [Fact]
    public void Compare_ShouldRespectSemVerRules()
    {
        var strategy = new SemVerVersionStrategy();

        Assert.True(strategy.Compare("1.2.3", "1.3.0") < 0);
        Assert.True(strategy.Compare("1.2.3-alpha", "1.2.3") < 0);
        Assert.Equal(0, strategy.Compare("1.0.0+build1", "1.0.0+build2"));
    }

    [Fact]
    public void TryParse_ShouldAllowNoSuffix_WhenConfigured()
    {
        var services = new ServiceCollection();
        services.AddOptions<SemVerVersionStrategyOptions>(Options.DefaultName)
            .Configure(o => o.AllowNoSuffix = true);

        using var provider = services.BuildServiceProvider();
        var factory = new DefaultWorkflowVersionStrategyFactory();
        var strategy = (SemVerVersionStrategy)factory.Create(
            typeof(SemVerVersionStrategy),
            canonicalName: "Orders",
            optionsName: null,
            services: provider);

        Assert.True(strategy.TryParse("Orders", out var canonical, out var version));
        Assert.Equal("Orders", canonical);
        Assert.Equal("0.0.0", version);
    }
}
