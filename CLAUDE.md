# Shahin Enterprises – B2B GST Billing System

> **Status:** All phases (0–4) in Section 12 are implemented. Use this file as the reference when
> changing or extending the app. Versions in use: .NET 10, MongoDB.Driver 3, Angular 21, PrimeNG 21.
> Notes on the implementation:
> - Data access lives in the `Services/` classes (via `Data/MongoContext.cs`) instead of a separate Repositories layer.
> - Item/client search uses case-insensitive regex (partial matches) instead of Mongo text indexes.
> - Business rules exist twice on purpose: `Services/InvoiceCalculator.cs` (authoritative, on save) and
>   `frontend/.../core/gst.ts` (live totals while typing). Keep them in sync and update both test suites.

## Changes since the original spec (these override anything below that disagrees)

All of these were requested or approved by the owner after the original phases were built.

**Business rules**
- **No round off.** `grandTotal` is the exact total (taxable + taxes, paise kept). `roundOff` is always 0 on new bills; old bills keep theirs, and screens/PDF show a "Round off" line only when it is not 0.
- **Invoice numbers** are prefix + separator + running number, e.g. `SE-0001`. There is no financial year in the number and the counter never resets (`financialYear` is still stored on the invoice for reporting).
- **Non-GST bills.** `invoice.isNonGst` is chosen when the bill is created and is fixed afterwards. Lines are saved with `gstRate` 0, no tax, no GST summary. They have their **own number series** (counter id `{businessId}:nongst`, prefix `invoiceNumbering.nonGstPrefix`, default `NG`; the two prefixes must differ). The PDF is titled by `template.nonGstTitle` (default "BILL") and hides GSTIN, state, place of supply, GST summary and tax rows. They are left out of GSTR-1 and included in sales, dashboard and client reports (marked "Non-GST"). `client.billedWithoutGst` only pre-selects the type on a new bill.
- **Cities instead of addresses.** A client has a `cities` list (no "main" city). Each bill picks one (`invoice.city`, saved in the billTo/shipTo address city). Bill To / Ship To show name, city and mobile number (the PDF also shows GSTIN and state on GST bills). Ship To is either "same as Bill To" or a dropdown of the client's cities. Address lines, pincode and contact person are no longer entered or shown. "Place of Supply" on the screen/PDF is the ship-to city; the GST state dropdown (`placeOfSupplyStateCode`) still decides CGST+SGST vs IGST. "Dispatched From" is the business city.
- **Items** are made from **Item setup** (`productTypes`: name, `usesVariants`, `variants`, `clothTypes`, `clothColours` (a list of colours **for each cloth**, so Polyester can have different colours from Cotton), `sizes`). An item stores `typeId`, `typeName`, `variant`, `cloth`, `colour`, `size`; its `name` is the type name and `sizeOrVariant` is built as "variant · cloth · colour · size". The Colour choice on an item lists only the colours of the cloth chosen, and the colour is cleared if that cloth has none. Colours also appear on orders, the ordering website, Excel import/export (a Colour column) and bills. Older items without a type still work (free text). HSN is removed from the screens and PDF (the field remains, empty). An item or client can be deleted only if no bill uses it (otherwise hide it).
- Removed from the bill: discount input (old bills that have one still show it), due date, e-way bill / LR numbers, PO number (PO date kept). Vehicle number is saved in capitals. "Time of sale" is `createdAt` shown in Indian time.
- **Payment status** can be changed from All bills (it records or removes payments; status stays derived from the payments).

**PDF**
- Items table: #, Item, Qty, Rate, Amount. Totals block: Total, CGST+SGST or IGST (GST bills), Grand Total. The copy label ("Original for Recipient"...) is a bordered box. Bank details are a full-width bold black box. Terms, declaration and signature share one box. Copies (Original/Duplicate/Triplicate) are chosen when downloading.
- Extra PDFs: **payment receipt** (`GET /invoices/{id}/payments/{paymentId}/receipt`), **client statement** (`GET /reports/client-statement`), and a "View bill" preview of an unsaved bill (`POST /invoices/preview-pdf`).

