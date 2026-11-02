
using Microsoft.AspNetCore.SignalR;
using FollowMeGPS.Services;

namespace FollowMeGPS.Hubs;

public class LocationHub : Hub
{
    private readonly LocationService _locationService;

    public LocationHub(LocationService locationService)
    {
        _locationService = locationService;
    }

    // Create an independent sharing session
    public SessionCredentials StartSharing(string displayName)
    {
        try
        {
            return _locationService.StartSession(displayName);
        }
        catch (ArgumentException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    // Receive and broadcast this owner's GPS coordinates
    public async Task UpdateLocation(
        string token,
        double latitude,
        double longitude)
    {
        if (!_locationService.UpdateLocation(
                token, latitude, longitude))
        {
            throw new HubException(
                "Invalid or expired owner session.");
        }

        var viewerToken =
            _locationService.GetViewerTokenForOwner(token);

        if (viewerToken is null)
            return;

        await Clients.Group(viewerToken).SendAsync(
            "LocationUpdated",
            latitude,
            longitude,
            DateTime.UtcNow);
    }

    // Viewer joins this specific owner's session
    public async Task<bool> JoinSession(string token)
    {
        if (!_locationService.IsValidViewer(token))
            return false;

        await Groups.AddToGroupAsync(
            Context.ConnectionId, token);

        var location = _locationService.GetLocation(token);

        if (location is not null)
        {
            await Clients.Caller.SendAsync(
                "LocationUpdated",
                location.Latitude,
                location.Longitude,
                location.UpdatedAt);
        }

        return true;
    }

    // Retrieve the location belonging to this viewer token
    public LocationData? GetLocation(string token)
    {
        return _locationService.GetLocation(token);
    }

    // Retrieve the display name belonging to this viewer token
    public string? GetDisplayName(string token)
    {
        return _locationService.GetDisplayNameForViewer(token);
    }

    // Stop only this owner's sharing session
    public async Task StopSharing(string token)
    {
        if (!_locationService.IsValidOwner(token))
            throw new HubException("Invalid owner session.");

        var viewerToken =
            _locationService.GetViewerTokenForOwner(token);

        _locationService.StopSession(token);

        if (viewerToken is not null)
        {
            await Clients.Group(viewerToken)
                .SendAsync("SharingStopped");
        }
    }
}
