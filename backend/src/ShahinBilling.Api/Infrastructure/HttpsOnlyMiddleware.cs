using System.Text.RegularExpressions;

namespace ShahinBilling.Api.Infrastructure;

/// <summary>
/// A safety net for "HTTPS only". The real redirect from http:// to https:// should happen at Cloudflare ("Always Use HTTPS"),
/// before a visitor's request ever reaches this server. If a plain-http request does get through anyway, this stops it:
/// a page request (GET/HEAD) is redirected to the https address, and anything else (a login, a form) is refused,
/// because the person's password would already have crossed the internet unprotected.
///
/// Switched on with Security:RequireHttps=true (off by default so local development over http keeps working).
///
/// How it tells http from https: behind the Cloudflare Tunnel the API itself only ever sees plain http from the tunnel program on the
/// same machine, so it reads Cloudflare's CF-Visitor / X-Forwarded-Proto headers, and only believes them from a local or private
/// connection. A request with no such headers from a local connection is an internal one (a health check, the server itself)
/// and is let through, so a missing header can never lock the site out.
/// </summary>
public partial class HttpsOnlyMiddleware(RequestDelegate next, IConfiguration config)
{
    private readonly bool _on = config.GetValue("Security:RequireHttps", false);

    [GeneratedRegex("\"scheme\"\\s*:\\s*\"(https?)\"", RegexOptions.IgnoreCase)]
    private static partial Regex VisitorScheme();

    public Task InvokeAsync(HttpContext ctx)
    {
        if (!_on || SchemeOf(ctx) != "http") return next(ctx);

        if (HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method))
        {
            var host = ctx.Request.Host.ToUriComponent();
            ctx.Response.StatusCode = StatusCodes.Status308PermanentRedirect;
            ctx.Response.Headers.Location = $"https://{host}{ctx.Request.PathBase}{ctx.Request.Path}{ctx.Request.QueryString}";
            return Task.CompletedTask;
        }

        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        ctx.Response.ContentType = "application/problem+json";
        return ctx.Response.WriteAsync("{\"title\":\"This site only works over HTTPS. Please use the https:// address.\",\"status\":400}");
    }

    /// <summary>"https", "http", or null when nothing says (an internal request).</summary>
    public static string? SchemeOf(HttpContext ctx)
    {
        if (ctx.Request.IsHttps) return "https";
        if (!ClientIp.IsLocalOrPrivate(ctx)) return "http";   // a direct plain-http connection from outside

        var cf = VisitorScheme().Match(ctx.Request.Headers["CF-Visitor"].ToString());
        if (cf.Success) return cf.Groups[1].Value.ToLowerInvariant();

        var xfp = ctx.Request.Headers["X-Forwarded-Proto"].ToString().Split(',')[0].Trim().ToLowerInvariant();
        return xfp is "http" or "https" ? xfp : null;
    }
}