**New features beyond the original phases**
- **Email** (MailKit; `Smtp` section in appsettings, real values only in `appsettings.Development.json`). Named **email templates** (collection `emailTemplates`): `recipient` Client or Business, `triggers` (Generated, Downloaded, PaymentChanged), `attachPdf`, `subject`/`body` with `{{Placeholders}}`, `isActive`. Sent in the background; a failed email never blocks billing; a bill/template pair is emailed at most once per 60 s; payment-change emails wait 6 s so a burst sends one email. The old single `business.emailSettings` only seeds the templates the first time.
- **Bill formats** (collection `billFormats`): named PDF looks; exactly one is active and its settings are copied to `business.template`, which every PDF uses.
- **Reports/exports:** client report (Excel, optional city and date range), client statement (PDF), items export by type (Excel, one sheet per type), sales register has a Bill Type column. **Dashboard** filters by client, city and date range and shows a Paid / Part paid / Unpaid breakdown.
- **Security:** email one-time code on login (6 digits, 10 min, 5 tries, resend after 30 s, sent to the business email; switch `business.requireLoginOtp`; asked only while email is set up; `Security:RequireOtp=false` is the way back in if email breaks; collection `loginChallenges` with a TTL index); **email verification on sign-up** (the account is created only after the emailed code is entered; the sign-up details wait in `loginChallenges` with purpose `register`); **forgot password** by emailed code; rate limit of 10 requests/minute/IP on the auth endpoints; optional **Google sign-in** (`Google:ClientId`; the Google email must match an existing user; the button is hidden while the id is blank); the JWT key is not kept in the shared appsettings files.
- **Automatic backup:** a daily JSON file per business in `Backup:Folder` (default `backups/` beside the app, git-ignored), newest 14 kept; manual export still available. `GET /backup/status` reports the last one.
- **Sidebar:** Dashboard, Create bill, All bills, Item setup, Items, Clients, Import, Reports, Bill formats, Email templates, Team, Activity, My business, plus My account for everyone. Staff only see Create bill, All bills, Orders, Clients and My account. A client's login sees only Order products, My orders and My account.

- **Phone orders and priority:** owner/staff can take an order for a client (`POST /api/orders`, Orders page "Take an order") with optional email copies to the client and/or the business (`emailClient`, `emailMe`). Such orders have `source: Phone`, `takenBy`, start as Accepted and are excluded from the live alert (`/orders/watch`). Every order has a `priority` (Normal, High, Urgent; stored with numeric `priorityRank`); clients choose it when ordering. Open orders (New/Accepted filter) sort urgent first. Email placeholders `{{OrderPriority}}`, `{{OrderSource}}` were added.

**New / changed collections:** `productTypes`, `emailTemplates`, `billFormats`, `loginChallenges`, `activity`, `orders` (new); `clients` (+`cities`, `billedWithoutGst`), `items` (+`typeId`, `typeName`, `variant`, `cloth`, `colour`, `size`), `invoices` (+`isNonGst`, `city`), `businesses` (+`emailSettings`, `requireLoginOtp`, `reminders`, `invoiceNumbering.nonGstPrefix`, `template.nonGstTitle`), `users` (+`isActive`, `tokenVersion`, `clientId`; `role` is Owner, Staff or Client), `invoices` (+`lastReminderAt`, `reminderCount`).

