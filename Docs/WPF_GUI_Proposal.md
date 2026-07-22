# Jolimont Bike Race — Modern WPF GUI Proposal

**Target stack:** WPF on **.NET 8** (LTS — the successor of .NET Core), MVVM with
`CommunityToolkit.Mvvm`, **WPF-UI** (Fluent / Windows 11 design) for controls and theming,
`Npgsql` (or EF Core + Npgsql provider) for PostgreSQL access, async throughout.

**Ground rule:** the application keeps exactly the same four sections and the same
behaviors as the 2016 WinForms app — Race Manager, Bikers, Chrono, Standings (+ Exit) —
but each screen is reorganized around the way it is actually used on race day.

---

## 1. Application shell

```
┌──────────────────────────────────────────────────────────────────────────┐
│ ☰  Jolimont Bike Race                                    🌓  ⛁ Connected │
├──────────┬───────────────────────────────────────────────────────────────┤
│ 🗂 Race   │                                                               │
│   Manager│                                                               │
│ 🚴 Bikers │                    <active section>                           │
│ ⏱ Chrono │                                                               │
│ 🏆 Stand- │                                                               │
│   ings   │                                                               │
│          │                                                               │
│ ⏻ Exit   │                                                               │
├──────────┴───────────────────────────────────────────────────────────────┤
│ Status: Jolimont Bike Race 2026 – Adultes · 47 registered · last save 10:31:04 │
└──────────────────────────────────────────────────────────────────────────┘
```

- **NavigationView** (WPF-UI) replaces the button column: same five entries, same icons
  semantics, collapsible to icon-only for more working area on the race-day laptop.
- **Status bar** (new, passive): DB connection state, active race, count of registered
  riders, timestamp of last XML/DB save — information the old app never surfaced.
- **Global race context**: the currently selected race is chosen once (combo in the
  header or in Chrono) and shared by all sections, instead of re-selecting the race in
  every screen.
- Light/dark theme toggle; dark theme is genuinely useful outdoors under a tent.
- All DB work is `async` — the UI never freezes while loading or saving (the old
  WinForms app blocked on every grid refresh).

---

## 2. Race Manager

Same behavior: maintain categories, maintain races, link categories to races
(`category`, `race`, `race_category`).

```
┌ Races ────────────────┐ ┌ Selected race: JBR 2026 – Adultes ──────────────┐
│ 🔍 filter…    [+ New] │ │ Name  [Jolimont Bike Race 2026 - Adultes     ]  │
│ ▸ JBR 2026 – Adultes  │ │ Start [not started]          (set by Chrono)   │
│   JBR 2026 – Enfants  │ │                                                 │
│   JBR 2016 – Adultes  │ │ Categories in this race                         │
│   JBR 2016 – Enfants  │ │ ☑ Adultes – Hommes      bibs 0–150              │
│   …                   │ │ ☑ Adultes – Femmes      bibs 0–150              │
│                       │ │ ☐ Enfants – 1 tour      bibs 1–150              │
│                       │ │ ☐ Enfants – 2 tours     bibs 1–150              │
└───────────────────────┘ └─────────────────────────────────────────────────┘
┌ Categories (master list) ────────────────────────────────────────────────┐
│ Name                │ Min bib │ Max bib │            [+ Add] [✎] [🗑]     │
│ Adultes – Hommes    │ 0       │ 150     │                                 │
│ …                                                                        │
└──────────────────────────────────────────────────────────────────────────┘
```

Efficiency changes (behavior preserved):

- **Master–detail** instead of three unrelated grids: select a race on the left, see and
  edit *its* linked categories on the right. The checkbox list is the same
  `race_category` editor, but scoped to the selected race so the operator always knows
  what they are editing.
- Checkbox changes are saved immediately (with an undo toast) — the separate
  **Validate** button disappears; there is no "did I validate?" doubt.
- Category rows validate `min ≤ max` and warn when two categories of the same race have
  overlapping bib ranges (data-quality issue the old app allowed silently).
