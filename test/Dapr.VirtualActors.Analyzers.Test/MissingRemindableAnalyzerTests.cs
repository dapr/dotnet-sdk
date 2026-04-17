// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Dapr.Analyzers.Common;

namespace Dapr.VirtualActors.Analyzers.Test;

public class MissingRemindableAnalyzerTests
{
    [Fact]
    public async Task ImplementsIVirtualActorRemindable_NoDiagnostic()
    {
        const string testCode = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Dapr.VirtualActors;
            using Dapr.VirtualActors.Runtime;

            public class MyActor(VirtualActorHost host) : VirtualActor(host), IVirtualActorRemindable
            {
                public Task RegisterMyReminder() =>
                    RegisterReminderAsync("r1", null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

                public Task ReceiveReminderAsync(string reminderName, byte[]? state,
                    TimeSpan dueTime, TimeSpan period, CancellationToken ct = default)
                    => Task.CompletedTask;
            }
            """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MissingRemindableAnalyzer>(testCode);
    }

    [Fact]
    public async Task UsesRemindersWithoutInterface_ReportsDiagnosticDAPRVACT005()
    {
        const string testCode = """
            using System;
            using System.Threading.Tasks;
            using Dapr.VirtualActors;
            using Dapr.VirtualActors.Runtime;

            public class BadActor(VirtualActorHost host) : VirtualActor(host)
            {
                public Task RegisterMyReminder() =>
                    RegisterReminderAsync("r1", null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(AnalyzerDiagnostics.MissingRemindableInterface)
            .WithSpan(6, 14, 6, 22)
            .WithMessage("Actor type 'BadActor' registers reminders but does not implement IVirtualActorRemindable");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MissingRemindableAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task NoReminderRegistration_NoDiagnostic()
    {
        const string testCode = """
            using System.Threading.Tasks;
            using Dapr.VirtualActors;
            using Dapr.VirtualActors.Runtime;

            public class CleanActor(VirtualActorHost host) : VirtualActor(host)
            {
                public Task DoWorkAsync() => Task.CompletedTask;
            }
            """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MissingRemindableAnalyzer>(testCode);
    }

    [Fact]
    public async Task AbstractActorUsesReminders_NoDiagnostic()
    {
        // Abstract actors are skipped — they cannot be instantiated directly
        const string testCode = """
            using System;
            using System.Threading.Tasks;
            using Dapr.VirtualActors;
            using Dapr.VirtualActors.Runtime;

            public abstract class BaseActor(VirtualActorHost host) : VirtualActor(host)
            {
                protected Task RegisterBaseReminder() =>
                    RegisterReminderAsync("base", null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            }
            """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MissingRemindableAnalyzer>(testCode);
    }

    [Fact]
    public async Task ClassNotInheritingVirtualActor_NoDiagnostic()
    {
        const string testCode = """
            using System.Threading.Tasks;

            public class SomeService
            {
                public Task RegisterReminderAsync(string name, byte[]? state,
                    System.TimeSpan dueTime, System.TimeSpan period)
                    => Task.CompletedTask;

                public Task CallIt() =>
                    RegisterReminderAsync("x", null, System.TimeSpan.Zero, System.TimeSpan.FromSeconds(5));
            }
            """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MissingRemindableAnalyzer>(testCode);
    }
}