**Added in the "worth adding next" round**
- **Staff logins with roles.** `users.role` is `Owner` or `Staff`. The owner adds staff on the **Team** screen (name, email, password; switch off, set a new password, remove). **Staff can:** create/edit/finalize bills, record payments, download PDFs, send a payment reminder, add/edit clients. **Staff cannot:** see the dashboard or reports, change My business, Item setup, items, bill formats, email templates, import, team or activity, cancel bills, remove payments, hide/delete clients or items. Enforced on the server with `[Authorize(Roles = AppRoles.Owner)]` and in the UI (menu, route guard `ownerGuard`, hidden buttons). A staff login's email codes go to the staff member's own email; the owner's go to the business email.
- **Sessions can be ended.** `users.isActive` and `users.tokenVersion`; the JWT carries a `tv` claim, and `SessionValidator` (10-second cache) rejects a token if the user is switched off or the version changed. The version goes up on password change/reset, "Log out of all devices" (My account page, all users), and when the owner switches off or resets a staff login.
- **Activity log** (`activity` collection, kept 365 days via TTL): who did what and when, for bills, payments, clients, items, setup, email, team, logins, backup and import. Owner-only **Activity** screen with date, person and kind filters. Written through `ApiControllerBase.Log(...)`; a failure to write never blocks the action.
- **Payment reminders.** Email templates can be sent "When: Payment reminder" (`Reminder` trigger; a standard template is added automatically). **Send payment reminder** in the All bills menu emails one bill now. **Automatic reminders** (`business.reminders`: enabled, afterDays 15, repeatEveryDays 7, maxReminders 3; **off by default**) run in `ReminderHostedService` every 3 hours, only between 9 am and 7 pm Indian time. Bills store `lastReminderAt` and `reminderCount`. Placeholder `{{DaysOutstanding}}` is available.
- **Excel import** (owner only, **Import** screen, `POST /api/import/{clients|items}?dryRun=`): download a template, check the file (nothing saved, every row reported OK / Skipped / Error), then import the good rows. Clients: name, GSTIN, state, mobile, email, cities. Items: type, variant, cloth, size, unit, GST %, rate; the type and its choices must already exist in Item setup. Existing clients/items are skipped, so re-importing never duplicates. Max 1000 rows, .xlsx, 2 MB.
- **Tests:** backend xUnit (`dotnet test`) and browser-side Vitest (`npm run test:unit` in the frontend folder) check the same GST numbers, so the live totals on screen and the saved totals on the server agree.

**Client ordering website** (added before go-live)
- A **client's ordering login** is a user with role `Client` and a `clientId` (JWT claim `clientId`). The owner creates it on the Clients screen (**Ordering login**: needs the client's email; the client gets an invitation and sets their own password with "Forgot password?", so nobody shares a password; switch off / resend / remove). There is no self sign-up. Their login codes and reset codes go to their own email.
- **Clients never see prices or other people's data.** `PortalController` (the only controller besides `AuthController` that accepts a client login) returns product names, variants, cloth, size and unit, and the client's own orders: its DTOs have no rate, GST, amount or total field (a unit test enforces that). Every other controller derives from `StaffApiControllerBase` (`[Authorize(Roles = "Owner,Staff")]`), so a client login gets 403 on items, clients, bills, settings, dashboard, orders, team and the rest (an integration test checks 14 endpoints). The client id always comes from the token.
- **Orders** (`orders` collection, number `ORD-0001` from its own counter `{businessId}:order`; status New, Accepted, Billed, Cancelled). A client places an order from the product list (quantity per product, a city if they have several, an optional note), can see their orders and cancel one while it is New. Owner and staff see them on the **Orders** screen (sidebar badge shows how many are New): view, **Accept**, **Cancel** (with a reason the client sees) or **Make the bill**, which opens Create Bill with the client, city and products filled in at the client's usual rates (`?orderId=`). Saving that bill marks the order Billed (`InvoiceRequest.orderId`); an order can be billed once.
- **Emails** use the `OrderPlaced` trigger: two standard templates are added automatically, "Order received (to client)" (to the person who ordered) and "New order (to me)". Placeholders `{{OrderNumber}} {{OrderDate}} {{OrderItems}} {{OrderNote}}`; the item list has names and quantities only. "To me" falls back to the owner's login email when My business has no email. `App:PublicUrl` (appsettings) is the website address used in the invitation email.
- **Live alerts for owner and staff:** while the app is open, `OrderAlerts` asks `GET /api/orders/watch` every 15 seconds (count of New orders plus any New order from the last 10 minutes). A new one shows a popup card (client, products, "View orders") and rings; the ring is synthesized in the browser (Web Audio, no sound file) and needs one click on the page first (the popup says when the browser is blocking it). Sidebar controls: sound on/off, Test, optional desktop notification when the tab is in the background. The browser remembers which orders it already rang for. Clients never run this.
- **Emails when owner or staff accept or cancel** (`OrderAccepted`, `OrderCancelled` triggers; two standard templates added automatically; placeholder `{{OrderCancelReason}}`) go to the person who ordered. A client cancelling their own order does not email anyone yet.
- Activity log records order events; backup export includes `orders`; Team lists only owner and staff.

