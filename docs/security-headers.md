# Security headers

Browser-protection headers for the live site. Two places are involved:

| Where | What | Done by |
|---|---|---|
| **The API** (every answer) | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, and `Cache-Control: no-store` on `/api/*` | The app itself (`SecurityHeadersMiddleware`). Nothing to set up. Switch off with `Security:Headers=false`. |
| **The website files** (the page and its scripts) | `Content-Security-Policy` (CSP) and HSTS | **You**, where the website files are served: Cloudflare or the web server. The steps are below. |
| **HTTPS only** | Redirect `http://` to `https://`, HSTS, and an app-level safety net | **You** in Cloudflare (main switch), plus `Security:RequireHttps` in the app. See "Forcing HTTPS" below. |

The CSP is the important one. It tells the browser which scripts, styles, images and connections the page may use, so a script an
attacker manages to slip into a page cannot run or send data out. It also protects the login session kept in the browser.

## The policy

If the website and the API are on the **same address** (for example the site at `https://billing.example.com` and the API at
`https://billing.example.com/api`):

```
default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com data:; img-src 'self' data: blob:; connect-src 'self'; frame-src 'self' blob:; object-src 'self' blob:; base-uri 'self'; form-action 'self'; frame-ancestors 'none'
```

If the API is on a **different address** (for example `https://api.example.com`), add it to `connect-src`:

```
... connect-src 'self' https://api.example.com; ...
```

What each part is for:

| Part | Why |
|---|---|
| `script-src 'self'` | Only the site's own scripts run. Inline scripts are not allowed. The build is set up so the page needs none (`inlineCritical` is off in `angular.json`). |
| `style-src 'self' 'unsafe-inline' https://fonts.googleapis.com` | The site's own styles, plus Google Fonts. `'unsafe-inline'` is needed because Angular and PrimeNG add small style blocks while the page runs. This is the usual, accepted compromise for Angular. |
| `font-src ... https://fonts.gstatic.com data:` | The text fonts (Manrope and Montserrat come from Google Fonts). |
| `img-src 'self' data: blob:` | The logo, and the login-check picture (a `data:` image). |
| `connect-src 'self'` | The site may talk only to itself (the API). |
| `frame-src 'self' blob:` and `object-src 'self' blob:` | The PDF preview in the bill format editor is shown from a `blob:` address. |
| `frame-ancestors 'none'` | No other site may show this one inside a frame. |
| `base-uri 'self'`, `form-action 'self'` | Closes two small tricks attackers use. |

This policy was tested against the built site with a real API and a real login. The dashboard (with its chart), the bill format editor
with its PDF preview, new bill, items, settings and the privacy page all ran with **no violations**.

## Turning it on in Cloudflare

1. Cloudflare dashboard, choose your site, then **Rules**, then **Transform Rules**, then **Modify Response Header**, then **Create rule**.
2. Name it "Security headers". For the filter choose **All incoming requests** (or "Hostname equals your site").
3. Under **Modify response header** add these as **Set static** (name and value):

   | Header name | Value |
   |---|---|
   | `Content-Security-Policy` | the policy above |
   | `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` |

   The other headers (`X-Content-Type-Options` and so on) already come from the API. Add them here too if the website files are served
   separately from the API:

   | Header name | Value |
   |---|---|
   | `X-Content-Type-Options` | `nosniff` |
   | `X-Frame-Options` | `DENY` |
   | `Referrer-Policy` | `strict-origin-when-cross-origin` |
   | `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=(), usb=()` |

4. **Deploy**. Then open the site in a browser, press F12 and look at the **Console**. Any problem shows as "Refused to ..." with
   the reason.

**Roll it out safely:** first use the header name `Content-Security-Policy-Report-Only` with the same value. The browser then only
reports problems in the Console and blocks nothing. Use the site normally for a day, and when the Console is clean, rename the header
to `Content-Security-Policy`.

