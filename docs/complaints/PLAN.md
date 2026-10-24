# Mahavira Fashion — Complaint Management System: Plan for approval

Status: **proposal only — nothing is built or deployed.** Schema draft: [`schema.sql`](schema.sql).

## 1. Stack recommendation

| Option | Pros | Cons |
|---|---|---|
| **Next.js + Supabase (Postgres, Auth, Storage) — recommended** | One codebase for public form, staff app and API; real SQL for reports; row-level security; private file storage; free tier covers this volume; Hindi/English via `next-intl`; PWA is easy | Free Supabase has **no automated backups** and pauses after 7 days of inactivity (we add our own nightly backup + a daily job that keeps it awake) |
| Firebase | Generous free tier, offline sync | NoSQL makes "complaints by supplier by month" reports painful; harder to export/own your data |
| WordPress/PHP on Hostinger | Familiar hosting | Slow to build securely, weak on mobile UX/PWA, messy role/audit logic |
| Airtable / AppSheet / Sheets | Fastest start | No real public tracking page, weak audit log, per-seat cost, outgrown quickly |

Supporting pieces: Tailwind (brand colours `#8B0000`, `#5E021F`, black/white), `next-intl` (EN/HI), Noto Sans Devanagari (subset) with system-font fallback, client-side image compression before upload (keeps 1 GB storage lasting years), Supabase `pg_cron` or GitHub Actions cron for SLA checks, daily summary and auto-close, Zod validation, Vitest + Playwright tests.

### Running cost (approx., verify current pricing before committing)
| Item | Cost |
|---|---|
| Supabase Free | ₹0 (Pro with automatic backups is about $25/mo) |
| Hosting | Vercel **Hobby is licensed for non-commercial use only** → for a business use Vercel Pro (about $20/mo) **or** Cloudflare/Netlify free tiers (more setup). See Decision 1 |
| Domain/email | Already owned (`mahavirafashion.in`) |
| Email sending | ₹0 (Hostinger SMTP, or Resend free 3,000/mo) |
| WhatsApp | Meta Cloud API: no monthly fee, pay per template message; BSPs (Interakt/AiSensy/Gupshup) charge a monthly plan on top |
| SMS in India | Needs **DLT registration** with TRAI (entity + template approval) — extra paperwork and cost |
| Nightly backups | ₹0 using GitHub Actions + Cloudflare R2 free tier |

## 2. Database
Full draft in `schema.sql`. Main entities:

| Group | Tables |
|---|---|
| Config | `settings`, `business_hours`, `holidays`, `sla_rules`, `message_templates` |
| People | `profiles` (owner/staff, `can_view_reports`), `customers` (unique +91 mobile) |
| Catalogue | `categories` (parent → sub-category, EN+HI names, default priority), `suppliers`, `products` (SKU, supplier) |
| Orders | `orders` (Shopify cache or manual store bill: amount, COD/prepaid, courier, tracking) |
| Tickets | `complaints`, `complaint_items` (1..n products, SKU, supplier, batch ref), `resolutions` (type, amount, refund approval), `complaint_events` (**append-only** timeline and audit log, internal vs customer-visible), `complaint_attachments`, `feedback` |
| Comms | `notification_outbox` (queue, retries, no double-sends) |
| Safety | `rate_limits`, `ticket_counters` (gap-free `MF-2026-00001`) |

Key design choices
- **One timeline table** serves as audit log, internal notes and customer updates; the database blocks edits/deletes on it.
- **Soft delete only**: no DELETE permission exists; "delete" sets `deleted_at` (owner only).
- **Public users never touch the database directly**; the form and tracking page go through rate-limited server routes that return only whitelisted fields.
- Multi-product complaints and `batch_ref` let reports spot a bad batch or supplier.

### Status flow (enforced on the server)
```
New → Acknowledged → In Progress ⇄ Waiting on Customer / Waiting on Supplier-Courier → Resolved → Closed
Any open status → Rejected (reason mandatory)      Resolved/Closed/Rejected → Reopened → In Progress
Resolved → Closed automatically after 7 days without customer reply
Refund resolution → needs owner approval before it can be marked Resolved
```

## 3. Screens

**Public (no login, EN/हिन्दी toggle)**
1. Complaint form (name, +91 mobile, optional email, order/bill no., channel, product, category, description, up to 5 photos/videos, consent tick)
2. Submission success (Ticket ID + "save this" + WhatsApp share)
3. Track complaint (Ticket ID + mobile) → status, customer-visible updates, reply box, add photos
4. Feedback (1–5 stars + comment, from link)
5. Privacy notice