- "New edition" helper: duplicate a past race with its category links to create the
  next year's race in one click (pure convenience — same underlying inserts).

---

## 3. Bikers

Same behavior: maintain the rider registry (`biker`) and register a rider to a
race/category with a bib number (`biker_race_category`).

```
┌ 🔍 Search name / bib / e-mail…            [+ New biker]  [Import CSV]  [Print] ┐
├────────────────────────────────────────────────┬───────────────────────────────┤
│ Name ▲        │ Born │ Locality        │ Bib   │  Stephane Boillat (1965)      │
│ Boillat S.    │ 1965 │ Le Fuet         │  12   │  ─────────────────────────    │
│ Boillat A.    │ 1996 │ Le Fuet         │  —    │  ✎ Contact details (form)     │
│ Boillat M.    │ 2000 │ Le Fuet         │  —    │                               │
│ Racine M.     │ 1954 │ Courtelary      │  7    │  Registration                 │
│ …             │      │                 │       │  Race     [JBR 2026 Adultes ▾]│
│               │      │                 │       │  Category [Adultes–Hommes  ▾] │
│               │      │                 │       │           bibs 0–150, 43 used │
│               │      │                 │       │  Bib nr   [ 44 ] (next free)  │
│               │      │                 │       │        [ Register ]           │
│               │      │                 │       │  ─────────────────────────    │
│               │      │                 │       │  History: 2016 Adultes #22 …  │
└────────────────────────────────────────────────┴───────────────────────────────┘
```

Efficiency changes:

- **Instant search box** filtering the grid as you type (name, bib, e-mail) — the single
  biggest win over the old app, where the operator scrolled a 275-row grid at the
  registration desk.
- Grid is virtualized and read-only; editing happens in the right-hand **detail panel**
  (same fields: first/last name, year of birth, address, e-mail, phone, natel/portable).
- The registration panel is the same Race → Category → Number → Validate flow, renamed
  **Register**, with three built-in guards:
  - category combo only offers categories linked to the chosen race (`race_category`);
  - bib number is pre-filled with the **next free number** in the category range and
    live-validated against min/max and duplicates (old app validated only on click);
  - suggested category based on year of birth when category rules allow it.
- Rider's registration **history** (past editions and bib numbers) shown in the panel —
  data that always existed in `biker_race_category` but was never visible.
- Optional CSV import for pre-registrations collected online.

---

## 4. Chrono (live timing)

Same behavior: pick race → Start (writes `race.racetick`) → record one row per
finish-line crossing (`race_standings`) → reset options → XML journal → DB commit.
This screen is redesigned to be **keyboard-first**, because during the race the operator
has no time for the mouse.

```
┌ Race [JBR 2026 – Adultes ▾]      ⏱ 00:42:17.3        Started 10:01:39  ┐
│                                                                        │
│   BIB ▸ [ 34_ ]   ⏎ = record crossing        [ SPACE = crossing, bib   │
│                                                 entered afterwards ]   │
├────────────────────────────────────────────────────────────────────────┤
│ #  │ Time of day │ Race time │ Bib │ Rider            │ Lap │          │
│ 57 │ 10:43:56    │ 00:42:17  │ 34  │ M. Racine        │ 3   │ ↺ undo   │
│ 56 │ 10:43:41    │ 00:42:02  │ 12  │ S. Boillat       │ 3   │          │
│ 55 │ 10:42:59    │ 00:41:20  │ ?   │ — assign bib —   │     │ [assign] │
│ …  │             │           │     │                  │     │          │
├────────────────────────────────────────────────────────────────────────┤
│ Autosaved to XML 10:43:56 · DB synced 10:43:56   [Start] [Reset race ⚠]│
│                                                  [Reset standings ⚠]   │
└────────────────────────────────────────────────────────────────────────┘
```

Efficiency changes:

