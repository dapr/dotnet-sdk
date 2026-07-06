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

namespace Dapr.Workflow.Versioning.Runtime.Test;

public class WorkflowVersioningServiceCollectionExtensionsTests
{
    [Fact]
    public void UseDefaultWorkflowStrategy_UsesNamedOptions()
    {
        var services = new ServiceCollection();
        services.AddDaprWorkflowVersioning();

        const string optionsName = "workflow-defaults";
        services.UseDefaultWorkflowStrategy<DateVersionStrategy>(optionsName);
        services.ConfigureStrategyOptions<DateVersionStrategyOptions>(optionsName, o =>
        {
            o.DateFormat = "yyyyMMddHHmmss";
        });

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<WorkflowVersioningOptions>();
        var strategy = options.DefaultStrategy?.Invoke(provider);

        Assert.NotNull(strategy);
        Assert.True(strategy!.TryParse("VacationApprovalWorkflow20260212153700", out var canonical, out var version));
        Assert.Equal("VacationApprovalWorkflow", canonical);
        Assert.Equal("20260212153700", version);
    }
}