**Staff (mobile-first)**
6. Login · 7. Home/dashboard (my open, overdue, due today) · 8. Complaint list (search, filters, saved views, overdue highlighted) · 9. Quick-add (≤30 s; warns on existing open complaint for the same mobile) · 10. Complaint detail (timeline, internal note vs customer update, status, assign, priority, resolution, attachments) · 11. Customer list and profile (history, repeat-complainer flag) · 12. Products/Suppliers list

**Owner/Admin**
13. Reports (cards and charts, filters, Excel/CSV/PDF export) · 14. Refund approvals queue · 15. Users and roles · 16. Categories (tree editor) · 17. SLA rules · 18. Message templates (EN/HI, WhatsApp approval status) · 19. Business hours/holidays · 20. Store settings (details, logo, ticket prefix) · 21. Notification log · 22. Backup/export status

## 4. Phases

| Phase | Scope | You get |
|---|---|---|
| **0** | Your decisions below; create Supabase/hosting accounts | Empty project, deployed "hello" page |
| **1 — MVP** | Auth and roles, public form with uploads and rate limit, quick-add, duplicate-mobile warning, ticket IDs, full workflow and timeline, assignment, internal notes vs customer updates, resolution types with refund approval, categories, basic dashboard cards, public tracking page, EN/HI UI, SLA due-dates calculated and overdue highlighting, installable PWA shell, nightly backup job | Usable for real complaints. Includes setup guide, env vars, 20 seed complaints |
| **2 — Communication** | WhatsApp and email on create/status/resolve (SMS if you choose), editable EN/HI templates, outbox with retries, Shopify order lookup (auto-fill, COD/prepaid, tracking, SKU), SLA reminders, daily owner summary, 7-day auto-close | Customers get automatic updates; staff stop retyping order details. Includes WhatsApp template submission guide and 20 seed complaints incl. orders |
| **3 — Insight** | Charts by category/source/product/supplier/staff/time, monthly refund and exchange value, repeat-complainer and supplier/batch views, feedback links and satisfaction score, Excel/CSV/PDF export, offline-friendly PWA polish | Spot recurring problems; owner reports. Includes final hardening checklist |

I suggest moving the basic "install to home screen" PWA manifest into Phase 1 since staff are on phones; offline support and polish stay in Phase 3.

## 5. Decisions needed from you

Needed before Phase 1 (defaults in **bold**; reply "go with defaults" to accept all):

1. **Hosting**: **Vercel Pro (about $20/mo, simplest)** · Cloudflare/Netlify free (₹0, more setup) · Vercel Hobby (free but not permitted for commercial use)
2. **Where the code lives**: **new folder `complaints/` in this repo** · separate new repo (cleaner; this repo is the unrelated Windows StoreManager app)
3. **Staff login**: **email + password** (free) · mobile OTP (needs an SMS/WhatsApp OTP provider and cost)
4. **Backups**: **nightly dump to Cloudflare R2 free tier** · upgrade Supabase to Pro (about $25/mo, automatic)
5. **Staff visibility**: **all staff can see all complaints, edit only their assigned or unassigned ones** · staff see only their own
6. **Refund approval**: **every refund needs owner approval** · only above a ₹ threshold you choose
7. **SLA clock**: **business hours only, paused while Waiting on Customer; proposed targets Urgent 30m/24h, High 2h/48h, Medium 4h/96h, Low 8h/7 days** · or give me your numbers. Also tell me your store hours
8. **Customer uploads**: **images up to 5 MB (compressed on phone), videos up to 20 MB; media purged 12 months after closing** · different limits
9. **Ticket numbering**: **calendar year, `MF-2026-00001`** · financial year (April–March)

Needed before Phase 2 (can wait):

10. **WhatsApp**: **Meta Cloud API direct** (no monthly fee, needs Meta Business verification and template approval, takes days) · Interakt/AiSensy/Gupshup if you already have an account
11. **SMS fallback**: **skip for now, add later after DLT registration** · include now (you hold DLT registration?)
12. **Email sending**: **Hostinger SMTP for support@mahavirafashion.in** · Resend
13. **Shopify**: I can read your store (read-only) through the Shopify connection already in this session to learn real order-number format and COD/Razorpay gateway names. OK to do that in Phase 2?

## 6. Risks and notes
- WhatsApp message templates must be pre-approved by Meta; approval can take hours to days. We submit them early in Phase 2.
- Shopify may require creating the API app through its newer Dev Dashboard instead of the older "custom app" screen; I'll verify the current steps when we reach Phase 2 and write them in the guide.
- Customer data (name, mobile, photos) is personal data under India's DPDP Act: the form has a consent tick and privacy notice, and Decision 8 sets a retention period.
- Tracking by Ticket ID + mobile is not OTP-secure; mitigated by rate limits, limited fields shown, and no internal notes ever exposed.
