using System.Net;
using System.Net.Sockets;

namespace ShahinBilling.Api.Infrastructure;

/// <summary>
/// The address of the person making the request, for rate limiting.
/// Behind a Cloudflare Tunnel (or any proxy on the same machine or network) every request reaches the API from a local address,
/// so the real visitor is read from CF-Connecting-IP (or X-Forwarded-For). Those headers are believed ONLY when the request
/// itself comes from a local or private address, so someone reaching the API directly cannot pretend to be another visitor.
/// RateLimit:TrustProxyHeaders=false turns the header reading off.
/// </summary>
public static class ClientIp
{
    public static string Of(HttpContext ctx)
    {
        var remote = ctx.Connection.RemoteIpAddress;
        if (remote is { IsIPv4MappedToIPv6: true }) remote = remote.MapToIPv4();

        var trust = ctx.RequestServices.GetService<IConfiguration>()?.GetValue("RateLimit:TrustProxyHeaders", true) ?? true;
        if (trust && remote != null && (IPAddress.IsLoopback(remote) || IsPrivate(remote)))
        {
            var cf = ctx.Request.Headers["CF-Connecting-IP"].ToString().Trim();
            if (IPAddress.TryParse(cf, out var viaCf)) return viaCf.ToString();

            var first = ctx.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
            if (IPAddress.TryParse(first, out var viaXff)) return viaXff.ToString();
        }
        return remote?.ToString() ?? "unknown";
    }

    /// <summary>True when the request itself comes from this machine or a private network (the tunnel or proxy), so its forwarding headers can be believed.</summary>
    public static bool IsLocalOrPrivate(HttpContext ctx)
    {
        var remote = ctx.Connection.RemoteIpAddress;
        if (remote is { IsIPv4MappedToIPv6: true }) remote = remote.MapToIPv4();
        return remote != null && (IPAddress.IsLoopback(remote) || IsPrivate(remote));
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;   // fc00::/7 = unique local
        return false;
    }
}