HSTS makes browsers use HTTPS only. Turn it on only when the whole site works over HTTPS. You can also set it in Cloudflare under
**SSL/TLS, Edge Certificates, HTTP Strict Transport Security**.

## If you serve the files with nginx instead

```nginx
add_header Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com data:; img-src 'self' data: blob:; connect-src 'self'; frame-src 'self' blob:; object-src 'self' blob:; base-uri 'self'; form-action 'self'; frame-ancestors 'none'" always;
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-Frame-Options "DENY" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
```

## When something changes

Update the policy when you:

- add a new outside service (a new font host, analytics, a payment page, a chat widget): add its address to the matching part;
- move the API to a different address: change `connect-src`;
- see "Refused to ..." in the browser Console after a change.

Do **not** add `'unsafe-inline'` or `'unsafe-eval'` to `script-src` to make an error go away. That removes the main protection. Find
what is trying to run inline code and fix that instead.

## Forcing HTTPS (no plain http)

Your site is reached through a Cloudflare Tunnel, so visitors first meet **Cloudflare**, and Cloudflare passes them to your server.
That is where "http to https" should be forced, before a request ever reaches your server. The app adds a second safety net.

### 1. In Cloudflare (the main switch)

Cloudflare dashboard, choose your site, then:

| Where | Setting | Value |
|---|---|---|
| **SSL/TLS, Overview** | Encryption mode | **Full** (the tunnel already encrypts the link to your server, so you do not need an origin certificate) |
| **SSL/TLS, Edge Certificates** | **Always Use HTTPS** | **On**. Anyone opening `http://...` is sent to `https://...` by Cloudflare. |
| **SSL/TLS, Edge Certificates** | **Automatic HTTPS Rewrites** | **On**. Fixes old `http://` links inside pages. |
| **SSL/TLS, Edge Certificates** | **Minimum TLS Version** | **1.2** |
| **SSL/TLS, Edge Certificates** | **HTTP Strict Transport Security (HSTS)** | **Enable**, max age 6 months to start (12 months once you are sure), include subdomains **on**, no-sniff **on**. After a visitor's first visit their browser refuses to use plain http for your site at all. |

HSTS is hard to undo: once browsers have seen it they refuse http until it expires. Turn it on only after the whole site, including
your ordering pages and email links, works on https. Start with the short max age.

### 2. In the app (the safety net)

In the **server's** `appsettings.Production.json` add:

```json
"Security": {
  "RequireHttps": true
}
```

and restart the API. Then, if a plain-http request ever reaches the app:

- a page request (GET) is sent on to the `https://` address (308 redirect);
- anything else, such as a login, is refused with "This site only works over HTTPS", because the password would already have crossed the internet unprotected.

How the app tells http from https: through the tunnel it reads Cloudflare's `CF-Visitor` and `X-Forwarded-Proto` headers, and believes them
only from a connection on the same machine or a private network. A request with **no** such header from the same machine (a health check, you
logging in to the server itself) is let through, so a missing header can never lock the site out. It is off by default, so local development
over `http://localhost` keeps working.

### 3. In the website build

The address of the API in the live build must be `https://` (or simply the same address as the site). A page loaded over https that
calls an `http://` API is blocked by the browser. Check `src/environments/environment.ts` (or the production build's replacement) before
you build for the live site.

You can also add `upgrade-insecure-requests;` to the end of the Content Security Policy above. It tells browsers to turn any stray
`http://` request from the page into `https://`.

### 4. Check that it works

From any computer:

```
curl -I http://your-site-address/
```

You should see a `301` (Cloudflare) or `308` response with `Location: https://your-site-address/`. Then open
`https://www.ssllabs.com/ssltest/` and enter your address for a full report on the HTTPS setup.

## Not covered here

The login session is still kept in the browser's storage (see the Cookie policy). A stricter option, keeping it in a cookie that
scripts cannot read at all, would need changes to login, logout and the API's authentication. The CSP above makes it much harder for
an attacker script to run in the first place.