**Still not built (ideas for later):** scheduled email of backups, e-invoicing, e-way bill API, credit notes, stock, WhatsApp.

## 0. How to work on this project (instructions for Claude)

- Read this whole file before writing any code. It is the single source of truth for this project.
- Build in the phases listed in Section 12. Finish one phase, make sure backend and frontend both build and run, then give me a short summary: what you built, how to run it, and how to test it. Then STOP and wait for my go-ahead before starting the next phase.
- Ask me before changing the tech stack, the data model, or any business rule in Section 4.
- I am an experienced .NET/C# developer but newer to Angular. Keep Angular code clean and idiomatic, and add short comments explaining non-obvious Angular concepts (signals, reactive forms, interceptors, guards, etc.).
- Write unit tests for every calculation in Section 4 (rate resolution, GST split, totals, rounding, amount in words, invoice numbering, GSTIN validation).
- Never hard-code or commit secrets. Use `appsettings.Development.json` (git-ignored) or .NET user-secrets for the Mongo connection string and JWT key, and provide an `appsettings.example.json`.
- Use `decimal` for all money in C# and store money as `Decimal128` in MongoDB. Never use `double` for money.
- Keep a `README.md` updated with setup and run instructions.

## 1. Overview

Shahin Enterprises sells mattresses and pillows, B2B only. Today all bills are written by hand in a paper bill book. This app replaces that with a web-based GST billing system.

Core idea: the owner sets up their business details, items, and clients once. Creating a bill is then mostly picking from searchable dropdowns. Rates, GST, and totals fill in automatically, and the result is a professional GST tax-invoice PDF.

Every field and setting must be dynamic and editable from the UI. Nothing business-specific may be hard-coded.

## 2. Tech stack

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core Web API, .NET 8 LTS or newer (use the latest LTS SDK installed) |
| Database | MongoDB, official `MongoDB.Driver` |
| Auth | JWT bearer tokens, passwords hashed with `BCrypt.Net-Next` |
| Validation | FluentValidation |
| PDF | QuestPDF (Community license) |
| Excel | ClosedXML |
| Tests | xUnit + FluentAssertions |
| API docs | Swagger / OpenAPI |
| Frontend | Angular (latest), standalone components, signals, reactive forms, lazy-loaded routes |
| UI library | PrimeNG (use `p-select`/`p-dropdown` with `filter`, `p-multiSelect` with `filter`, `p-table`, `p-dialog`, `p-toast`, `p-confirmDialog`) |

## 3. Repository structure

```
shahin-billing/
  CLAUDE.md
  README.md
  backend/
    ShahinBilling.sln
    src/ShahinBilling.Api/
      Controllers/
      Models/          (MongoDB documents)
      Dtos/
      Services/        (business logic: rates, GST, invoices, numbering)
      Repositories/
      Pdf/             (QuestPDF invoice document + template rendering)
      Reports/         (Excel exports)
      Validators/
      Helpers/         (AmountInWords, GstinValidator, StateCodes, FinancialYear)
      Auth/
    tests/ShahinBilling.Tests/
  frontend/
    shahin-billing-web/   (Angular app)
      src/app/
        core/          (auth service, JWT interceptor, auth guard, API services, models)
        shared/        (reusable components, pipes such as currency-INR)
        features/
          auth/        (login, register)
          dashboard/
          settings/    (My Business)
          items/
          clients/
          billing/     (create/edit bill)
          bills/       (all bills list, view, payments)
          bill-format/ (template editor)
          reports/
```

## 4. Business rules (most important – implement exactly, with unit tests)

