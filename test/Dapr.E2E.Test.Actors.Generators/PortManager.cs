// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

using System.Net.NetworkInformation;

namespace Dapr.E2E.Test.Actors.Generators;

internal sealed class PortManager
{
    private readonly ISet<int> reservedPorts = new HashSet<int>();

    private readonly object reservationLock = new();

    public int ReservePort(int rangeStart = 55000)
    {
        var ports = this.ReservePorts(1, rangeStart);

        return ports.First();
    }

    public (int, int) ReservePorts(int rangeStart = 55000)
    {
        var ports = this.ReservePorts(2, rangeStart).ToArray();

        return (ports[0], ports[1]);
    }

    public ISet<int> ReservePorts(int count, int rangeStart = 55000)
    {
        lock (this.reservationLock)
        {
            var globalProperties = IPGlobalProperties.GetIPGlobalProperties();

            var activePorts =
                globalProperties
                    .GetActiveTcpListeners()
                    .Select(endPoint => endPoint.Port)
                    .ToHashSet();

            var availablePorts =
                Enumerable
                    .Range(rangeStart, Int32.MaxValue - rangeStart + 1)
                    .Where(port => !activePorts.Contains(port))
                    .Where(port => !this.reservedPorts.Contains(port));

            var newReservedPorts = availablePorts.Take(count).ToHashSet();

            this.reservedPorts.UnionWith(newReservedPorts);

            return newReservedPorts;
        }
    }
}