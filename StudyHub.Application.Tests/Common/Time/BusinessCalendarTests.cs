using FluentAssertions;
using StudyHub.Application.Common.Time;

namespace StudyHub.Application.Tests.Common.Time;

public class BusinessCalendarTests
{
    // Riyadh observes no daylight saving, so a fixed UTC+3 zone is exact and the tests do
    // not depend on the machine's time-zone database
    private static readonly BusinessCalendar Calendar = new(
        TimeZoneInfo.CreateCustomTimeZone("Test/Riyadh", TimeSpan.FromHours(3), "Riyadh", "Riyadh"));

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void MonthStartUtc_MidMonth_ShouldReturnTheFirstDayAtRiyadhMidnight()
    {
        var now = Utc(2026, 9, 19, 12);

        var monthStart = Calendar.MonthStartUtc(now);

        monthStart.Should().Be(Utc(2026, 8, 31, 21));
        monthStart.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void MonthStartUtc_FirstHoursOfAMonthInRiyadh_ShouldBelongToTheNewMonth()
    {
        // 22:00Z on 30 September is already 1 October in Riyadh: the case ADR-40 exists for
        var now = Utc(2026, 9, 30, 22);

        var monthStart = Calendar.MonthStartUtc(now);

        monthStart.Should().Be(Utc(2026, 9, 30, 21));
    }

    [Fact]
    public void MonthStartUtc_JustBeforeRiyadhMidnight_ShouldStayInTheOldMonth()
    {
        var now = Utc(2026, 9, 30, 20, 59);

        var monthStart = Calendar.MonthStartUtc(now);

        monthStart.Should().Be(Utc(2026, 8, 31, 21));
    }

    [Fact]
    public void MonthStartUtc_NewYearInRiyadh_ShouldCrossTheYear()
    {
        var now = Utc(2027, 1, 1, 0, 30);

        var monthStart = Calendar.MonthStartUtc(now);

        monthStart.Should().Be(Utc(2026, 12, 31, 21));
    }

    [Fact]
    public void DayStartUtc_EveningInRiyadh_ShouldReturnThatDaysMidnight()
    {
        var now = Utc(2026, 9, 19, 17);

        var dayStart = Calendar.DayStartUtc(now);

        dayStart.Should().Be(Utc(2026, 9, 18, 21));
        dayStart.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void DayStartUtc_AfterRiyadhMidnight_ShouldAlreadyBeTheNextDay()
    {
        // 22:30Z on 19 September is already 20 September in Riyadh
        var now = Utc(2026, 9, 19, 22, 30);

        var dayStart = Calendar.DayStartUtc(now);

        dayStart.Should().Be(Utc(2026, 9, 19, 21));
    }

    [Fact]
    public void MonthStartUtc_NonUtcInput_ShouldThrow()
    {
        var now = new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Unspecified);

        var act = () => Calendar.MonthStartUtc(now);

        act.Should().Throw<ArgumentException>();
    }
}
