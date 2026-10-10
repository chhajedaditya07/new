# Store Manager

Offline-first internal management app for an Indian ethnic-wear store: tasks, follow-ups, contacts with tags, payment reminders and saree fall-pico job tracking. Windows 10/11, WinUI 3, .NET 10, SQLite.

The full design is in [`docs/PROPOSAL.md`](docs/PROPOSAL.md). This README tracks what is built.

## Build status

| Step | Scope | Status |
|------|-------|--------|
| a | Project setup, shell, theme, database, **Contacts + Tags** | **Built** (see below) |
| b | Tasks | Not started |
| c | Follow-ups | Not started |
| d | Payment reminders | Not started |
| e | Fall-pico | Not started |
| f | Dashboard, notifications, global search | Not started |
| g | Users/roles, backup, settings, export/print, reports | Not started |
| h | Installer | Not started |

## Try it on a Windows PC (no build tools needed)

Every push builds the app on GitHub Actions and publishes a self-contained folder.

1. Open the repository's **Actions** tab, open the latest **CI** run on branch `claude/compassionate-cori-5n0fck`.
2. Download the **StoreManager-win-x64** artifact and unzip it.
3. Run `StoreManager.exe`. Nothing else needs installing.

Your data is stored in `%LOCALAPPDATA%\StoreManager\store.db` (Settings shows the exact folder).
To use a throw-away database for testing, set the environment variable `STOREMANAGER_DATA` to an empty folder before starting.

## Build from source

Requirements: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download), Visual Studio 2022 17.14+ or `dotnet` CLI.

```powershell
dotnet restore StoreManager.sln
dotnet build   StoreManager.sln -c Release
dotnet test    tests/StoreManager.Tests -c Release
dotnet run --project src/StoreManager.App -c Release
```

The WinUI project only builds on Windows. Everything else (Core, Data, Services, ViewModels, Tests) builds and tests on any OS:

```bash
dotnet test tests/StoreManager.Tests
```

### Database migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> -p src/StoreManager.Data -s src/StoreManager.Data -o Migrations
```

The app applies pending migrations automatically on start.

## Solution layout

```
src/StoreManager.Core        entities, rules, formatting (₹ grouping, IST dates, +91 phones), repository contracts
src/StoreManager.Data        EF Core + SQLite (WAL), migrations, repositories, demo data
src/StoreManager.Services    CSV/Excel/vCard import, Excel export, WhatsApp links, message templates
src/StoreManager.ViewModels  all screen logic (MVVM), no UI types, so it is unit-tested on any OS
src/StoreManager.App         WinUI 3 views, navigation shell, toasts, file pickers, string resources (English + Hindi)
tests/StoreManager.Tests     xUnit
```

## What to test in step (a)

On a Windows PC with the published build:

1. **First run**: the Dashboard opens. Go to **Contacts**. You see the friendly empty state with an *Add your first contact* button and a *load demo data* link.
2. **Add a contact** (button, or `Ctrl+N`): type a name and a mobile like `98765 43210`, press **Enter**. The panel switches to the profile and a toast confirms. Try leaving both blank and saving to see inline errors.
3. **Duplicate warning**: add another contact and type the same mobile. A warning names the existing contact and offers *Open*.
4. **Tags**: *Manage tags* → add `VIP`, `Bridal 2026`; rename, recolour, merge, delete. Back in a contact, *Edit* and toggle tags.
5. **Filter**: search by name, city or part of a phone number; use the type chips; use the *Tags* dropdown with *Any/All*; sort.
6. **Bulk**: `Ctrl`/`Shift`-click several rows → bulk bar → *Add tag* / *Remove tag* / *Delete*. After deleting, press **Undo** in the toast.
7. **Import/Export**: *Export to Excel*, edit the file, *Import* it again. Duplicates are skipped and reported. `.csv` and `.vcf` (phone contacts) also work.
8. **Settings**: change theme (instant), language to हिन्दी (restart the app to apply), load/remove demo data.
9. **Keyboard**: `Ctrl+1`…`Ctrl+8` switch modules, `Ctrl+N` new item, `F5` refresh, `Esc` cancels a form.
10. Close and reopen: window size and the last screen are remembered.

## Known gaps in step (a)

- Contact **photo** field exists in the data model but there is no picker yet.
- The contact profile **timeline** and **balance** are placeholders until Tasks/Follow-ups/Payments/Fall-Pico exist.
- Global search (`Ctrl+K`), notifications, the system-tray icon, PIN login and backups are later steps.
- **Verified automatically on every push** (GitHub Actions, Windows): the whole solution including the XAML compiles, all 155 tests pass, and the published app starts on a clean machine, stays running, creates its database and writes no error log.
- **Not verified by anyone yet**: how the screens actually look and feel on a real display (layout at 1366×768 and 125–150% scaling, touch, colours), and whether switching to Hindi renders correctly on a device. The Hindi text itself is a first draft and should be reviewed by someone at the store.
