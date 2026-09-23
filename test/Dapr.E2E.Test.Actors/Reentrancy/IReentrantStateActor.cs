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

using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.E2E.Test.Actors.Reentrancy;

public interface IReentrantStateActor : IPingActor, IActor
{
    // Writes the shared key from an ordinary method call. With reentrancy
    // enabled for the type, this runs on a reentrancy-scoped state tracker.
    Task SetValue(string value);

    // Registers a reminder whose callback reads the shared key. The callback
    // runs on the default state tracker, because no reentrancy id reaches it.
    Task StartReminder();

    // Returns the value the reminder callback read, or an empty string if the
    // reminder has not fired yet.
    Task<string> GetValueSeenByReminder();
}