### 4.1 Client-specific rate resolution
- Every item has a `defaultRate`.
- Every item can have zero or more `specialRates` entries. Each entry is `{ rate, clientIds[] }`. The owner sets a rate and multi-selects the clients it applies to.
- When an item is selected on a bill for client X:
  1. If any `specialRates` entry of that item contains X, use that entry's rate.
  2. Otherwise use `defaultRate`.
- Validation: a client may appear in at most ONE special-rate entry per item. Reject saves that break this rule, with a clear error message.
- The resolved rate is pre-filled on the bill but stays editable for that one bill line. Editing it on a bill must NOT change the item master.
- If the client on a bill is changed after items were added, re-resolve all line rates and show a toast saying rates were updated.
- All rates are entered EXCLUSIVE of GST.

### 4.2 Tax type: CGST+SGST vs IGST
- The business has a state (from its GSTIN). Each bill has a **Place of Supply** state, which defaults to the Bill To client's state and stays editable on the bill.
- If business state == place of supply: charge CGST and SGST, each at half the item's GST rate.
- If they differ: charge IGST at the full GST rate.
- Show the place of supply on the bill and the PDF.

### 4.3 Calculations (per line, then totals)
```
lineAmount     = quantity × rate
discount       = optional per line (amount, default 0)
taxableValue   = lineAmount − discount
intra-state:  cgst = taxableValue × (gstRate/2)/100 ; sgst = same
inter-state:  igst = taxableValue × gstRate/100
lineTotal      = taxableValue + cgst + sgst + igst
```
- Round each tax amount to 2 decimals (MidpointRounding.AwayFromZero).
- Totals: sum of taxable values, sum of CGST, SGST, IGST.
- No round off: `grandTotal` = the exact total (see "Changes since the original spec"). `roundOff` stays 0 on new bills.
- Also produce a GST summary grouped by GST rate (taxable value and tax per rate) for the PDF.
- Calculations run live in the Angular UI for instant feedback, AND are recalculated on the server on save. The server value is authoritative, and the client never sends final totals that are trusted.

### 4.4 Invoice numbering
- Format is configurable in settings: prefix + separator + running number, e.g. `SE-0001`. Configurable parts: prefix text, separator, number padding. There is no financial year in the number.
- The counter never restarts. Non-GST bills have their own series and prefix (see "Changes since the original spec").
- Generate numbers atomically using a `counters` collection with `FindOneAndUpdate` + `$inc` (no duplicates, even with concurrent requests).
- The number is assigned when the bill is first saved and never changes after that, even when the bill is edited.

### 4.5 Snapshots
- When a bill is saved, copy (snapshot) the business details, client details (name, GSTIN, addresses, state), and item details (name, size, HSN, unit, GST rate) into the invoice document.
- Later edits to the business, client, or item masters must NOT change old bills.

### 4.6 Cancel, never hard-delete
- Bills cannot be deleted, only cancelled (status `Cancelled`, with reason and date), to preserve invoice-number continuity for GST.
- Cancelled bills appear in the list with a clear badge and are excluded from sales totals and reports. The PDF of a cancelled bill shows a "CANCELLED" watermark.

### 4.7 Amount in words (Indian system)
- Convert the grand total to words using lakh/crore, e.g. `Rupees One Lakh Twenty-Three Thousand Four Hundred Fifty Only`. Handle paise if non-zero.

### 4.8 GSTIN validation and state auto-fill
- GSTIN format regex: `^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$`. Also validate the checksum character.
- Auto-uppercase input.
- The first 2 digits are the state code. Include the full Indian GST state-code list (01–38, plus 97 Other Territory) as a helper. When a valid GSTIN is typed, auto-select the state (still editable).

## 5. Data model (MongoDB)

Every document except `users` carries `businessId`. Every query must filter by the `businessId` from the logged-in user's JWT. Never trust a businessId sent from the client. All documents have `createdAt` and `updatedAt`.

**users**: `id, name, email (unique), passwordHash, businessId, role ("Owner"), createdAt`

