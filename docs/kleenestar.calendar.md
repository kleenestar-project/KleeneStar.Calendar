![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Calendar and Appointments

**KleeneStar.Calendar** (plugin id `kleenestar.calendar`) adds the object kind **appointment**
(`appointment`, German *Termin*) and the **calendar** of a workspace. Not to be confused with the
core's working-hours calendars of SLAs (`KleeneStar.Core/docs/kleenestar.calendar.md`), which this
plugin neither uses nor changes.

An appointment is an ordinary object of a class of the kind: its title, description, workflow,
comments, attachments, relations, history, security level and permissions are the object's, and
the core's object detail experience shows them. The plugin adds what an object has no column for.

## Data model

The tables live in `KleeneStar.Model` (migration `AddAppointments`), because the installation has
one database and one migration history; without the plugin they are not read.

| Entity | Content |
|--------|---------|
| `Appointment` | One per appointment object (`ObjectId` unique, cascade): `Start`/`End` (UTC), `AllDay`, `TimeZone`, `Location`, `Recurrence` (RRULE), `Exceptions` (removed occurrence starts), `SeriesEnd` (derived: end of the last occurrence, `null` for an open series), `AutoClose`, `Closed`, `OrganizerId`, `ExternalId` (iCalendar `UID`) |
| `AppointmentParticipant` | `IdentityId` (no FK) or a guest (`Name`, `Email`), `Role` (required, optional, chair), `Response` (pending, accepted, tentative, declined), `Comment` (the reservation or reason), `Responded` |
| `AppointmentResource` | The booking of a resource: `ObjectId` (cascade) and `ResourceId`, an object of the **asset** kind (no FK; dropped when the asset is removed) |
| `AppointmentCalendarSource` | A further layer of a workspace calendar: `Kind` `workspace` (`SourceWorkspaceId`) or `query` (`Query`, WQL), `Color`, `Enabled`, `Order` |

Times are stored in UTC; a series repeats in the local time of its `TimeZone` (a weekly meeting at
ten stays at ten across daylight saving time). A local time the clocks skip is read with the offset
before the jump; an ambiguous one as the earlier instant. An all-day appointment runs from midnight
of its first day to midnight after its last day in its zone.

## Software architecture

| Part | Role |
|------|------|
| `CalendarHub` | Service locator: `AppointmentManager`, `Settings` |
| `WebManager/AppointmentManager` | Schedules, participants, bookings, layers, `GetEntries`, `GetUpcoming`, `FindConflicts`, `CloseExpired`; audits through `CoreHub.AuditManager` in `Connect()` |
| `WebAppointment/RecurrenceRule` | RRULE subset of RFC 5545: `FREQ` DAILY/WEEKLY/MONTHLY/YEARLY, `INTERVAL`, `COUNT`, `UNTIL`, `BYDAY` (with ordinals `2TU`, `-1FR`), `BYMONTHDAY`, `BYMONTH`; other parts are refused |
| `WebAppointment/AppointmentSchedule` | Occurrences of a schedule in a period, exceptions, `Normalize` (derived columns) |
| `WebAppointment/ICalendar`, `AppointmentExchange` | Reading and writing `.ics` (folding, escaping, `TZID`, `DURATION`, `EXDATE`, `RECURRENCE-ID`, attendees with `PARTSTAT`) and the import into a workspace |
| `WebAppointment/AppointmentCloser` | Timer closing what is over |
| `WebAppointment/IcsObjectExportFormat` | Format `ics` of the core's export dialog |
| `WebWorkspaceTemplate/CalendarWorkspaceTemplate` | Workspace template *Team calendar* |

Reads of objects always go through `CoreHub.ObjectManager`, so content visibility and security
levels apply. The one question asked across that boundary is a conflict: whether a time is taken is
answered from every booking (unrestricted), what it is taken by only for appointments the caller
may see (otherwise "another appointment").

The core offers two scope interfaces for add-on kinds (`KleeneStar.Core/WebScope`):
`IScopeObjectKindOverview` gives a kind overview the sidebar chrome of the core's overviews (header,
icon, kind links, settings toolbar); `IScopeObjectKindDetail` gives a detail page the full object
detail experience of the asset kind. The calendar page and the appointment page implement them.
`ObjectKindConfiguredCondition` and `ObjectBoardProjection` are public for add-ons.

## UI

- **Sidebar**: *Kalender* between *Blogs* (order 2) and *Umfragen* (order 4, `KleeneStar.Poll`) or *Issues* (order 5) on every kind page, where
  the workspace has an appointment class. On the calendar and appointment pages: *Coming up* - the
  next 10 entries within 7 days (`UpcomingCount`, `UpcomingDays`; closed and cancelled ones left
  out) - and *Calendars* (the own calendar and the layers, *Include calendar…*, a layer opens its
  removal).
