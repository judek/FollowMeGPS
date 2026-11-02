
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace FollowMeGPS.Services;

public class LocationService
{
    private readonly ConcurrentDictionary<string, SharingSession> _sessions = new();

    public SessionCredentials StartSession()
    {
        string ownerToken = GenerateToken();
        string viewerToken = GenerateToken();

        var expiresAt = DateTime.UtcNow.AddHours(8);

        var session = new SharingSession(
            ownerToken,
            viewerToken,
            expiresAt);

        _sessions[ownerToken] = session;

        return new SessionCredentials(
            ownerToken,
            viewerToken,
            expiresAt);
    }

    public bool UpdateLocation(
        string token,
        double latitude,
        double longitude)
    {
        if (latitude < -90 || latitude > 90 ||
            longitude < -180 || longitude > 180)
            return false;

        if (!TryGetOwnerSession(token, out var session))
            return false;

        lock (session.SyncRoot)
        {
            if (DateTime.UtcNow > session.ExpiresAt)
            {
                _sessions.TryRemove(token, out _);
                return false;
            }

            session.CurrentLocation = new LocationData(
                latitude,
                longitude,
                DateTime.UtcNow);

            return true;
        }
    }

    public LocationData? GetLocation(string token)
    {
        var session = FindViewerSession(token);

        if (session is null)
            return null;

        lock (session.SyncRoot)
        {
            if (DateTime.UtcNow > session.ExpiresAt)
            {
                _sessions.TryRemove(session.OwnerToken, out _);
                return null;
            }

            return session.CurrentLocation;
        }
    }

    public bool IsValidViewer(string token)
    {
        return FindViewerSession(token) is not null;
    }

    public bool IsValidOwner(string token)
    {
        return TryGetOwnerSession(token, out _);
    }

    public string? GetViewerTokenForOwner(string token)
    {
        return TryGetOwnerSession(token, out var session)
            ? session.ViewerToken
            : null;
    }

    public void StopSession(string token)
    {
        if (TryGetOwnerSession(token, out var session))
        {
            _sessions.TryRemove(session.OwnerToken, out _);
        }
    }

    private bool TryGetOwnerSession(
        string token,
        out SharingSession session)
    {
        if (_sessions.TryGetValue(token, out session!))
        {
            if (DateTime.UtcNow <= session.ExpiresAt)
                return true;

            _sessions.TryRemove(token, out _);
        }

        session = null!;
        return false;
    }

    private SharingSession? FindViewerSession(string token)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.ViewerToken == token &&
                DateTime.UtcNow <= session.ExpiresAt)
            {
                return session;
            }
        }

        return null;
    }

    private static string GenerateToken()
    {
        return Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));
    }

    private sealed class SharingSession
    {
        public string OwnerToken { get; }
        public string ViewerToken { get; }
        public DateTime ExpiresAt { get; }

        public object SyncRoot { get; } = new();

        public LocationData? CurrentLocation { get; set; }

        public SharingSession(
            string ownerToken,
            string viewerToken,
            DateTime expiresAt)
        {
            OwnerToken = ownerToken;
            ViewerToken = viewerToken;
            ExpiresAt = expiresAt;
        }
    }
}

public record SessionCredentials(
    string OwnerToken,
    string ViewerToken,
    DateTime ExpiresAt);

public record LocationData(
    double Latitude,
    double Longitude,
    DateTime UpdatedAt);
