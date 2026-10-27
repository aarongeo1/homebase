using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Homebase.Application;
using Homebase.Devices;
using Homebase.Domain;
using Homebase.Domain.Devices;
using Homebase.Domain.Sun;
using Homebase.Testing;

namespace Homebase.Application.Tests
{
    internal sealed class HomeFixture
    {
        public HomeFixture(DateTime? nowUtc = null, GeoLocation location = null)
        {
            Clock = new FixedClock(nowUtc ?? TestHome.DefaultNow);
            Home = TestHome.Create(Clock.UtcNow);
            Tv = new FakeTvDriver();
            Services = new HomeServices(
                Home.AsRepositories(),
                new DriverRegistry(new IDeviceDriver[] { new SimulatedDriver(), new PushDriver(), Tv, new FakeLightDriver(DriverKeys.GoveeLan), new FakeLightDriver(DriverKeys.GoveeCloud) }),
                new IDeviceDiscovery[] { new FixtureDiscoveries(Discoveries) },
                new HouseholdClock(Clock, TimeZoneInfo.Utc),
                location);
        }

        public FixedClock Clock { get; }

        public InMemoryHome Home { get; }

        public FakeTvDriver Tv { get; }

        public List<IDeviceDiscovery> Discoveries { get; } = new List<IDeviceDiscovery>();

        public HomeServices Services { get; }
    }

    /// <summary>Reads the fixture's list at call time, so tests can add discoveries after the services are built.</summary>
    internal sealed class FixtureDiscoveries : IDeviceDiscovery
    {
        private readonly List<IDeviceDiscovery> _discoveries;

        public FixtureDiscoveries(List<IDeviceDiscovery> discoveries)
        {
            _discoveries = discoveries;
        }

        public async Task<DiscoveryResult> DiscoverAsync(CancellationToken cancellationToken)
        {
            var all = new DiscoveryResult();
            foreach (var discovery in _discoveries)
            {
                var result = await discovery.DiscoverAsync(cancellationToken);
                all.Devices.AddRange(result.Devices);
                all.Notes.AddRange(result.Notes);
            }

            return all;
        }
    }

    internal sealed class FakeDiscovery : IDeviceDiscovery
    {
        public DiscoveryResult Result { get; set; } = new DiscoveryResult();

        public Task<DiscoveryResult> DiscoverAsync(CancellationToken cancellationToken) => Task.FromResult(Result);
    }
}
