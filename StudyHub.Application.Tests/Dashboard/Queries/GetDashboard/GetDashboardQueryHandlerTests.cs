using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Time;
using StudyHub.Application.Dashboard;
using StudyHub.Application.Dashboard.Queries.GetDashboard;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Tests.Dashboard.Queries.GetDashboard;

public class GetDashboardQueryHandlerTests
{
    // A fixed UTC+3 zone: exact for Riyadh, which observes no daylight saving
    private const int RiyadhOffsetHours = 3;

    private static readonly BusinessCalendar Calendar = new(
        TimeZoneInfo.CreateCustomTimeZone("Test/Riyadh", TimeSpan.FromHours(RiyadhOffsetHours), "Riyadh", "Riyadh"));

    private readonly Mock<IDashboardQueries> _dashboardQueriesMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly GetDashboardQueryHandler _handler;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public GetDashboardQueryHandlerTests()
    {
        _currentUserMock.Setup(c => c.UserId).Returns(_currentUserId);

        _handler = new GetDashboardQueryHandler(
            _dashboardQueriesMock.Object, _currentUserMock.Object, Calendar);
    }

    private static DashboardDto AnyDashboard() => new(
        DateTime.UtcNow,
        new DashboardCountsDto(3, 2, 6, 4, 1, 1, 1),
        Array.Empty<UrgentTaskDto>(),
        Array.Empty<RecentCourseDto>());

    [Fact]
    public async Task Handle_ExistingAccount_ShouldReturnTheDashboardFromTheQueries()
    {
        // Arrange
        var dashboard = new DashboardDto(
            DateTime.UtcNow,
            new DashboardCountsDto(3, 2, 6, 4, 1, 1, 1),
            new[] { new UrgentTaskDto(Guid.NewGuid(), "Finish lab 3", Guid.NewGuid(), StudyTaskStatus.Pending, TaskPriority.High, DateTime.UtcNow.AddDays(1), false) },
            new[] { new RecentCourseDto(Guid.NewGuid(), "Databases", DateTime.UtcNow) });

        _dashboardQueriesMock
            .Setup(q => q.GetAsync(_currentUserId, It.IsAny<DashboardCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        // Act
        var result = await _handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        // Assert
        result.Should().BeSameAs(dashboard);
    }

    [Fact]
    public async Task Handle_MissingAccount_ShouldThrowNotFound()
    {
        // Arrange
        _dashboardQueriesMock
            .Setup(q => q.GetAsync(It.IsAny<Guid>(), It.IsAny<DashboardCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DashboardDto?)null);

        // Act
        var act = () => _handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_Always_ShouldAskForTheCurrentUsersDashboardOnly()
    {
        // Arrange
        _dashboardQueriesMock
            .Setup(q => q.GetAsync(It.IsAny<Guid>(), It.IsAny<DashboardCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyDashboard());

        // Act
        await _handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        // Assert
        _dashboardQueriesMock.Verify(
            q => q.GetAsync(_currentUserId, It.IsAny<DashboardCriteria>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _dashboardQueriesMock.Verify(
            q => q.GetAsync(It.Is<Guid>(id => id != _currentUserId), It.IsAny<DashboardCriteria>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Always_ShouldPassTheRulesOfSection15_4()
    {
        // Arrange
        DashboardCriteria? criteria = null;

        _dashboardQueriesMock
            .Setup(q => q.GetAsync(It.IsAny<Guid>(), It.IsAny<DashboardCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DashboardCriteria, CancellationToken>((_, c, _) => criteria = c)
            .ReturnsAsync(AnyDashboard());

        var before = DateTime.UtcNow;

        // Act
        await _handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        var after = DateTime.UtcNow;

        // Assert
        criteria.Should().NotBeNull();
        criteria!.UtcNow.Kind.Should().Be(DateTimeKind.Utc);
        criteria.UtcNow.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);

        // The expected values are written out rather than recomputed from the handler's
        // own constants or from the calendar: a test that compares a rule with itself
        // proves nothing
        var riyadhNow = criteria.UtcNow.AddHours(RiyadhOffsetHours);
        var riyadhUrgentUntil = criteria.UrgentUntil.AddHours(RiyadhOffsetHours);

        riyadhUrgentUntil.TimeOfDay.Should().Be(TimeSpan.Zero);
        riyadhUrgentUntil.Date.Should().Be(riyadhNow.Date.AddDays(4));
        criteria.UrgentUntil.Kind.Should().Be(DateTimeKind.Utc);

        criteria.MaxUrgentTasks.Should().Be(10);
        criteria.MaxRecentCourses.Should().Be(5);
    }
}
