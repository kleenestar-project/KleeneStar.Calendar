![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar.Calendar - Appointments and Calendars

**KleeneStar.Calendar** adds appointments (*Termine*) to the **KleeneStar** platform and shows
them in the calendar of a workspace. An appointment is an object like an issue or an asset - with
workflow, comments, attachments, relations, history and permissions - and the plugin adds its time,
its participants and the resources it books.

- **Calendar** per workspace (month, week, agenda), between *Blogs* and *Issues* in the sidebar,
  with the next ten appointments of the coming seven days below.
- **Series** after RFC 5545 (daily, weekly, monthly, yearly, n-th weekday, end by date or count),
  repeating in their time zone, single occurrences removable.
- **Templates**: the templates of appointment classes preset duration, start time, place, series,
  participants and resources.
- **Status** from the class's workflow; appointments are closed automatically when they are over.
- **Participants** with accept, accept with reservations, decline and a comment; guests without
  account.
- **Resources**: the installation's assets (rooms, devices, vehicles) can be booked once at a time.
- **Conflicts** of participants and resources shown live while booking.
- **Included calendars**: other workspaces and any WQL query as layers of the calendar.
- **Import and export** of iCalendar files (`.ics`) - whole calendars and single objects.

Built on the **WebExpress-Framework** and **KleeneStar.Core**. The concept is in
[docs/kleenestar.calendar.md](docs/kleenestar.calendar.md).

## Build

The repository is cloned beside the other KleeneStar repositories; the host references it.

```
dotnet build KleeneStar/src/KleeneStar.slnx
KleeneStar.Calendar/src/KleeneStar.Calendar.Test/bin/Debug/net11.0/KleeneStar.Calendar.Test.exe
```

## License

MIT, see [LICENSE](LICENSE).
