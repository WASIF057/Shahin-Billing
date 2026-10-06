# Shahin Enterprises – GST Billing

A web app that replaces the paper bill book: business details, items with client-specific rates,
clients, GST tax invoices (CGST/SGST or IGST worked out automatically), PDF bills, payments,
dashboard, and GSTR-1 Excel reports.

```
shahin-billing/
  CLAUDE.md        full specification and business rules (read this first when changing anything)
  backend/         ASP.NET Core Web API (.NET 10) + MongoDB
  frontend/        Angular 21 + PrimeNG app
```

---

## 1. Install these once

| Tool | Version | Check with |
|---|---|---|
| .NET SDK | 10.x (see note below for .NET 8) | `dotnet --version` |
| Node.js | 22.12 or newer (LTS) | `node -v` |
| MongoDB | Local Community Server, or a free MongoDB Atlas cluster | `mongosh` |

**Only have the .NET 8 SDK?** In both `.csproj` files change `net10.0` to `net8.0`, and in
`ShahinBilling.Api.csproj` change the JwtBearer version from `10.*` to `8.*`.

---

## 2. Run the backend (API)

```bash
cd backend/src/ShahinBilling.Api
cp appsettings.example.json appsettings.Development.json      # Windows: copy appsettings.example.json appsettings.Development.json
```

Open `appsettings.Development.json` and set:
- `Mongo:ConnectionString`: `mongodb://localhost:27017` for local Mongo, or your Atlas connection string.
- `Jwt:Key`: any long random text, **at least 32 characters**. Keep it secret. It lives only in this file (the shared `appsettings.json` has it blank).
- `Smtp`: your mail server, to send bills, login codes and password resets (see "Email, login codes and backups" below). Optional but recommended.

This file is git-ignored, so your secrets never get committed.

```bash
dotnet run
```

The API starts on **http://localhost:5080**. Open **http://localhost:5080/swagger** to see every endpoint.

Run the unit tests (GST maths, rates, numbering, GSTIN, amount in words, reminders, import rules):

```bash
cd backend
dotnet test
```

The same GST rules also run in the browser (live totals while you type), so they have their own tests with the same numbers:

```bash
cd frontend/shahin-billing-web
npm run test:unit
```

---

### Email, login codes and backups

**Email (optional).** In `appsettings.Development.json` fill in `Smtp`: `Host`, `Port`, `Username`, `Password`, `FromEmail`, `FromName`, `UseStartTls`.
For Gmail use `smtp.gmail.com`, port 587, and a 16-letter **App Password** (Google account → Security → 2-Step Verification → App passwords), not your normal password.
Restart the API, then open **Email templates** and use the send-test button on a template.

**Login security.** While email is set up, logging in asks for a 6-digit code emailed to your business email (My business → Security turns it on or off).
Creating an account also confirms the email with a 6-digit code first (no account is made until it is entered). "Forgot password?" on the login screen resets the password with an emailed code. If email ever breaks and you are locked out, set `"Security": { "RequireOtp": false }` in `appsettings.Development.json` and restart.
Auth endpoints are limited to 10 requests a minute per computer.

**Google sign-in (optional, off until configured).** Create an OAuth *Web* client at console.cloud.google.com (APIs & Services → Credentials), add `http://localhost:4200` as an authorized JavaScript origin, and put the client id in `appsettings.Development.json` as `"Google": { "ClientId": "….apps.googleusercontent.com" }`.
Only an existing account whose email matches the Google email can sign in this way.

**Backups.** The API saves a backup file of each business once a day in `backups/` beside the app (git-ignored) and keeps the newest 14. Change this with `Backup:Folder`, `Backup:Keep` or `Backup:Enabled`.
You can still download one any time from Reports. Copy the folder somewhere safe too: a backup on the same computer does not protect you if the computer is lost.

### Staff, activity, reminders, import

- **Team** (owner only): add logins for your staff. Staff can bill, take payments and add clients; they can't see reports or change settings. Switching a login off ends its session straight away. **My account** (everyone) has "Log out of all devices".
- **Activity** (owner only): who did what and when, kept for a year.
- **Payment reminders:** *Send payment reminder* in the All bills menu emails the client for one bill. **Email templates → Automatic payment reminders** can send them for you (off until you switch it on; 9 am–7 pm only).
- **Import** (owner only): add many clients or items from Excel. Download the template, fill it in, **Check the file**, then import. Items need their type set up in Item setup first.

### Client ordering website

