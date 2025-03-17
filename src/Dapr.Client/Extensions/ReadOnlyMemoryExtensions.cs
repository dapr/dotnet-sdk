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

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Dapr.Client;

internal static class ReadOnlyMemoryExtensions
{
    public static MemoryStream CreateMemoryStream(this ReadOnlyMemory<byte> memory, bool isReadOnly)
    {
        if (memory.IsEmpty)
        {
            return new MemoryStream(Array.Empty<byte>(), !isReadOnly);
        }

        if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
        {
            return new MemoryStream(segment.Array!, segment.Offset, segment.Count, !isReadOnly);
        }

        throw new ArgumentException(nameof(memory), "Unable to create MemoryStream from provided memory value");
    }
}
