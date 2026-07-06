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

public class DefaultWorkflowVersionStrategyFactoryTests
{
    [Fact]
    public void Create_ShouldConfigureContextConsumer()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var factory = new DefaultWorkflowVersionStrategyFactory();

        var strategy = factory.Create(typeof(TestContextStrategy), "Orders", "orders-options", services);
        var typed = Assert.IsType<TestContextStrategy>(strategy);

        Assert.Equal("Orders", typed.Context.CanonicalName);
        Assert.Equal("orders-options", typed.Context.OptionsName);
    }

    private sealed class TestContextStrategy : IWorkflowVersionStrategy, IWorkflowVersionStrategyContextConsumer
    {
        public WorkflowVersionStrategyContext Context { get; private set; }

        public void Configure(WorkflowVersionStrategyContext context)
        {
            Context = context;
        }

        public bool TryParse(string typeName, out string canonicalName, out string version)
        {
            canonicalName = typeName;
            version = "0";
            return true;
        }

        public int Compare(string? v1, string? v2) => 0;
    }
}
