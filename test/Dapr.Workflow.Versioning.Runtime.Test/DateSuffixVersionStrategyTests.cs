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

public class DateSuffixVersionStrategyTests
{
    [Fact]
    public void TryParse_ShouldParseDefaultFormat()
    {
        var strategy = new DateSuffixVersionStrategy();

        var parsed = strategy.TryParse("MyWorkflow20260212", out var canonical, out var version);

        Assert.True(parsed);
        Assert.Equal("MyWorkflow", canonical);
        Assert.Equal("20260212", version);
    }

    [Fact]
    public void TryParse_ShouldRejectNoSuffix_ByDefault()
    {
        var strategy = new DateSuffixVersionStrategy();

        Assert.False(strategy.TryParse("MyWorkflow", out _, out _));
    }

    [Fact]
    public void TryParse_ShouldAllowNoSuffix_WhenEnabled()
    {
        var services = new ServiceCollection();
        services.AddOptions<DateSuffixVersionStrategyOptions>(Options.DefaultName)
            .Configure(o => o.AllowNoSuffix = true);

        using var provider = services.BuildServiceProvider();
        var factory = new DefaultWorkflowVersionStrategyFactory();
        var strategy = (DateSuffixVersionStrategy)factory.Create(
            typeof(DateSuffixVersionStrategy),
            canonicalName: "Orders",
            optionsName: null,
            services: provider);

        Assert.True(strategy.TryParse("Orders", out var canonical, out var version));
        Assert.Equal("Orders", canonical);
        Assert.Equal("0", version);
    }

    [Fact]
    public void TryParse_ShouldUseNamedFormatFromFactory()
    {
        var services = new ServiceCollection();
        services.AddOptions<DateSuffixVersionStrategyOptions>("custom")
            .Configure(o => o.DateFormat = "yyyy-MM-dd");

        using var provider = services.BuildServiceProvider();
        var factory = new DefaultWorkflowVersionStrategyFactory();
        var strategy = (DateSuffixVersionStrategy)factory.Create(
            typeof(DateSuffixVersionStrategy),
            canonicalName: "Orders",
            optionsName: "custom",
            services: provider);

        Assert.True(strategy.TryParse("Orders2026-02-12", out var canonical, out var version));
        Assert.Equal("Orders", canonical);
        Assert.Equal("2026-02-12", version);
    }

    [Fact]
    public void TryParse_ShouldReadFromDate()
    {
        var services = new ServiceCollection();
        const string optionsName = "workflow-defaults";
        services.AddOptions<DateSuffixVersionStrategyOptions>(optionsName)
            .Configure(o => o.DateFormat = "yyyyMMddHHmmss");

        using var provider = services.BuildServiceProvider();
        var factory = new DefaultWorkflowVersionStrategyFactory();
        var strategy = (DateSuffixVersionStrategy)factory.Create(
            typeof(DateSuffixVersionStrategy),
            canonicalName: "",
            optionsName: optionsName,
            services: provider);
        
        Assert.True(strategy.TryParse("VacationApprovalWorkflow20260212153700", out var canonical, out var version));
        Assert.Equal("VacationApprovalWorkflow", canonical);
        Assert.Equal("20260212153700", version);
    }

    [Fact]
    public void Compare_ShouldOrderByDate()
    {
        var strategy = new DateSuffixVersionStrategy();

        Assert.True(strategy.Compare("20260101", "20261231") < 0);
        Assert.True(strategy.Compare("20261231", "20260101") > 0);
    }

    [Fact]
    public void Compare_ShouldPreferValidDateOverInvalid()
    {
        var strategy = new DateSuffixVersionStrategy();

        Assert.True(strategy.Compare("20261231", "not-a-date") > 0);
        Assert.True(strategy.Compare("bad", "20260101") < 0);
    }
}
