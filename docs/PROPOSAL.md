# Store Manager: Step 1 Proposal (for approval)

Working name: **[App Name]**, for **[Store Name]**, **[City]**. These placeholders are still unfilled (see question 1).
Nothing is built yet. This document covers the four things Step 1 asks for: assumptions and questions, tech stack, screens, and database schema.

---

## 1. Assumptions and questions (8 max)

Each question has the default I'll use if you don't answer.

| # | Question | My default |
|---|----------|-----------|
| 1 | What are the app name, store name and city? The prompt still has `[placeholders]`. | App = "DukaanDesk", Store = "My Store", City = blank. All three are Settings values, so they're easy to change. |
| 2 | One counter PC only, or will a second PC (owner at home or in the back office) need the same data soon? | One PC now. The data layer sits behind repository interfaces so LAN or cloud sync can come later. A shared SQLite file over a network share is **not** safe, so I won't do that. |
| 3 | What hardware is there: thermal printer model and paper width (58 or 80 mm), USB barcode/QR scanner, webcam? | 80 mm ESC/POS thermal printer, USB scanner (it types like a keyboard), laptop or USB webcam. Printing also works through any Windows printer driver. |
| 4 | The prompt lists an *accountant* user but only three roles (Owner, Manager, Staff). What should the accountant be? | Add a 4th role, **Accountant**: read and edit payments and reports, no delete, no fall-pico or task admin. |
| 5 | Who provides the Hindi text? | I draft all Hindi strings in `.resw` files. Someone from the store should review them before release. |
| 6 | WhatsApp: personal or Business app on Desktop? WhatsApp offers no official way to send automatically. | The app opens a pre-filled chat (`whatsapp://send?phone=…&text=…`). The staff member presses Send. It never auto-sends. |
| 7 | Karigar payment: per piece, weekly settlement, or both? | Rate per piece (from the default rate list, overridable), settled weekly. Payables are created per settlement. |
| 8 | What Windows version and CPU are the PCs (Win10 22H2 or Win11, x64)? | x64, Win10 1809 or newer, self-contained .NET so nothing needs installing first. |

Design decisions I'm making that you can veto:
- **Money is stored as integer paise** (never float). Display uses Indian grouping (₹1,25,000).
- **Dates and times are stored in UTC and shown in IST.** Reminders compare in UTC, so a changed PC clock or time zone can't lose them.
- **`IsDeleted` is skipped on append-only tables** (`ActivityLog`, `PieceStatusHistory`, `FollowUpLog`, `PaymentTransaction`, `Setting`). History must never disappear. A payment is reversed with a correcting entry, not a delete.
- **The 4-digit PIN stops casual misuse, not a determined attacker.** PINs are stored as salted hashes (PBKDF2), with a lockout after 5 wrong tries. Backups can be password-encrypted as an option.
- **Contact "outstanding balance" is derived**, not stored: the sum of open receivables minus open payables for that contact.

---

## 2. Final tech stack

| Concern | Choice | Why |
|---|---|---|
| Language / runtime | C# on **.NET 10 (current LTS)**, self-contained | No runtime install on the shop PC. |
| UI | **WinUI 3** (Windows App SDK, latest stable), **unpackaged** | Native Fluent look, Mica, touch. Unpackaged avoids MSIX signing-certificate pain for a single shop. |
| Pattern | MVVM with **CommunityToolkit.Mvvm**, **Microsoft.Extensions.DependencyInjection** | As requested. |
| Data | **SQLite** with **EF Core** and migrations, WAL mode, FTS5 table for global search | Offline-first and fast at 100k+ rows. FTS5 gives instant Ctrl+K. |
| Tray icon | **H.NotifyIcon.WinUI** | WinUI 3 has no native tray support. |
| Toasts | Windows App SDK `AppNotificationManager` | Works for unpackaged apps. |
| Excel I/O | **ClosedXML** (export and import), **CsvHelper**, a small vCard parser | MIT/permissive. |
| PDF / A5 printing | **QuestPDF** (free for businesses under US$1M revenue), then the Windows print pipeline | Statements, ledgers, A5 receipts. |
| Thermal printing | ESC/POS bytes sent as raw data to the Windows print queue (58/80 mm) | Fast and driver-independent. A Windows-driver fallback is available. |
| QR / barcode | **QRCoder** to generate, USB scanner as keyboard input to read | No extra drivers. |
| Webcam | `Windows.Media.Capture.MediaCapture`, plus a file picker | |
| Lists | `ListView` / `ItemsRepeater` with virtualisation and paged async queries | The old WinUI DataGrid is unmaintained, so I'll build a lightweight virtualised table control. |
| Tests | **xUnit** on a platform-neutral **Core** library | Balances, reminder scheduling, status transitions. |
| Installer | **Inno Setup** (`.exe`), optional start-with-Windows, bundled .NET | Doesn't need a code-signing certificate. MSIX stays possible later. |
| CI | GitHub Actions on `windows-latest` | See the build-verification note below. |