Your clients can log in, choose products (names only, **never prices**) and place orders; you and your staff see them under **Orders**.
1. **Clients → ⋮ Ordering login** (the shopping-cart icon) on a client who has an email address. They get an invitation email and set their own password with *Forgot password?*.
2. Set `App:PublicUrl` in `appsettings.Development.json` to your website address so the invitation shows the link.
3. While the app is open, owner and staff get a popup and a ring when an order arrives (a browser needs one click on the page before it will play sound; use the sound switch in the sidebar to turn it off or test it). Accepting or cancelling an order emails the client.
4. When a client orders, they and you get an email listing the items (edit the wording under **Email templates**, "A client places an order"). On **Orders**, *Make the bill* opens Create Bill already filled in.
5. **Phone orders**: when a client calls, use **Orders → Take an order**. Pick the client, add products and quantities, choose a priority and tick whether to email the client and/or yourself. A phone order starts as *Accepted* and does not ring the alert. Clients can also mark their own order Normal, High or Urgent; urgent orders are listed first and carry a badge.

## 3. Run the frontend

In a second terminal:

```bash
cd frontend/shahin-billing-web
npm install
npm start
```

Open **http://localhost:4200**.

The API address is in `src/environments/environment.ts`. Change it if your API runs elsewhere.

---

## 4. First steps in the app

1. **Create account**: enter your business name, your name, email and a password.
2. **My business**: fill in GSTIN (state fills in automatically), address, bank details, logo, signature,
   and bill numbering (for example prefix `SE` → `SE-0001`; Non-GST bills get their own prefix, default `NG`).
   - To try everything quickly, click **Add demo data** at the bottom (works in Development mode only).
3. **Item setup**, then **Items**: first add your types (Bed, Pillow…) with their sizes, variants and cloth types in **Item setup**, then add each item by picking from those lists, with GST % and default rate.
   Use **Special rates** to give selected clients a different rate (multi-select).
4. **Clients**: add the businesses you sell to, with GSTIN, state, mobile number and the cities they have stores in.
5. **Create bill**: pick the client, pick items, type quantities. Then:
   - **Save & download PDF** finalizes the bill and downloads the PDF.
   - **Save & finalize** finalizes it without downloading.
   - **Save draft** keeps it editable as a draft.
6. **All bills**: search, filter, record payments, duplicate, cancel, or download Original/Duplicate/Triplicate copies.
7. **Bill format**: change layout, colour, text size and what shows on the PDF, with a live preview.
8. **Reports**: download GSTR-1 Excel for a month, a sales register, or a full backup.

---

## 5. How the GST rules work

Full details are in `CLAUDE.md`, section 4.

- **Rates**: a client's special rate wins; everyone else gets the item's default rate. Rates are before GST.
- **Tax type**: your state vs the bill's *state of supply* (the State dropdown on the bill, which starts as the client's state and can be changed).
  Same state → CGST + SGST (half each). Different state → IGST.
- **Rounding**: tax per line to 2 decimals. The grand total is the exact total (paise kept); there is no round off.
- **Numbers**: one running series that never restarts and is never reused (Non-GST bills have their own series). Bills are cancelled, never deleted.
- **Non-GST bills**: pick "Non-GST bill" on Create Bill. No tax, a plain "BILL" PDF, own numbers, left out of GSTR-1. Check with your accountant that this suits how you sell.
- **Old bills don't change**: each bill stores a copy of business, client and item details at the time.

Please confirm HSN codes, GST rates, and the place-of-supply rule for "bill to / ship to" deliveries with your accountant.

---

## 6. Troubleshooting

| Problem | Fix |
|---|---|
| API stops at start: "Mongo:ConnectionString is not set" or "Jwt:Key must be set" | Create `appsettings.Development.json` as in step 2. |
| Browser says "Can't reach the server" | The API isn't running, or `environment.ts` has the wrong URL. |
| Login asks for a code but none arrives | Check the `Smtp` settings and the business email under My business. To get back in, set `Security:RequireOtp` to `false` and restart. |
| CORS error in the browser console | Add your frontend address to `Cors:Origins` in the appsettings file. |
| Logged out suddenly | The login lasts 8 hours (`Jwt:ExpiryHours`). Log in again. |
| `dotnet build` errors | Paste the error to Claude along with `CLAUDE.md`. |

---

## 7. Going live (later)

- **Database**: a MongoDB Atlas cluster.
- **API**: Azure App Service, a VPS, or any host that runs .NET.
  - Set `ASPNETCORE_ENVIRONMENT=Production`.
  - Provide `Mongo__ConnectionString`, `Jwt__Key` and `Cors__Origins__0` as environment variables.
- **Frontend**:
  1. Set the live API URL in `environment.ts`.
  2. Run `npm run build`.
  3. Host the `dist/shahin-billing-web/browser` folder on any static host (Azure Static Web Apps, Netlify, Nginx, etc.).
- **Licences**: QuestPDF (the PDF library) is free under its Community licence for businesses with
  under USD 1M annual revenue.
