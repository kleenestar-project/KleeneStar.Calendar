using KleeneStar.Calendar.WebAppointment;
using AppointmentEntity = KleeneStar.Model.Entities.Appointment;

namespace KleeneStar.Calendar.Test.WebAppointment
{
    /// <summary>
    /// Tests the recurrence rules and the occurrences of a schedule.
    /// </summary>
    public class UnitTestRecurrenceRule
    {
        [Theory]
        [InlineData("FREQ=WEEKLY;BYDAY=MO,WE;COUNT=10", "FREQ=WEEKLY;BYDAY=MO,WE;COUNT=10")]
        [InlineData("RRULE:freq=daily;interval=2", "FREQ=DAILY;INTERVAL=2")]
        [InlineData("FREQ=MONTHLY;BYDAY=2TU;UNTIL=20271231", "FREQ=MONTHLY;BYDAY=2TU;UNTIL=20271231T235959Z")]
        [InlineData("FREQ=YEARLY;BYMONTH=3;BYMONTHDAY=-1", "FREQ=YEARLY;BYMONTH=3;BYMONTHDAY=-1")]
        public void Parse_WritesTheRuleBack(string text, string expected)
        {
            Assert.True(RecurrenceRule.TryParse(text, out var rule, out var error), error);
            Assert.Equal(expected, rule.ToString());
        }

        [Theory]
        [InlineData("BYDAY=MO")]
        [InlineData("FREQ=HOURLY")]
        [InlineData("FREQ=WEEKLY;BYSETPOS=1")]
        [InlineData("FREQ=WEEKLY;COUNT=2;UNTIL=20270101")]
        [InlineData("FREQ=WEEKLY;BYDAY=XX")]
        public void Parse_RefusesWhatItCannotRepeat(string text)
        {
            Assert.False(RecurrenceRule.TryParse(text, out _, out var error));
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [Fact]
        public void Weekly_FallsOnTheNamedDays()
        {
            var rule = RecurrenceRule.Parse("FREQ=WEEKLY;BYDAY=MO,WE;COUNT=5");
            var start = new DateTime(2026, 11, 2, 10, 0, 0); // a Monday

            var occurrences = rule.Expand(start).ToList();

            Assert.Equal(
            [
                new DateTime(2026, 11, 2, 10, 0, 0),
                new DateTime(2026, 11, 4, 10, 0, 0),
                new DateTime(2026, 11, 9, 10, 0, 0),
                new DateTime(2026, 11, 11, 10, 0, 0),
                new DateTime(2026, 11, 16, 10, 0, 0)
            ], occurrences);
        }

        [Fact]
        public void Weekly_EverySecondWeek()
        {
            var rule = RecurrenceRule.Parse("FREQ=WEEKLY;INTERVAL=2;COUNT=3");
            var start = new DateTime(2026, 11, 5, 9, 30, 0);

            Assert.Equal(
            [
                new DateTime(2026, 11, 5, 9, 30, 0),
                new DateTime(2026, 11, 19, 9, 30, 0),
                new DateTime(2026, 12, 3, 9, 30, 0)
            ], rule.Expand(start).ToList());
        }

        [Fact]
        public void Monthly_SecondTuesdayAndLastFriday()
        {
            var second = RecurrenceRule.Parse("FREQ=MONTHLY;BYDAY=2TU;COUNT=3").Expand(new DateTime(2026, 1, 13, 14, 0, 0)).ToList();
            var last = RecurrenceRule.Parse("FREQ=MONTHLY;BYDAY=-1FR;COUNT=3").Expand(new DateTime(2026, 1, 30, 14, 0, 0)).ToList();

            Assert.Equal([new DateTime(2026, 1, 13, 14, 0, 0), new DateTime(2026, 2, 10, 14, 0, 0), new DateTime(2026, 3, 10, 14, 0, 0)], second);
            Assert.Equal([new DateTime(2026, 1, 30, 14, 0, 0), new DateTime(2026, 2, 27, 14, 0, 0), new DateTime(2026, 3, 27, 14, 0, 0)], last);
        }

        [Fact]
        public void Monthly_SkipsMonthsWithoutTheDay()
        {
            var rule = RecurrenceRule.Parse("FREQ=MONTHLY;COUNT=4");

            Assert.Equal(
            [
                new DateTime(2026, 1, 31, 8, 0, 0),
                new DateTime(2026, 3, 31, 8, 0, 0),
                new DateTime(2026, 5, 31, 8, 0, 0),
                new DateTime(2026, 7, 31, 8, 0, 0)
            ], rule.Expand(new DateTime(2026, 1, 31, 8, 0, 0)).ToList());
        }

        [Fact]
        public void Yearly_LeapDayOnlyInLeapYears()
        {
            var rule = RecurrenceRule.Parse("FREQ=YEARLY;COUNT=2");

            Assert.Equal([new DateTime(2028, 2, 29), new DateTime(2032, 2, 29)], rule.Expand(new DateTime(2028, 2, 29)).ToList());
        }

        [Fact]
        public void Until_EndsTheSeriesOnTheLastDay()
        {
            var rule = RecurrenceRule.Parse("FREQ=DAILY;UNTIL=20261105");

            Assert.Equal(5, rule.Expand(new DateTime(2026, 11, 1, 18, 0, 0)).Count());
        }

        [Fact]
        public void Occurrences_KeepTheLocalTimeAcrossDaylightSavingTime()
        {
            // Monday 19 October 2026 10:00 in Berlin is 08:00 UTC (summer time); a week after the
            // clocks went back on 25 October it is 09:00 UTC - and still 10:00 local
            var zone = AppointmentTime.Zone("Europe/Berlin");
            var start = AppointmentTime.ToUtc(new DateTime(2026, 10, 19, 10, 0, 0), zone);

            var schedule = new AppointmentEntity
            {
                ObjectId = Guid.NewGuid(),
                Start = start,
                End = start.AddHours(1),
                TimeZone = "Europe/Berlin",
                Recurrence = "FREQ=WEEKLY;COUNT=2"
            };

            var occurrences = AppointmentSchedule.AllOccurrences(schedule, 10).ToList();

            Assert.Equal(2, occurrences.Count);
            Assert.Equal(new DateTime(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc), occurrences[0].Start);
            Assert.Equal(new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc), occurrences[1].Start);
            Assert.All(occurrences, x => Assert.Equal(TimeSpan.FromHours(1), x.End - x.Start));
        }

