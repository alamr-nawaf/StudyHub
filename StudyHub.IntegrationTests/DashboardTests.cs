using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using StudyHub.Application.Common.Time;
using StudyHub.Domain.Entities;
using StudyHub.IntegrationTests.Infrastructure;

namespace StudyHub.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class DashboardTests
{
    // The application's own calendar on the configured zone: the test asks the same
    // question the API does rather than re-implementing Riyadh midnight (ADR-40)
    private static readonly BusinessCalendar Calendar =
        new(TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh"));

    private readonly IntegrationTestFixture _fixture;

    public DashboardTests(IntegrationTestFixture fixture) => _fixture = fixture;

    private static async Task<Guid> CreateCourseAsync(TestUser user, string title)
    {
        var response = await user.Client.PostAsJsonAsync("/api/courses",
            new { title, description = (string?)null });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.ReadJsonAsync()).GetProperty("courseId").GetGuid();
    }

    private static async Task<Guid> CreateTaskAsync(
        TestUser user, string title, Guid? courseId = null, DateTime? dueDateUtc = null)
    {
        var response = await user.Client.PostAsJsonAsync("/api/tasks", new
        {
            title,
            content = (string?)null,
            parentItemId = (Guid?)null,
            courseId,
            priority = 1,
            dueDate = dueDateUtc?.Iso()
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.ReadJsonAsync()).GetProperty("taskId").GetGuid();
    }

    private static async Task<JsonElement> GetDashboardAsync(TestUser user)
    {
        var response = await user.Client.GetAsync("/api/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.ReadJsonAsync();
    }

    private static IEnumerable<string> Titles(JsonElement dashboard, string part) =>
        dashboard.GetProperty(part).EnumerateArray().Select(e => e.GetProperty("title").GetString()!);

    [Fact]
    public async Task Dashboard_LittleAndMuchData_ShouldIssueThreeStatementsBoth()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();
        var courseId = await CreateCourseAsync(user, "Statement budget");
        await CreateTaskAsync(user, "One task", courseId, DateTime.UtcNow.AddDays(1));

        // Act
        _fixture.StatementCounter.Reset();
        await GetDashboardAsync(user);
        var withLittleData = _fixture.StatementCounter.Count;

        await _fixture.Factory.InScopeAsync(async context =>
        {
            // 200 more items through the domain's factory methods, not SQL
            for (var i = 0; i < 200; i++)
            {
                context.Add(i % 2 == 0
                    ? Note.Create(user.Id, $"Bulk note {i}", "body", null, courseId)
                    : TaskItem.Create(user.Id, $"Bulk task {i}", null, null, courseId,
                        dueDate: DateTime.UtcNow.AddDays(2)));
            }

            await context.SaveChangesAsync();
        });

        _fixture.StatementCounter.Reset();
        var dashboard = await GetDashboardAsync(user);
        var withMuchData = _fixture.StatementCounter.Count;

        // Assert
        withLittleData.Should().Be(3);

        // The whole point of ADR-42: the cost of the endpoint does not grow with the data
        withMuchData.Should().Be(withLittleData);

        // And the extra rows really are there, so the two measurements are not of the same
        // empty dashboard
        dashboard.GetProperty("counts").GetProperty("tasks").GetInt32().Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task Dashboard_RecentCourses_ShouldFollowLatestActivity()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();

        var c1 = await CreateCourseAsync(user, "C1");
        await CreateCourseAsync(user, "C2");
        await CreateCourseAsync(user, "C3");

        // Act
        await CreateTaskAsync(user, "Inside C1", c1, DateTime.UtcNow.AddDays(1));
        var dashboard = await GetDashboardAsync(user);

        // Assert
        // Working inside a course is what makes it recent, not renaming it (ADR-41): C1 was
        // the oldest course until a task appeared under it
        Titles(dashboard, "recentCourses").Should().Equal("C1", "C3", "C2");
    }

    [Fact]
    public async Task Dashboard_UrgentWindow_ShouldEndAtRiyadhMidnightAfterTheThirdDay()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();

        var windowEnd = Calendar.DayStartUtc(DateTime.UtcNow).AddDays(4);
        await CreateTaskAsync(user, "JustInside", dueDateUtc: windowEnd.AddMinutes(-1));
        await CreateTaskAsync(user, "JustOutside", dueDateUtc: windowEnd.AddMinutes(1));

        // Act
        var dashboard = await GetDashboardAsync(user);

        // Assert
        var generatedAt = dashboard.GetProperty("generatedAt").GetDateTime().ToUniversalTime();
        var expectedEnd = Calendar.DayStartUtc(generatedAt).AddDays(4);

        // The window ends at a Riyadh midnight, four days after the day the response was
        // generated in: today plus three whole days (§15.4)
        TimeZoneInfo.ConvertTimeFromUtc(expectedEnd, TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh"))
            .TimeOfDay.Should().Be(TimeSpan.Zero);

        var urgent = Titles(dashboard, "urgentTasks").ToList();
        urgent.Should().Contain("JustInside");

        // Recomputed from the response's own instant, so a Riyadh midnight passing between
        // the two calls moves the expectation instead of failing the test
        if (windowEnd.AddMinutes(1) < expectedEnd)
            urgent.Should().Contain("JustOutside");
        else
            urgent.Should().NotContain("JustOutside");
    }

    [Fact]
    public async Task Dashboard_DeletedCourse_ShouldLeaveEveryPart()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();

        var kept = await CreateCourseAsync(user, "Kept");
        var doomed = await CreateCourseAsync(user, "Doomed");
        await CreateTaskAsync(user, "KeptTask", kept, DateTime.UtcNow.AddDays(1));
        await CreateTaskAsync(user, "DoomedTask", doomed, DateTime.UtcNow.AddDays(1));

        var before = await GetDashboardAsync(user);
        before.GetProperty("counts").GetProperty("courses").GetInt32().Should().Be(2);
        before.GetProperty("counts").GetProperty("tasks").GetInt32().Should().Be(2);

        // Act
        var delete = await user.Client.DeleteAsync($"/api/courses/{doomed}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await GetDashboardAsync(user);

        // Assert
        var counts = after.GetProperty("counts");
        counts.GetProperty("courses").GetInt32().Should().Be(1);
        counts.GetProperty("tasks").GetInt32().Should().Be(1);
        counts.GetProperty("pendingTasks").GetInt32().Should().Be(1);

        // The same filter has to reach all three parts of the response, not only the counts
        Titles(after, "urgentTasks").Should().Equal("KeptTask");
        Titles(after, "recentCourses").Should().Equal("Kept");
    }
}
