using KleeneStar.Calendar.WebAppointment;
using System.Text;

namespace KleeneStar.Calendar.Test.WebAppointment
{
    /// <summary>
    /// Tests reading and writing iCalendar files.
    /// </summary>
    public class UnitTestICalendar
    {
        [Fact]
        public void Write_ThenRead_KeepsTheEvent()
        {
            var item = new ICalendarEvent
            {
                Uid = "abc@kleenestar",
                Summary = "Sprint review; demo, retro",
                Description = "Line one\nLine two",
                Location = "Room 4.12",
                Start = new DateTime(2026, 11, 25, 13, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 11, 25, 14, 30, 0, DateTimeKind.Utc),
                TimeZone = "Europe/Berlin",
                Recurrence = "FREQ=WEEKLY;BYDAY=WE;COUNT=4",
                Status = "CONFIRMED"
            };

            item.Exceptions.Add(new DateTime(2026, 12, 2, 13, 0, 0, DateTimeKind.Utc));
            item.Attendees.Add(new ICalendarAttendee { Name = "Alex Smith", Email = "alex@example.com", Role = "REQ-PARTICIPANT", Status = "TENTATIVE", Comment = "only from 14:00" });

            var events = ICalendar.Read(ICalendar.Write([item], "Team"));

            var read = Assert.Single(events);
            Assert.Equal(item.Uid, read.Uid);
            Assert.Equal(item.Summary, read.Summary);
            Assert.Equal(item.Description, read.Description);
            Assert.Equal(item.Location, read.Location);
            Assert.Equal(item.Start, read.Start);
            Assert.Equal(item.End, read.End);
            Assert.Equal(item.Recurrence, read.Recurrence);
            Assert.Equal("Europe/Berlin", read.TimeZone);
            Assert.Equal(item.Exceptions, read.Exceptions);

            var attendee = Assert.Single(read.Attendees);
            Assert.Equal("Alex Smith", attendee.Name);
            Assert.Equal("alex@example.com", attendee.Email);
            Assert.Equal("TENTATIVE", attendee.Status);
            Assert.Equal("only from 14:00", attendee.Comment);
        }

        [Fact]
        public void Write_FoldsLongLinesAt75Octets()
        {
            var item = new ICalendarEvent
            {
                Uid = "long",
                Summary = new string('ä', 120),
                Start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc)
            };

            var text = ICalendar.Write([item]);

            Assert.All(text.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line));
            Assert.Equal(item.Summary, Assert.Single(ICalendar.Read(text)).Summary);
        }

        [Fact]
        public void Read_UnderstandsWhatCalendarsWrite()
        {
            const string text =
                "BEGIN:VCALENDAR\r\n" +
                "VERSION:2.0\r\n" +
                "BEGIN:VTIMEZONE\r\nTZID:W. Europe Standard Time\r\nEND:VTIMEZONE\r\n" +
                "BEGIN:VEVENT\r\n" +
                "UID:040000008200E00074C5B7101A82E0080000000\r\n" +
                "SUMMARY:Board meet\r\n ing\r\n" +
                "DTSTART;TZID=W. Europe Standard Time:20260715T090000\r\n" +
                "DURATION:PT1H30M\r\n" +
                "ATTENDEE;CN=\"Kim, Lee\";PARTSTAT=DECLINED:mailto:kim@example.com\r\n" +
                "BEGIN:VALARM\r\nTRIGGER:-PT15M\r\nDESCRIPTION:Reminder\r\nEND:VALARM\r\n" +
                "END:VEVENT\r\n" +
                "BEGIN:VEVENT\r\n" +
                "UID:holiday\r\n" +
                "SUMMARY:Company holiday\r\n" +
                "DTSTART;VALUE=DATE:20261224\r\n" +
                "DTEND;VALUE=DATE:20261227\r\n" +
                "END:VEVENT\r\n" +
                "END:VCALENDAR\r\n";

            var events = ICalendar.Read(text, "Europe/Berlin");

            Assert.Equal(2, events.Count);

            var meeting = events[0];
            Assert.Equal("Board meeting", meeting.Summary);
            Assert.Equal(new DateTime(2026, 7, 15, 7, 0, 0, DateTimeKind.Utc), meeting.Start);
            Assert.Equal(TimeSpan.FromMinutes(90), meeting.End - meeting.Start);
            Assert.Null(meeting.Description);
            Assert.Equal("Kim, Lee", Assert.Single(meeting.Attendees).Name);
            Assert.Equal("DECLINED", meeting.Attendees[0].Status);

            var holiday = events[1];
            Assert.True(holiday.AllDay);
            Assert.Equal(TimeSpan.FromDays(3), holiday.End - holiday.Start);
        }

        [Fact]
        public void Read_SkipsAnEventWithoutStart()
        {
            Assert.Empty(ICalendar.Read("BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:x\r\nSUMMARY:no time\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n"));
        }
    }
}