- **One giant input field**: type the bib and press **Enter** — that is "New standing".
  Pressing **Space** with an empty field records a crossing *now* with an unassigned
  bib (rider bunches cross faster than you can type); unassigned rows are highlighted
  and can be completed afterwards. Timestamp accuracy is preserved because the tick is
  taken at keypress, not after data entry.
- **Undo last crossing** (Ctrl+Z / ↺): the old app could only reset the whole list.
- Bib is resolved live against `biker_race_category` → rider name + lap count shown
  immediately; unknown bibs are flaged but still recorded (never lose a timing point).
- **Autosave replaces the three manual buttons**: after *every* crossing the XML journal
  is rewritten and an async DB commit is queued. *Load Standings XML* remains available
  (crash recovery), and *Update database* remains as a manual "force sync" — same
  capabilities, but the safe path is now the default instead of depending on the
  operator remembering to click.
- **Start** asks for confirmation and is disabled once ticks exist; **Reset race** /
  **Reset race standings** keep their destructive behavior but sit behind a red
  confirmation dialog stating exactly what will be deleted.
- Big-font timer and time-of-day (readable from a distance), newest crossing on top.

---

## 5. Standings

Same behavior: pick race + categories → compute classification (`standing`) → print.

```
┌ Race [JBR 2026 – Adultes ▾]   Categories ☑ Hommes ☑ Femmes   [Compute] ┐
├──────────────── tab: Adultes–Hommes ─┬─ tab: Adultes–Femmes ───────────┤
│ Pos │ Bib │ Rider          │ Laps │ Race time │ Gap                    │
│ 1   │ 1   │ …              │ 8    │ 1:23:45   │ —                      │
│ 2   │ 2   │ …              │ 8    │ 1:25:02   │ +1:17                  │
│ …                                                                      │
├─────────────────────────────────────────────────────────────────────────┤
│              [🖨 Print preview]  [PDF]  [CSV]  [Save to database]        │
└─────────────────────────────────────────────────────────────────────────┘
```

Efficiency changes:

- Results shown in **one tab per category** instead of a single flat list; the print
  output keeps the official per-category layout.
- **Live mode**: while Chrono is running, standings recompute automatically from
  `race_standings` (read-only preview) — the speaker can announce provisional rankings
  without touching the timing screen. *Save to database* still performs the explicit
  write to `standing` for the official result, exactly like before.
- **Print preview** plus PDF and CSV export (the old app printed blind).
- Computation rule is unchanged: order by laps completed, then by last-crossing tick;
  `racetime` and `gap` formatted exactly as in the 2016 output.

---

## 6. Architecture

```
JolimontBikeRace.sln
├─ src/JBR.App            WPF (.NET 8) — views, shell, DI bootstrap
│   ├─ Views/             RaceManagerView, BikersView, ChronoView, StandingsView (XAML)
│   └─ ViewModels/        one VM per view + ShellViewModel (CommunityToolkit.Mvvm)
├─ src/JBR.Core           domain models (Biker, Race, Category, Registration,
│                         Crossing, StandingEntry), standings calculator, tick helpers
├─ src/JBR.Data           Npgsql repositories (async), XML journal reader/writer
│                         (same file formats as 2016 for compatibility)
└─ tests/JBR.Core.Tests   standings computation + bib validation unit tests
```

- **MVVM** end to end; no code-behind logic, so timing logic is unit-testable.
- The XML journal format stays **byte-compatible** with the 2016 files
  (`RACE_STANDINGS` / `RACE_DATETIME`), so old exports can be reloaded.
- Ticks remain .NET `DateTime.Ticks` (`DateTime.Now.Ticks`) — full continuity with the
  existing database contents.
- Connection string in `appsettings.json`; the schema is the one created by
  `Database/create_jolimontbikerace.sql`.

## 7. Migration path

1. Run `Database/create_jolimontbikerace.sql` on a current PostgreSQL (16/17).
2. Restore the 2016 data with `pg_restore --data-only` if historical data is wanted
   (FKs are compatible with the legacy data since they mirror the implied relations).
3. Build the WPF app against the same schema — no data migration needed.