- **Calendar** (`/calendar/{workspacekey}`): `ControlDataSchedule` with month, week (time axis) and
  agenda. A drag moves a single appointment (a series or a layer entry is refused with 409); drawing
  a span plans a new appointment in the first appointment class. Headline: *New appointment* and a
  dropdown with the templates of the appointment classes, *Include calendar…*, *Import iCalendar…*,
  *Export as iCalendar* (with or without layers).
- **Appointment page** (`/appointment/{objectkey}`): the core's detail experience plus the schedule
  card (time, series in words, next occurrence, zone, location, organizer, closing), conflicts card,
  participants card (answers with reservations; *Accept*, *With reservations*, *Decline* for an
  invited reader) and resources card. *Edit appointment* opens the dialog; a series offers *Remove
  occurrence*.
- **Appointment dialog**: tabs *Appointment* (title, type and template for a new one, start and end
  as date plus `HH:mm`, all day, zone, closing, location, description), *Series* (frequency,
  interval, weekdays, end date or count, or a rule written out), *Participants* (accounts, guests as
  `Name <mail>`), *Resources* (assets, *Book despite conflicts*). Below the tabs the conflict panel
  (`kleenestar.calendar.conflicts.js`) asks `/api/1/calendar/{key}/conflicts` on every change.
  Saving refuses a resource booked twice unless booked anyway; a participant's conflict is shown,
  not refused.

## Templates

Appointment templates are the core's templates of appointment classes. Besides `Summary`,
`Description` and field presets, a template carries calendar presets under the add-on keys
`kleenestar.calendar:duration` (minutes), `:starttime` (`HH:mm`), `:allday`, `:location`,
`:recurrence` (RRULE), `:participants` and `:resources` (ids separated by semicolons) and
`:autoclose`. The core's template wizard keeps keys of the form `{plugin id}:{name}`
(`TemplatePresetForm.IsAddOnKey`). The template travels as the add dialog's item id, so its load
and its submit name it.

## Status and automatic closing

The workflow of an appointment class decides its states (the template: *Planned* → *Confirmed* →
*Held*, *Cancelled*, *Plan again*). `AppointmentCloser` runs every `AutoCloseInterval` minutes
(default 5, 0 switches it off) and moves every appointment with `AutoClose` whose `SeriesEnd` lies
more than `AutoCloseGrace` minutes in the past into the first closing state (status category
*Done*) its workflow leads to, as a transition without actor. A series without end is never closed.
The closing is idempotent and safe on several replicas. Moving an appointment into the future opens
`Closed` again; moving its time resets the answers of all participants but the one who moved it.

## REST endpoints

Plugin routes lie under `/kleenestar/kleenestar.calendar/`.

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `api/1/calendar/{workspacekey}` | GET, PUT, POST | Schedule control: entries of `from`/`to`, move, quick create |
| `api/1/calendar/{workspacekey}/appointment` | GET, POST | Add dialog (`?id=` template) |
| `api/1/calendar/{workspacekey}/conflicts` | POST | Conflicts of the posted dialog |
| `api/1/calendar/{workspacekey}/sources` | GET, POST | Layers; include a layer |
| `api/1/calendar/{workspacekey}/import` | GET, POST | Import (JSON with `Content`, or a `text/calendar` body with `?class=`) |
| `api/1/calendar/{workspacekey}/export` | GET | `{key}.ics` (`?layers=true`) |
| `api/1/calendar/{workspacekey}/classes`, `templates`, `participants`, `resources`, `workspaces` | GET | Selections of the dialogs |
| `api/1/appointments/{objectkey}` | GET, PUT, POST | Edit dialog |
| `api/1/appointments/{objectkey}/respond` | GET, POST | Answer of the caller |
| `api/1/appointments/{objectkey}/exclude` | GET, POST | Remove an occurrence |
| `api/1/calendarsources/{sourceid}` | GET, POST, DELETE | Remove a layer |

## Permissions

The plugin's pages guard themselves (`AppointmentAuthorization`): the calendar needs
`WorkspaceReadContentPermission`, arranging layers `WorkspaceWriteContentPermission`, planning an
appointment the right to create in its class, changing one `ContentAuthorization.MayWrite`. An
invited account answers for itself. A layer is read with the reader's visibility, so including a
calendar reveals nothing.

## Settings

`Plugins:kleenestar.calendar`: `TimeZone` (zone of new appointments when the account names none;
default the host's), `AutoCloseInterval` (5), `AutoCloseGrace` (0), `UpcomingDays` (7),
`UpcomingCount` (10). The zone of a request is the account's `TimeZone`, else the setting.