**businesses** (My Business settings):
- `name` (e.g. Shahin Enterprises), `logo` (base64, max 500 KB), `addressLine1, addressLine2, city, state, stateCode, pincode`
- `phone, alternatePhone, email, website`
- `gstin, pan`
- `bank: { accountName, accountNumber, ifsc, bankName, branch, upiId }`
- `signature` (base64 image, max 300 KB), `authorisedSignatoryName`
- `termsAndConditions` (multiline text), `declarationText`
- `invoiceNumbering: { prefix, separator, padding }`
- `defaultTemplateSettings` (see Section 9)

**items**:
- `name, sizeOrVariant` (e.g. 72x36x6 inch), `description, hsnCode, unit` (default "PCS")
- `gstRate` (percent, choose from 0 / 5 / 12 / 18 / 28, editable list), `defaultRate`
- `specialRates: [{ id, rate, clientIds[] }]`
- `isActive` (inactive items are hidden from billing dropdowns but kept for old bills)

**clients**:
- `name, gstin, pan, contactPerson, phone, email`
- `billingAddress: { line1, line2, city, state, stateCode, pincode }`
- `shippingSameAsBilling` (bool, default true), `shippingAddress` (same shape)
- `notes, isActive`

**invoices**:
- `invoiceNumber, invoiceDate, dueDate, financialYear`
- `status`: `Draft | Final | Cancelled`, `cancelReason, cancelledAt`
- `businessSnapshot`
- `clientId, billTo` (snapshot: name, gstin, address, state, stateCode), `shipTo` (snapshot), `shipToSameAsBillTo`
- `placeOfSupplyState, placeOfSupplyStateCode, isInterState`
- `poNumber, poDate, transport: { transporterName, vehicleNumber, ewayBillNumber, lrNumber, deliveryDate }`
- `lines: [{ itemId, name, sizeOrVariant, hsnCode, unit, quantity, rate, discount, taxableValue, gstRate, cgst, sgst, igst, lineTotal }]`
- `totals: { taxableTotal, cgstTotal, sgstTotal, igstTotal, roundOff, grandTotal }`
- `gstSummary: [{ gstRate, taxableValue, cgst, sgst, igst }]`
- `amountInWords, notes`
- `payments: [{ id, date, amount, mode (Cash/Bank Transfer/UPI/Cheque), reference, note }]`
- `paymentStatus`: `Unpaid | PartlyPaid | Paid` (derived from payments vs grandTotal), `amountPaid, balanceDue`
- `templateSettingsOverride` (optional)

**counters**: `businessId, financialYear, seq`

Create indexes: users.email unique; invoices (businessId, invoiceNumber) unique; invoices (businessId, invoiceDate); invoices (businessId, clientId); text indexes for searching clients and items by name.

## 6. API (all under `/api`, JWT required except auth)

