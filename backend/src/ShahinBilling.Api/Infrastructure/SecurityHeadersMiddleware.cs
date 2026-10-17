namespace ShahinBilling.Api.Infrastructure;

/// <summary>
/// Adds the standard browser-protection headers to every response, and tells browsers and proxies not to keep copies of API answers
/// (they hold private data). Switch off with Security:Headers=false.
/// The Content-Security-Policy for the website itself is set where the website files are served; see docs/security-headers.md.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next, IConfiguration config)
{
    private readonly bool _on = config.GetValue("Security:Headers", true);

    public Task InvokeAsync(HttpContext ctx)
    {
        if (!_on) return next(ctx);

        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";                                   // never guess a file's type
            h["X-Frame-Options"] = "DENY";                                             // cannot be shown inside another site's frame
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";                  // do not leak full addresses to other sites
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            if (ctx.Request.Path.StartsWithSegments("/api") && !h.ContainsKey("Cache-Control"))
            {
                h["Cache-Control"] = "no-store";                                       // answers hold private business data
                h["Pragma"] = "no-cache";
            }
            return Task.CompletedTask;
        });
        return next(ctx);
    }
}
