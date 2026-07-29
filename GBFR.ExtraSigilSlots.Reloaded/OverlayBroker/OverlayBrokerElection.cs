using GBFR.OverlayHub.Contracts;
using Reloaded.Mod.Interfaces;

namespace GBFR.OverlayHub.Runtime;

internal sealed record OverlayBrokerElection(
    IGbfrOverlayHub Hub,
    IOverlayBrokerHostControl? HostControl)
{
    internal bool IsHost => HostControl is not null;
}

/// <summary>
/// Elects the first-loaded compatible peer as the neutral Broker carrier.
/// The process-local mutex keeps election atomic across separate mod load contexts.
/// </summary>
internal static class OverlayBrokerElectionService
{
    internal static OverlayBrokerElection Elect(
        IModLoader loader,
        IMod owner,
        string modId,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(log);

        using var electionMutex = new Mutex(
            initiallyOwned: false,
            $"Local\\GBFR.OverlayBroker.Election.{Environment.ProcessId}");
        var acquired = false;
        try
        {
            try
            {
                acquired = electionMutex.WaitOne(TimeSpan.FromSeconds(5));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
                log("Overlay Broker recovered an abandoned process-local election lock.");
            }
            if (!acquired)
                throw new TimeoutException("Timed out waiting for the process-local Overlay Broker election lock.");

            if (TryGetCompatibleHub(loader, out var existing))
            {
                if (existing is IRecoverableGbfrOverlayHub recoverable &&
                    !recoverable.IsHostAvailable)
                {
                    var recoveredHost = recoverable.TryAcquireHost(modId);
                    if (recoveredHost is not null)
                    {
                        try
                        {
                            loader.AddOrReplaceController<IGbfrOverlayHub>(owner, existing);
                            log($"Overlay Broker election transferred the graphics-writer lease to '{modId}'.");
                            return new OverlayBrokerElection(existing, recoveredHost);
                        }
                        catch
                        {
                            recoveredHost.MarkHostUnavailable("controller ownership transfer failed");
                            throw;
                        }
                    }
                }
                log($"Overlay Broker election joined bootstrap peer '{existing.HostModId}'.");
                return new OverlayBrokerElection(existing, null);
            }

            var endpoints = OverlayBrokerFactory.Create(modId, log);
            loader.AddOrReplaceController<IGbfrOverlayHub>(owner, endpoints.Hub);
            if (!TryGetCompatibleHub(loader, out var published))
                throw new InvalidOperationException("Reloaded-II did not publish the elected Overlay Broker controller.");
            if (!ReferenceEquals(published, endpoints.Hub))
            {
                log($"Overlay Broker election lost to bootstrap peer '{published.HostModId}'; joining it as a peer.");
                return new OverlayBrokerElection(published, null);
            }

            log($"Overlay Broker election selected first-loaded peer '{modId}' as bootstrap carrier.");
            return new OverlayBrokerElection(endpoints.Hub, endpoints.Host);
        }
        finally
        {
            if (acquired)
                electionMutex.ReleaseMutex();
        }
    }

    private static bool TryGetCompatibleHub(IModLoader loader, out IGbfrOverlayHub hub)
    {
        hub = null!;
        if (loader.GetController<IGbfrOverlayHub>() is not { } controller ||
            !controller.TryGetTarget(out var candidate) ||
            candidate is null)
        {
            return false;
        }
        if (candidate.ApiVersion != OverlayHubProtocol.ApiVersion)
        {
            throw new InvalidOperationException(
                $"An incompatible GBFR Overlay Broker API is already loaded: " +
                $"host={candidate.HostModId}, api={candidate.ApiVersion}, required={OverlayHubProtocol.ApiVersion}.");
        }
        hub = candidate;
        return true;
    }
}
