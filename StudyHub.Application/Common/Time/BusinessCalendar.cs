namespace StudyHub.Application.Common.Time;

/// <summary>
/// Answers "which month" — and, from M9, "which day" — in the one configured business
/// time zone (ADR-40). Instants stay UTC everywhere else; only the period boundaries are
/// computed in the zone, so a month begins at Riyadh midnight rather than UTC midnight.
/// It holds no clock: the caller passes the current instant, which keeps it testable.
/// </summary>
public sealed class BusinessCalendar
{
    private readonly TimeZoneInfo _zone;

    public BusinessCalendar(TimeZoneInfo zone)
    {
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
    }

    // The UTC instant at which the business month containing utcNow began,
    // e.g. 1 September 00:00 in Riyadh = 31 August 21:00Z
    public DateTime MonthStartUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The current instant must be a UTC DateTime.", nameof(utcNow));

        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _zone);
        var monthStartLocal = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);

        // ConvertTimeToUtc returns Kind == Utc, which Npgsql requires for a timestamptz
        // parameter (ADR-18)
        return TimeZoneInfo.ConvertTimeToUtc(monthStartLocal, _zone);
    }

    // The UTC instant at which the business day containing utcNow began,
    // e.g. 19 September 00:00 in Riyadh = 18 September 21:00Z
    public DateTime DayStartUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The current instant must be a UTC DateTime.", nameof(utcNow));

        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _zone);

        // Built as Unspecified on purpose: ConvertTimeToUtc refuses a Local instant for a
        // zone that is not the machine's, and midnight in the zone is neither UTC nor local
        var dayStartLocal = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, _zone);
    }
}