**Solution layout** (the split is what makes the logic testable without Windows):
```
StoreManager.Core        net10.0   entities, enums, rules (balances, status machine, reminder rules), interfaces
StoreManager.Data        net10.0   EF Core + SQLite, migrations, repositories, backup, FTS, seed data
StoreManager.Services    net10.0   ReminderScheduler, WhatsAppService, ExportService, AuthService, NotificationService (interface)
StoreManager.App         net10.0-windows10.0.19041   WinUI 3: Views, ViewModels, toast/tray, PrintService, webcam
StoreManager.Tests       net10.0   xUnit for Core, Data (in-memory SQLite) and Services
installer/               Inno Setup script
```

**Build-verification note (please read).** I'm working in a Linux sandbox. WinUI 3 projects only build on Windows, and the sandbox doesn't currently have the .NET SDK. For each step I'll:
1. Run the unit tests for Core/Data/Services where the SDK is available.
2. Add a `windows-latest` GitHub Actions workflow that builds the WinUI app and runs the tests, then read the logs and fix failures.
3. **Not claim the UI has been visually verified.** I can't run it. You'll do the "how to run / what to test" checks on a real Windows PC after each step.

---

## 3. Screens and wireframes

Shell: left `NavigationView` (Dashboard, Tasks, Follow-ups, Contacts, Payments, Fall-Pico, Reports, Settings). Top bar holds the Ctrl+K search box, the notification bell with badge, the user chip (switch user) and the sync/backup status dot. A right-hand **side panel** slides in for every new/edit form, so the user never leaves the list. Mica backdrop, light and dark themes.

### Shell and global
1. **PIN login / user switch**: big avatar tiles for each user, then a 4-digit keypad (touch-friendly).
2. **Morning summary popup**: after login, lists today's due tasks, follow-ups, payments and fall-pico alerts. Missed reminders appear at the top in an amber "Missed while the PC was off" block.
3. **Global search (Ctrl+K)**: centred palette with grouped results (Contacts, Fall-Pico jobs, Bills, Tasks). Typing 10 digits jumps to the contact by mobile.
4. **Notification centre**: flyout from the bell, with Mark done, Snooze and Open actions.
5. **Quick-add bar** (Dashboard and a global `+` button): + Task, + Follow-up, + Contact, + Payment, + Fall-Pico Job.

### Dashboard
6. **Today dashboard**: a grid of clickable cards.
   ```
   [Tasks due 5 | 2 overdue] [Follow-ups today 8] [Receivable overdue ₹1,25,000 | due this week ₹40,000]
   [Payables this week ₹80,000] [Fall-pico: due from karigar 6 | ready for pickup 9 | overdue 3]
   [Birthdays & anniversaries: next 7 days]            [Quick-add bar]
   ```
   Each card opens the matching module with the filter pre-applied.

### Contacts
7. **Contact list**: search box, type chips, tag chips (AND/OR toggle), sort, record count, bulk-select bar (Tag / Untag / Export).
8. **Contact side panel (add/edit)**: required fields marked, mobile with +91 and a duplicate warning, tag multi-select with colours, and a photo.
9. **Contact profile**: header (photo, name, tags, balance badge, buttons Call/WhatsApp/Copy address) and a tabbed timeline (All, Tasks, Follow-ups, Payments, Fall-Pico, Notes).
10. **Tag manager**: rename, recolour, merge two tags, delete (with usage count).
11. **Import wizard**: Excel/CSV/vCard, then column mapping, then a duplicate preview, then import.