- **Auth**: `POST /auth/register` (creates user + empty business), `POST /auth/login`, `GET /auth/me`, `POST /auth/change-password`
- **Business**: `GET /business`, `PUT /business`
- **Items**: `GET /items?search=&active=`, `GET /items/{id}`, `POST /items`, `PUT /items/{id}`, `PATCH /items/{id}/active`
- **Item rate for a client**: `GET /items/{id}/rate?clientId=` → `{ rate, isSpecial }`
- **Clients**: `GET /clients?search=&active=`, `GET /clients/{id}`, `POST /clients`, `PUT /clients/{id}`, `PATCH /clients/{id}/active`, `GET /clients/{id}/special-rates` (read-only list of the client's special prices)
- **Invoices**:
  - `GET /invoices?search=&clientId=&from=&to=&status=&paymentStatus=&page=&pageSize=`
  - `GET /invoices/{id}`, `POST /invoices`, `PUT /invoices/{id}`
  - `POST /invoices/{id}/finalize`, `POST /invoices/{id}/cancel`, `POST /invoices/{id}/duplicate` (new draft with the same client and lines, rates re-resolved)
  - `POST /invoices/calculate` (returns calculated lines and totals without saving, used for previews)
  - `POST /invoices/{id}/payments`, `DELETE /invoices/{id}/payments/{paymentId}`
  - `GET /invoices/{id}/pdf?copies=original,duplicate,triplicate`
- **Template**: `GET /template`, `PUT /template`, `POST /template/preview-pdf` (renders a sample invoice with the given settings)
- **Dashboard**: `GET /dashboard` → this month's sales, last month's sales, total unpaid amount, count of unpaid bills, top 5 clients by sales this financial year, last 5 bills
- **Reports**: `GET /reports/gstr1?month=YYYY-MM` (Excel), `GET /reports/sales?from=&to=` (Excel)
- **Backup**: `GET /backup/export` (JSON of all of this business's data)

Use consistent error responses (ProblemDetails) with field-level validation messages that the Angular forms can display.

## 7. Frontend screens

Global: a left sidebar menu (Dashboard, Create Bill, All Bills, Items, Clients, Reports, Bill Format, My Business), a top bar with the business name and logout, and a responsive layout that is usable on a mobile phone. Show the INR format `₹1,23,456.00` (Indian digit grouping) everywhere. Show toasts for success and errors, and a confirm dialog before cancelling a bill or leaving an unsaved form.

1. **Login / Register**: email + password. Register also asks for the business name. JWT stored in localStorage; an HTTP interceptor adds the token; a route guard protects all pages; auto-logout on 401.

2. **My Business**: a form for all fields in the businesses model, with logo and signature upload (with preview), GSTIN auto-fill of state, bank details, terms, and invoice-number format with a live example such as `SE-0001`.

3. **Items**: a table with search, add/edit in a dialog. Inside the edit dialog, a **Special Rates** section: rows of `[rate] [multi-select clients with search] [remove]` plus an "Add special rate" button. Clients already used in another row are disabled in the other rows' multi-selects.

4. **Clients**: a table with search, add/edit form. Billing address, with a "Shipping same as billing" checkbox (ticked by default) that hides the shipping fields. GSTIN validation with state auto-fill. The client detail view shows a read-only list of that client's special rates.

5. **Create / Edit Bill** (the main screen; must be fast to use with the keyboard):
   - Invoice number (shown after save), invoice date (default today), due date (optional).
   - Client: a searchable dropdown. On select, auto-fill Bill To, Ship To (if same as billing), GSTIN, and Place of Supply.
   - Bill To and Ship To shown as cards. A "Ship To same as Bill To" toggle; when off, Ship To becomes editable.
   - Collapsible "More details" section: PO number/date, transporter, vehicle number, e-way bill number, LR number, notes.
   - Lines table: item (searchable dropdown showing name + size), HSN (auto), quantity, unit (auto), rate (auto-resolved, editable, with a small "special rate" tag when applicable), discount, taxable value, GST % (auto), tax amount, line total. "Add item" button; delete-row icon; pressing Enter in the last quantity field adds a new row.
   - Totals panel: total, CGST + SGST (or IGST) on GST bills, grand total, amount in words. All update live.
   - Buttons: Save Draft, Save & Finalize, Save & Download PDF.
   - Show a warning (not a block) if the e-way bill number is empty and the grand total exceeds ₹50,000.

6. **All Bills**: a paginated table with search (invoice number or client), filters (date range, client, status, payment status), and colour-coded payment status badges. Row actions: View, Edit, Duplicate, Download PDF, Record Payment, Cancel. The view page shows the invoice preview and its payment history.

7. **Dashboard**: summary cards plus a simple monthly sales bar chart for the current financial year (PrimeNG chart or Chart.js).

8. **Reports**: pick a month and download the GSTR-1 Excel; pick a date range and download the sales Excel. A backup export button.

9. **Bill Format**: see Section 9.

## 8. PDF invoice layout (QuestPDF, A4 portrait)

- Header: logo, business name, address, phone, email, GSTIN; the title "TAX INVOICE"; a copy label at the top right ("Original for Recipient" / "Duplicate for Transporter" / "Triplicate for Supplier"). When multiple copies are requested, put each copy in the same PDF as separate pages.
- Invoice meta: invoice number, date, due date, place of supply, PO number, transport details (only if filled).
- Two boxes: Bill To and Ship To (name, address, GSTIN, state + code).
- Items table: S.No, Item (name + size), HSN, Qty, Unit, Rate, Discount (hidden if all zero), Taxable Value, then CGST % / CGST amount and SGST % / SGST amount (or IGST % / IGST amount), and Total. The table header repeats on new pages.
- Below the table: GST summary by rate (GST bills), totals block, grand total in bold, amount in words.
- Footer: bank details and UPI ID, terms and conditions, declaration, "For Shahin Enterprises" with the signature image and "Authorised Signatory", and page numbers in the form "Page x of y".

## 9. Bill Format (template) editor

Settings stored on the business (and optionally overridden per invoice):
- `layout`: "Classic" (bordered table) or "Modern" (clean, minimal borders). Implement both.
- `primaryColor` (colour picker), `fontSize` (Small/Normal/Large)
- Show/hide toggles: logo, discount column, HSN column, GST summary table, bank details, UPI ID, terms, declaration, signature, transport details, PO details
- Editable texts: invoice title, footer note, terms, declaration
- Default copies to print (checkboxes: Original/Duplicate/Triplicate)

The editor page shows the settings on the left and a live PDF preview on the right (an iframe that calls `/template/preview-pdf`, debounced at 500 ms).

## 10. Reports

- **GSTR-1 Excel** (final bills only, cancelled excluded), sheets:
  - `B2B`: Recipient GSTIN, Receiver Name, Invoice Number, Invoice Date (dd-MMM-yyyy), Invoice Value, Place of Supply (code-name), Reverse Charge (N), Invoice Type (Regular B2B), Rate, Taxable Value, Cess (0). One row per invoice per GST rate.
  - `HSN Summary`: HSN, Description, UQC, Total Quantity, Total Value, Taxable Value, IGST, CGST, SGST.
- **Sales Excel**: one row per invoice with totals and payment status.

## 11. Non-functional requirements

- Security: BCrypt password hashing; JWT expiry of 8 hours; business-level data isolation on every query; CORS allowing only the Angular dev origin (configurable); request size limits for image uploads.
- Server-side validation on all inputs (FluentValidation). Mirror the key validations in Angular forms for instant feedback.
- Quantity must be > 0. Rates must be ≥ 0. A bill needs at least one line to be finalized. A client is mandatory.
- Dates are displayed as dd-MM-yyyy and stored as UTC.
- Performance: dropdown searches respond quickly with up to a few thousand items and clients. Use server-side search with debounce if lists grow large.
- Seed data (Development only): a script or endpoint that creates a demo business, 5 items (mattresses in different sizes, pillows), 4 clients (one in another state, to test IGST), and a special rate example.

## 12. Build phases (STOP after each and wait for approval)

**Phase 0 – Setup and Auth**
Solution and Angular app scaffolding, MongoDB connection, Swagger, PrimeNG setup, layout with sidebar, register/login/logout, guard and interceptor, README.
*Done when*: I can register, log in, see an empty dashboard, and log out.

**Phase 1 – Masters**
My Business settings (including logo, signature, bank, numbering), Items with special rates, Clients with GSTIN auto-fill and shipping toggle. Unit tests for GSTIN validation and the special-rate uniqueness rule. Seed data.
*Done when*: I can add items, set a special pillow rate for 2 selected clients, and add clients.

**Phase 2 – Billing and PDF**
Create/Edit Bill screen with all auto-fill behaviour, live calculations, server recalculation, invoice numbering, snapshots, draft/finalize, PDF generation with copies. Unit tests for Section 4 calculations, numbering, and amount in words.
*Done when*: picking a special-rate client fills that client's rate, an out-of-state client gets IGST, and the PDF downloads correctly.

**Phase 3 – Bills management**
All Bills list with search, filters, and pagination; view; edit; duplicate; cancel; payments and payment status.

**Phase 4 – Dashboard, Reports, Bill Format, Backup**
Dashboard cards and chart, GSTR-1 and sales Excel, template editor with live preview and both layouts, JSON backup export.

## 13. Out of scope for now (design so these can be added later)

- E-invoicing (IRN/QR via the GST portal), which is mandatory above ₹5 crore turnover
- Sending bills by WhatsApp (email is built, see above)
- E-way bill generation via API
- Credit notes / debit notes
- Stock / inventory tracking
