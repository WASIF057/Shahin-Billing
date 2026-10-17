# Error pages (404 and 502)

There are two different "not working" situations, and they are handled in two different places.

| Error | What it means | Where it is handled |
|---|---|---|
| **404, page not found** | The address does not exist on your site. | **The website itself.** Done: any unknown address shows a branded "We couldn't find that page" screen (the site's catch-all route). Nothing to set up. |
| **502 / 503 / 504, server not answering** | Your server (or the tunnel to it) is down, restarting, or too busy. | **Two layers**, below. |

## 502 layer 1: inside the website (already done)

If the website is already open in someone's browser when the server stops answering, a full-screen **"We can't reach the server"** screen
appears by itself, with the sleeping mattress, a "Try again now" button and a countdown. It checks `https://your-site/api/health` in the
background (every 5 seconds at first, then a little less often, up to 30 seconds) and goes away on its own when the server is back. If the
device has no internet, it says "You're offline" instead.

`GET /api/health` answers `200 {"status":"ok"}` when the API and the database both respond, and `503` when the database does not. You can
also point an uptime monitor at it (for example UptimeRobot, free) to be told by email when the site is down.

## 502 layer 2: a branded page from Cloudflare (you set this up)

A visitor who opens the site while the server is down never receives the website at all. Cloudflare answers with its own grey
"Bad gateway / Web server is down" page. [`error-pages/502.html`](error-pages/502.html) is a ready-made replacement. It is one file with
no scripts and nothing to download, so it works even when your server is off. It refreshes itself every 30 seconds.

**The page has to be hosted somewhere that stays up when your server is down.** Good free choices:

- **Cloudflare Pages** (the easiest, you are already on Cloudflare): Workers & Pages, Create, Pages, "Upload assets", upload a folder that
  holds `502.html`. You get an address like `https://shahin-error.pages.dev/502.html`.
- or **GitHub Pages** for a repository that holds the file.

Then tell Cloudflare to use it. In the Cloudflare dashboard open **Custom Pages** (under your site, or Account, Configurations, Custom
Pages), find **"5XX Errors"** (or "Always Online" / server errors) and choose **Custom page**, then paste the address of your hosted
`502.html`. Cloudflare only offers custom 5XX pages on some plans (generally Pro and above). If your plan does not show the option, the
branded screen from layer 1 still covers people who already have the site open.

If you serve the site with nginx on the same machine, `error_page 502 503 504 /502.html;` can show the same file, but that only works
while nginx itself is up.

## Planned maintenance

When you update the server, you can use the same page on purpose: host it, and temporarily point Cloudflare at it. Edit the text in
`502.html` first if you want to say when you will be back. Remember to change the wording again afterwards.

## Changing the wording or the phone number

- The 404 page and the "can't reach the server" screen are components in the website: `features/not-found/not-found.ts` and
  `shared/server-down.ts`.
- The standalone page is plain HTML: edit `docs/error-pages/502.html`. It shows the phone number from your price list, so please check it.