### Tasks
12. **Task list**: groups Overdue / Today / Tomorrow / This Week / Later, with a "My Tasks" toggle.
13. **Task Kanban**: To Do / In Progress / Done columns, drag-and-drop.
14. **Task calendar**: month and week views, with tasks as coloured pills.
15. **Task side panel**: title, notes, due date/time, priority, assignee, linked contact, checklist, photos, recurrence.

### Follow-ups
16. **Follow-up list**: tabs Due Today / Overdue / Upcoming / By Staff, with a WhatsApp template picker.
17. **Follow-up detail**: contact summary, attempt log timeline, "Log attempt" button. After logging, a "Schedule next?" quick-pick row (+1 day, +3 days, +1 week, custom) appears. A Close as Won/Lost dialog asks for a reason.

### Payments
18. **Payments home**: header totals (total receivable, overdue, collected this month, payable). Tabs: Receivable, Payable, Reminders to send today, Cheques, Ageing.
19. **Bill side panel**: party, bill no., bill date, amount, due date, notes, bill photo.
20. **Bill detail with payment entry**: transactions list, balance, "Add payment" (mode-specific fields, cheques include number/bank/date), "Send WhatsApp reminder" (logged).
21. **Party ledger**: running balance, print and PDF statement.
22. **Ageing report**: 0–30 / 31–60 / 61–90 / 90+ buckets, drill-down by party.

### Fall-Pico
23. **Fall-Pico board**: Kanban with status columns (Received, With Karigar, Ready, Delivered, plus On Hold/Cancelled). Cards show job/piece number, customer, due date and a large status chip. A scan field at the top accepts a QR/barcode scan and moves the piece to the next status.
24. **New job wizard (side panel)**: customer found by mobile, promised date, add pieces (photo, work types, fall colour, source, charge), advance paid, then **Save & Print token** (one button).
25. **Job detail**: pieces with per-piece status history, karigar assignment, money summary, print token / tags, WhatsApp "ready for pickup".
26. **Karigar view**: per karigar, pieces held, overdue count, amount owed, weekly settlement statement (creates a payable).
27. **Fall-pico alerts list**: overdue at karigar, ready but uncollected for X days, promised date tomorrow.

### Essentials
28. **Reports**: jobs per month, revenue vs karigar cost, average turnaround, karigar performance, receivables ageing. Export to Excel/PDF.
29. **Settings**: Store profile, Message templates, Tags, Follow-up purposes, Work types and rates, Staff and roles, Reminder rules, Backup, Theme and language, Demo data toggle.
30. **Activity log and Recycle Bin**: filter by user, date and entity; restore within 30 days.
31. **Backup and restore**: schedule, folder, last-backup status, restore wizard (verifies the file, takes a safety copy of current data first).

---

## 4. Database schema

SQLite via EF Core. **Conventions**: `Id` is a GUID (so a future multi-PC sync is possible) and is stored as TEXT. `CreatedAt`, `UpdatedAt` are UTC. `CreatedBy` and `UpdatedBy` are User FKs. `IsDeleted` and `DeletedAt` exist except on append-only tables (marked ⛔). Money is `INTEGER` paise. A **RowVersion** column is included now to ease future sync.

### Core
- **User**: Name, Role (Owner/Manager/Accountant/Staff), PinHash, PinSalt, FailedAttempts, LockedUntil, Language, IsActive, AvatarColor
- **Contact**: Type (Customer/Supplier/Karigar/Staff/Other), Name, Mobile *(unique where not deleted)*, WhatsApp, AltPhone, City, Address, Gstin, Birthday, Anniversary, ReferredByContactId, Notes, PhotoAttachmentId
- **Tag**: Name *(unique)*, ColorHex
- **ContactTag**: ContactId, TagId *(composite PK)*
- **Attachment**: OwnerType, OwnerId, FilePath, Kind (Photo/Bill/Other), Caption
- **Setting** ⛔: Key, Value
- **MessageTemplate**: Kind (FollowUp/PaymentReminder/FallPicoReady/Birthday…), Language, Body
- **LookupItem**: List (FollowUpPurpose/WorkType/FollowUpOutcome…), Name, SortOrder, DefaultRatePaise, IsActive
- **SavedView**: Module, Name, FilterJson, UserId

### Tasks
- **Task**: Title, Notes, DueAt, Priority, Status, AssignedUserId, ContactId?, RecurrenceRule (None/Daily/Weekly/Monthly + interval), RecurrenceParentId, CompletedAt
- **TaskChecklistItem**: TaskId, Text, IsDone, SortOrder