        [Fact]
        public void Occurrences_LeaveOutTheExceptions()
        {
            var start = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
            var schedule = new AppointmentEntity
            {
                ObjectId = Guid.NewGuid(),
                Start = start,
                End = start.AddMinutes(30),
                TimeZone = "UTC",
                Recurrence = "FREQ=DAILY;COUNT=3",
                Exceptions = AppointmentSchedule.FormatExceptions([start.AddDays(1)])
            };

            var occurrences = AppointmentSchedule.AllOccurrences(schedule, 10).Select(x => x.Start).ToList();

            Assert.Equal([start, start.AddDays(2)], occurrences);
            Assert.All(AppointmentSchedule.AllOccurrences(schedule, 10), x => Assert.Contains("@", x.Id));
        }

        [Fact]
        public void Occurrences_OfAPeriodAreThoseOverlappingIt()
        {
            var start = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
            var schedule = new AppointmentEntity
            {
                ObjectId = Guid.NewGuid(),
                Start = start,
                End = start.AddHours(2),
                TimeZone = "UTC",
                Recurrence = "FREQ=DAILY"
            };

            var occurrences = AppointmentSchedule.Occurrences(schedule, start.AddDays(10).AddHours(1), start.AddDays(12)).ToList();

            // the one on day 10 still runs at 10:00, the one on day 12 starts at the end
            Assert.Equal([start.AddDays(10), start.AddDays(11)], occurrences.Select(x => x.Start).ToList());
        }

        [Fact]
        public void Normalize_ComputesTheEndOfTheSeries()
        {
            var start = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
            var bounded = new AppointmentEntity { ObjectId = Guid.NewGuid(), Start = start, End = start.AddHours(1), TimeZone = "UTC", Recurrence = "FREQ=WEEKLY;COUNT=3" };
            var open = new AppointmentEntity { ObjectId = Guid.NewGuid(), Start = start, End = start.AddHours(1), TimeZone = "UTC", Recurrence = "FREQ=WEEKLY" };
            var single = new AppointmentEntity { ObjectId = Guid.NewGuid(), Start = start, End = start.AddHours(-1), TimeZone = "UTC", Recurrence = " " };

            AppointmentSchedule.Normalize(bounded);
            AppointmentSchedule.Normalize(open);
            AppointmentSchedule.Normalize(single);

            Assert.Equal(start.AddDays(14).AddHours(1), bounded.SeriesEnd);
            Assert.Null(open.SeriesEnd);
            Assert.Null(single.Recurrence);
            Assert.Equal(start, single.End);
            Assert.Equal(start, single.SeriesEnd);
        }

        [Fact]
        public void Normalize_AllDayCoversWholeLocalDays()
        {
            var zone = AppointmentTime.Zone("Europe/Berlin");
            var schedule = new AppointmentEntity
            {
                ObjectId = Guid.NewGuid(),
                Start = AppointmentTime.ToUtc(new DateTime(2026, 12, 24, 15, 0, 0), zone),
                End = AppointmentTime.ToUtc(new DateTime(2026, 12, 26, 11, 0, 0), zone),
                AllDay = true,
                TimeZone = "Europe/Berlin"
            };

            AppointmentSchedule.Normalize(schedule);

            Assert.Equal(new DateTime(2026, 12, 24), AppointmentTime.ToLocal(schedule.Start, zone));
            Assert.Equal(new DateTime(2026, 12, 27), AppointmentTime.ToLocal(schedule.End, zone));
        }

        [Fact]
        public void ToUtc_MovesATimeTheClocksSkip()
        {
            var zone = AppointmentTime.Zone("Europe/Berlin");

            // 29 March 2026, 02:30 does not exist in Berlin; it is 03:30 summer time = 01:30 UTC
            Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc), AppointmentTime.ToUtc(new DateTime(2026, 3, 29, 2, 30, 0), zone));
        }
    }
}
