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

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Structural and behavioral verification result for an interpreted machine definition.
/// </summary>
public sealed record InterpretedMachineVerificationResult(IReadOnlyList<string> Defects)
{
    /// <summary>
    /// Gets a value indicating whether no defects were found.
    /// </summary>
    public bool IsValid => Defects.Count == 0;

    /// <summary>
    /// Throws when the definition has verification defects.
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("Interpreted machine verification failed: " + string.Join("; ", Defects));
        }
    }
}