### Follow-ups
- **FollowUp**: ContactId, PurposeId, Channel, NextDueAt, Priority, AssignedUserId, Status (Open/Won/Lost), CloseReason, ClosedAt
- **FollowUpLog** ⛔: FollowUpId, AttemptAt, Outcome, Notes, UserId

### Payments
- **PaymentEntry**: Direction (Receivable/Payable), ContactId, BillNo, BillDate, AmountPaise, DueDate, Notes, Status *(derived and cached)*, WrittenOffAt, FallPicoJobId?
- **PaymentTransaction** ⛔: EntryId, PaidAt, AmountPaise, Mode, ChequeNo, Bank, ChequeDate, ChequeClearedAt?, ReversalOfId?, UserId
- **ReminderRule**: Scope (Receivable/Payable), DaysBeforeDue, OnDueDate, RepeatEveryDaysWhenOverdue, IsActive
- **PaymentReminderLog** ⛔: EntryId, SentAt, Channel, UserId

### Fall-Pico
- **FallPicoJob**: JobNo (FP-0001, sequential via a counter table), ContactId, ReceivedAt, PromisedAt, AdvancePaise, Notes
- **FallPicoPiece**: JobId, PieceNo, Description, Source (BoughtHere/CustomerOwn), WorkTypesJson, FallColourNotes, ChargePaise, KarigarContactId?, SentAt, ExpectedBackAt, KarigarRatePaise, Status, ReadySince, DeliveredAt, QrCode
- **PieceStatusHistory** ⛔: PieceId, FromStatus, ToStatus, ChangedAt, UserId, Note
- **KarigarSettlement**: KarigarContactId, PeriodStart, PeriodEnd, TotalPaise, PaymentEntryId?

### Reminders, notifications, audit
- **ReminderInstance**: SourceType, SourceId, DueAt, FiredAt?, AcknowledgedAt?, SnoozedUntil?, UserId? *(the missed-reminder catch-up reads rows where `DueAt <= now AND AcknowledgedAt IS NULL`)*
- **Notification**: Kind, Title, Body, LinkType, LinkId, CreatedAt, ReadAt?, UserId?
- **ActivityLog** ⛔: UserId, At, Action (Create/Update/Delete/Restore/Login…), EntityType, EntityId, ChangesJson
- **BackupLog** ⛔: At, Path, SizeBytes, Result, Kind (Auto/Manual/Restore)
- **Counter**: Name, NextValue *(for job numbers)*
- **SearchIndex** (FTS5 virtual table): EntityType, EntityId, Text *(contact names and mobiles, job numbers, bill numbers, task titles)*

### Key indexes
`Contact(Mobile)` unique, `Contact(Name)`, `ContactTag(TagId, ContactId)`, `Task(Status, DueAt)`, `FollowUp(Status, NextDueAt)`, `PaymentEntry(Direction, Status, DueDate)`, `PaymentTransaction(EntryId)`, `FallPicoPiece(Status, ExpectedBackAt)`, `FallPicoPiece(KarigarContactId, Status)`, `ReminderInstance(DueAt) WHERE AcknowledgedAt IS NULL`, `ActivityLog(EntityType, EntityId)`, and `IsDeleted` as a partial-index filter where useful.

### Rules that get unit tests
- **Payment balance** = Amount − sum(transactions) (reversals counted). Status: Paid if 0, Partly Paid if 0 < balance < amount, Overdue if balance > 0 and DueDate < today, Written Off overrides.
- **Piece status transitions** (allowed edges only): Received → WithKarigar → Ready → Delivered. OnHold and Cancelled are reachable from any non-final state. Delivered and Cancelled are final.
- **Reminder scheduling**: before-due, on-due, and repeat-while-overdue dates. Missed reminders catch-up after downtime. Snooze: 15 min, 1 hr, tomorrow.

---

## 5. Build order (unchanged from your prompt)

a. Setup, shell, theme, DB, Contacts + Tags → b. Tasks → c. Follow-ups → d. Payments → e. Fall-Pico → f. Dashboard, notifications, search → g. Users/roles, backup, settings, export/print, reports → h. Installer.

**Awaiting your approval (and answers to the 8 questions above) before writing any code.**
