using System.Net.NetworkInformation;

namespace Dapr.E2E.Test.Actors.Generators;

internal sealed class PortManager
{
    private readonly ISet<int> reservedPorts = new HashSet<int>();

    private readonly object reservationLock = new();

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
                    .Where(port => !this.reservedPorts.Contains(port))
                    .Take(count);

            return availablePorts.ToHashSet();
        }
    }
}