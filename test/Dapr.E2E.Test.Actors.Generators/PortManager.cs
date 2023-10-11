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