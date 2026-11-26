using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Model.Entities;
using AppointmentEntity = KleeneStar.Model.Entities.Appointment;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Calendar.Test.WebManager
{
    /// <summary>
    /// Tests the appointment manager on an in-memory database: schedules, answers, bookings,
    /// layers, the calendar of a workspace and its conflicts.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestAppointmentManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("CA1E0000-0000-4000-8000-000000000001");
        private static readonly Guid OtherWorkspaceId = Guid.Parse("CA1E0000-0000-4000-8000-000000000002");
        private static readonly Guid ClassId = Guid.Parse("CA1E0000-0000-4000-8000-000000000003");
        private static readonly Guid OtherClassId = Guid.Parse("CA1E0000-0000-4000-8000-000000000004");
        private static readonly Guid AliceId = Guid.Parse("CA1E0000-0000-4000-8000-000000000005");
        private static readonly Guid BobId = Guid.Parse("CA1E0000-0000-4000-8000-000000000006");
        private static readonly Guid RoomId = Guid.Parse("CA1E0000-0000-4000-8000-000000000007");

        private static readonly DateTime Monday = new(2030, 3, 4, 9, 0, 0, DateTimeKind.Utc);

        private static void Seed(string database)
        {
            CalendarHubFixture.Initialize(database);

            using var db = CalendarHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "TEAM", Name = "Team" });
            db.Workspaces.Add(new Workspace { Id = OtherWorkspaceId, Key = "OPS", Name = "Operations" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Meeting", WorkspaceId = WorkspaceId, Kind = ObjectKind.Appointment });
            db.Classes.Add(new Class { Id = OtherClassId, Name = "Meeting", WorkspaceId = OtherWorkspaceId, Kind = ObjectKind.Appointment });
            db.Identities.Add(new Identity { Id = AliceId, Name = "Alice", UserName = "alice", Email = "alice@example.com", PasswordHash = "x" });
            db.Identities.Add(new Identity { Id = BobId, Name = "Bob", UserName = "bob", Email = "bob@example.com", PasswordHash = "x" });
            db.Objects.Add(new ObjectEntity(RoomId) { Key = "OPS-1", Summary = "Room 4.12", WorkspaceId = OtherWorkspaceId, ClassId = OtherClassId, Kind = ObjectKind.Asset });
            db.SaveChanges();
        }

        private static Guid Appointment(string database, Guid workspaceId, string summary, DateTime start, TimeSpan length, string recurrence = null)
        {
            var id = Guid.NewGuid();

            using (var db = CalendarHubFixture.CreateDbContext(database))
            {
                db.Objects.Add(new ObjectEntity(id)
                {
                    Key = "T-" + Random.Shared.Next(1000, 99999),
                    Summary = summary,
                    WorkspaceId = workspaceId,
                    ClassId = workspaceId == WorkspaceId ? ClassId : OtherClassId,
                    Kind = ObjectKind.Appointment
                });
                db.SaveChanges();
            }

            CalendarHub.AppointmentManager.SaveSchedule(new AppointmentEntity
            {
                ObjectId = id,
                Start = start,
                End = start + length,
                TimeZone = "UTC",
                Recurrence = recurrence
            }, Guid.Empty);

            return id;
        }

        private static Workspace Team(string database)
        {
            using var db = CalendarHubFixture.CreateDbContext(database);

            return db.Workspaces.First(x => x.Id == WorkspaceId);
        }

        [Fact]
        public void SaveSchedule_ResetsTheAnswersWhenTheTimeMoves()
        {
            var database = nameof(SaveSchedule_ResetsTheAnswersWhenTheTimeMoves);
            Seed(database);

            var manager = CalendarHub.AppointmentManager;
            var id = Appointment(database, WorkspaceId, "Planning", Monday, TimeSpan.FromHours(1));

            manager.SetParticipants(id,
            [
                new AppointmentParticipant { IdentityId = AliceId, Name = "Alice", Role = AppointmentParticipantRole.Chair },
                new AppointmentParticipant { IdentityId = BobId, Name = "Bob" }
            ]);

            manager.Respond(id, AliceId, AppointmentResponse.Accepted, null);
            manager.Respond(id, BobId, AppointmentResponse.Tentative, "only the first half");

            // the location changes, the time does not: the answers stand
            var schedule = manager.GetSchedule(id);
            schedule.Location = "Room 4.12";
            manager.SaveSchedule(schedule, AliceId);

            Assert.Equal("only the first half", manager.GetParticipants(id).Single(x => x.IdentityId == BobId).Comment);
            Assert.Equal(AppointmentResponse.Tentative, manager.GetParticipants(id).Single(x => x.IdentityId == BobId).Response);

            // the time moves: the answers of everybody but the one who moved it are reset
            schedule.Start = Monday.AddDays(1);
            schedule.End = Monday.AddDays(1).AddHours(1);
            manager.SaveSchedule(schedule, AliceId);

            var participants = manager.GetParticipants(id);
            Assert.Equal(AppointmentResponse.Accepted, participants.Single(x => x.IdentityId == AliceId).Response);
            Assert.Equal(AppointmentResponse.Pending, participants.Single(x => x.IdentityId == BobId).Response);
        }

        [Fact]
        public void SetParticipants_KeepsTheAnswersOfThoseInvitedBefore()
        {
            var database = nameof(SetParticipants_KeepsTheAnswersOfThoseInvitedBefore);
            Seed(database);

            var manager = CalendarHub.AppointmentManager;
            var id = Appointment(database, WorkspaceId, "Retro", Monday, TimeSpan.FromHours(1));

            manager.SetParticipants(id, [new AppointmentParticipant { IdentityId = AliceId, Name = "Alice" }]);
            manager.Respond(id, AliceId, AppointmentResponse.Declined, "on leave");

            var added = manager.SetParticipants(id,
            [
                new AppointmentParticipant { IdentityId = AliceId, Name = "Alice" },
                new AppointmentParticipant { IdentityId = BobId, Name = "Bob" },
                new AppointmentParticipant { IdentityId = BobId, Name = "Bob" },
                new AppointmentParticipant { Name = "Guest", Email = "guest@example.com" }
            ]);

            var participants = manager.GetParticipants(id);

            Assert.Equal(2, added.Count);
            Assert.Equal(3, participants.Count);
            Assert.Equal(AppointmentResponse.Declined, participants.Single(x => x.IdentityId == AliceId).Response);
            Assert.Equal("on leave", participants.Single(x => x.IdentityId == AliceId).Comment);
            Assert.Null(manager.Respond(id, Guid.NewGuid(), AppointmentResponse.Accepted, null));
        }

        [Fact]
        public void FindConflicts_NamesTakenParticipantsAndResources()
        {
            var database = nameof(FindConflicts_NamesTakenParticipantsAndResources);
            Seed(database);

            var manager = CalendarHub.AppointmentManager;
            var weekly = Appointment(database, OtherWorkspaceId, "Ops sync", Monday, TimeSpan.FromHours(1), "FREQ=WEEKLY;COUNT=10");
            manager.SetParticipants(weekly, [new AppointmentParticipant { IdentityId = AliceId, Name = "Alice" }]);
            manager.SetResources(weekly, [RoomId]);

            var declined = Appointment(database, WorkspaceId, "Lunch", Monday.AddDays(7), TimeSpan.FromHours(1));
            manager.SetParticipants(declined, [new AppointmentParticipant { IdentityId = BobId, Name = "Bob" }]);
            manager.Respond(declined, BobId, AppointmentResponse.Declined, null);

            // a week later, half an hour into the weekly sync
            var conflicts = manager.FindConflicts(new AppointmentConflictQuery
            {
                Start = Monday.AddDays(7).AddMinutes(30),
                End = Monday.AddDays(7).AddMinutes(90),
                TimeZone = "UTC",
                IdentityIds = [AliceId, BobId],
                ResourceIds = [RoomId]
            });

            Assert.Equal(2, conflicts.Count);
            Assert.Contains(conflicts, x => x.Kind == AppointmentConflictKind.Participant && x.SubjectId == AliceId && x.OtherObjectId == weekly);
            Assert.Contains(conflicts, x => x.Kind == AppointmentConflictKind.Resource && x.SubjectId == RoomId && x.SubjectName == "Room 4.12");
            Assert.All(conflicts, x => Assert.Equal(Monday.AddDays(7), x.Start));

            // touching is no overlap, and the appointment's own bookings do not count
            Assert.Empty(manager.FindConflicts(new AppointmentConflictQuery
            {
                Start = Monday.AddDays(7).AddHours(1),
                End = Monday.AddDays(7).AddHours(2),
                TimeZone = "UTC",
                IdentityIds = [AliceId],
                ResourceIds = [RoomId]
            }));

            Assert.Empty(manager.FindConflicts(new AppointmentConflictQuery
            {
                ObjectId = weekly,
                Start = Monday,
                End = Monday.AddHours(1),
                TimeZone = "UTC",
                ResourceIds = [RoomId]
            }));
        }

        [Fact]
        public void FindConflicts_OfASeriesChecksEveryOccurrence()
        {
            var database = nameof(FindConflicts_OfASeriesChecksEveryOccurrence);
            Seed(database);

            var manager = CalendarHub.AppointmentManager;
            var single = Appointment(database, WorkspaceId, "Workshop", Monday.AddDays(14), TimeSpan.FromHours(3));
            manager.SetResources(single, [RoomId]);

            var conflicts = manager.FindConflicts(new AppointmentConflictQuery
            {
                Start = Monday.AddHours(1),
                End = Monday.AddHours(2),
                TimeZone = "UTC",
                Recurrence = "FREQ=WEEKLY;COUNT=4",
                ResourceIds = [RoomId]
            });

            Assert.Equal(Monday.AddDays(14), Assert.Single(conflicts).Start);
        }

        [Fact]
        public void GetEntries_ShowsTheOwnAppointmentsAndTheIncludedWorkspace()
        {
            var database = nameof(GetEntries_ShowsTheOwnAppointmentsAndTheIncludedWorkspace);
            Seed(database);

            var manager = CalendarHub.AppointmentManager;
            var own = Appointment(database, WorkspaceId, "Standup", Monday, TimeSpan.FromMinutes(15), "FREQ=DAILY;COUNT=5");
            var other = Appointment(database, OtherWorkspaceId, "Release", Monday.AddDays(2), TimeSpan.FromHours(2));
            Appointment(database, WorkspaceId, "Last year", Monday.AddYears(-1), TimeSpan.FromHours(1));

            var workspace = Team(database);
            var week = manager.GetEntries(workspace, Monday.Date, Monday.Date.AddDays(7));

            Assert.Equal(5, week.Count(x => x.Object.Id == own));
            Assert.DoesNotContain(week, x => x.Object.Id == other);

            manager.AddSource(new AppointmentCalendarSource
            {
                WorkspaceId = WorkspaceId,
                Name = "Operations",
                Kind = AppointmentCalendarSource.WorkspaceKind,
                SourceWorkspaceId = OtherWorkspaceId,
                Color = "#2ec4b6"
            });

            var layered = manager.GetEntries(workspace, Monday.Date, Monday.Date.AddDays(7));
            var release = Assert.Single(layered, x => x.Object.Id == other);

            Assert.Equal("Operations", release.Source.Name);
            Assert.Equal(6, layered.Count);
            Assert.Equal(5, manager.GetEntries(workspace, Monday.Date, Monday.Date.AddDays(7), layers: false).Count);

            var upcoming = manager.GetUpcoming(workspace, Monday.AddHours(1), TimeSpan.FromDays(7), 3);
            Assert.Equal(3, upcoming.Count);
            Assert.True(upcoming.SequenceEqual(upcoming.OrderBy(x => x.Start)));
        }

        [Fact]
        public void Validate_RefusesALayerWithoutTarget()
        {
            var database = nameof(Validate_RefusesALayerWithoutTarget);
            Seed(database);

            var manager = CalendarHub.AppointmentManager;

            Assert.Contains("kleenestar.calendar:source.workspace.self", manager.Validate(new AppointmentCalendarSource { WorkspaceId = WorkspaceId, Name = "Me", Kind = "workspace", SourceWorkspaceId = WorkspaceId }));
            Assert.Contains("kleenestar.calendar:source.query.required", manager.Validate(new AppointmentCalendarSource { WorkspaceId = WorkspaceId, Name = "Q", Kind = "query" }));
            Assert.Contains("kleenestar.calendar:source.kind.required", manager.Validate(new AppointmentCalendarSource { WorkspaceId = WorkspaceId, Name = "X", Kind = "other" }));
            Assert.Throws<InvalidOperationException>(() => manager.AddSource(new AppointmentCalendarSource { WorkspaceId = WorkspaceId, Kind = "workspace" }));
        }

        [Fact]
        public void ExcludeOccurrence_RemovesOneOccurrenceOnly()
        {
            var database = nameof(ExcludeOccurrence_RemovesOneOccurrenceOnly);
            Seed(database);

            var manager = CalendarHub.AppointmentManager;
            var id = Appointment(database, WorkspaceId, "Standup", Monday, TimeSpan.FromMinutes(15), "FREQ=DAILY;COUNT=3");

            Assert.True(manager.ExcludeOccurrence(id, Monday.AddDays(1)));
            Assert.False(manager.ExcludeOccurrence(id, Monday.AddDays(1).AddMinutes(5)));

            var starts = manager.GetEntries(Team(database), Monday.Date, Monday.Date.AddDays(7)).Where(x => x.Object.Id == id).Select(x => x.Start).ToList();

            Assert.Equal([Monday, Monday.AddDays(2)], starts);
        }

        [Fact]
        public void Import_OfAnExportUpdatesTheAppointments()
        {
            var database = nameof(Import_OfAnExportUpdatesTheAppointments);
            Seed(database);

            var workspace = Team(database);
            var id = Appointment(database, WorkspaceId, "Review", Monday, TimeSpan.FromHours(1), "FREQ=WEEKLY;COUNT=3");
            Model.Entities.Class meeting;

            using (var db = CalendarHubFixture.CreateDbContext(database))
            {
                meeting = db.Classes.First(x => x.Id == ClassId);
            }

            var file = ICalendar.Write(AppointmentExchange.Events(workspace, false));
            var result = AppointmentExchange.Import(workspace, meeting, file.Replace("COUNT=3", "COUNT=4"), AliceId, _ => true, TimeZoneInfo.Utc);

            Assert.Equal(0, result.Created);
            Assert.Equal(1, result.Updated);
            Assert.Equal("FREQ=WEEKLY;COUNT=4", CalendarHub.AppointmentManager.GetSchedule(id).Recurrence);
        }

        [Fact]
        public void Import_UpdatesWhatItImportedBefore()
        {
            var database = nameof(Import_UpdatesWhatItImportedBefore);
            Seed(database);

            var workspace = Team(database);
            Model.Entities.Class meeting;

            using (var db = CalendarHubFixture.CreateDbContext(database))
            {
                meeting = db.Classes.First(x => x.Id == ClassId);
            }

            string File(string summary) =>
                "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:imported-1\r\n" +
                $"SUMMARY:{summary}\r\nDTSTART:20300304T090000Z\r\nDTEND:20300304T100000Z\r\n" +
                "ATTENDEE;CN=Alice;PARTSTAT=ACCEPTED:mailto:alice@example.com\r\n" +
                "ATTENDEE;CN=Visitor:mailto:visitor@example.com\r\n" +
                "END:VEVENT\r\nEND:VCALENDAR\r\n";

            var first = AppointmentExchange.Import(workspace, meeting, File("Kick-off"), AliceId, _ => true, TimeZoneInfo.Utc);
            var second = AppointmentExchange.Import(workspace, meeting, File("Kick-off (moved)"), AliceId, _ => true, TimeZoneInfo.Utc);

            Assert.Equal(1, first.Created);
            Assert.Equal(0, second.Created);
            Assert.Equal(1, second.Updated);

            var id = first.Objects.Single().Id;
            var participants = CalendarHub.AppointmentManager.GetParticipants(id);

            Assert.Equal(AppointmentResponse.Accepted, participants.Single(x => x.IdentityId == AliceId).Response);
            Assert.Contains(participants, x => x.IdentityId is null && x.Email == "visitor@example.com");
            Assert.Equal("imported-1", CalendarHub.AppointmentManager.GetSchedule(id).ExternalId);
        }
    }
}
