using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Time;
using StudyHub.Domain.Entities;
using StudyHub.IntegrationTests.Infrastructure;

namespace StudyHub.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class QuotaTests
{
    private static readonly BusinessCalendar Calendar =
        new(TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh"));

    private readonly IntegrationTestFixture _fixture;

    public QuotaTests(IntegrationTestFixture fixture) => _fixture = fixture;

    private static async Task<Guid> CreateNoteAsync(TestUser user)
    {
        var response = await user.Client.PostAsJsonAsync("/api/notes", new
        {
            title = "A note to summarize",
            content = "Read chapter 7, write the summary, and solve the exercises on joins.",
            parentItemId = (Guid?)null,
            courseId = (Guid?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.ReadJsonAsync()).GetProperty("noteId").GetGuid();
    }

    private async Task<int> UsageRowCountAsync(Guid userId) =>
        await _fixture.Factory.InScopeAsync(context =>
            context.AiUsageLogs.CountAsync(l => l.UserId == userId));

    private static async Task<int> TokensUsedThisMonthAsync(TestUser user)
    {
        var me = await user.Client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await me.ReadJsonAsync()).GetProperty("tokensUsedThisMonth").GetInt32();
    }

    [Fact]
    public async Task Me_UsageBeforeTheRiyadhMonthStart_ShouldNotCount()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();
        var monthStart = Calendar.MonthStartUtc(DateTime.UtcNow);

        await _fixture.Factory.InScopeAsync(async context =>
        {
            // CreatedAt is stamped by the entity, so the two instants are pushed through
            // the change tracker — never through SQL, which would bypass the model
            var before = AiUsageLog.Create(user.Id, "ItBefore", 1000);
            context.Add(before);
            context.BackdateCreatedAt(before, monthStart.AddHours(-1));

            var after = AiUsageLog.Create(user.Id, "ItAfter", 2000);
            context.Add(after);
            context.BackdateCreatedAt(after, monthStart.AddHours(1));

            await context.SaveChangesAsync();
        });

        // Act
        var used = await TokensUsedThisMonthAsync(user);

        // Assert
        // Only the row inside the month counts, and "the month" begins at Riyadh midnight,
        // not at UTC midnight three hours later (ADR-39, ADR-40)
        used.Should().Be(2000);
    }

    [Fact]
    public async Task Summarize_MonthNearlySpent_ShouldReturn429AndRecordNothing()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();
        var noteId = await CreateNoteAsync(user);

        var quota = await _fixture.Factory.InScopeAsync(context =>
            context.Users.Where(u => u.Id == user.Id).Select(u => u.MonthlyTokenQuota).SingleAsync());

        await _fixture.Factory.InScopeAsync(async context =>
        {
            // Everything but 5 tokens of the month is spent: no estimate can fit
            context.Add(AiUsageLog.Create(user.Id, "ItFill", quota - 5));
            await context.SaveChangesAsync();
        });

        var rowsBefore = await UsageRowCountAsync(user.Id);

        // Act
        var response = await user.Client.PostAsJsonAsync($"/api/notes/{noteId}/summarize", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // The check is before the call, so nothing was provided and nothing is charged
        (await UsageRowCountAsync(user.Id)).Should().Be(rowsBefore);
    }

    [Fact]
    public async Task Summarize_TwoParallelCalls_ShouldRecordBoth()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();
        var noteId = await CreateNoteAsync(user);

        // Act
        var first = user.Client.PostAsJsonAsync($"/api/notes/{noteId}/summarize", new { });
        var second = user.Client.PostAsJsonAsync($"/api/notes/{noteId}/summarize", new { });

        var responses = await Task.WhenAll(first, second);

        // Assert
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        var tokens = new List<int>();
        foreach (var response in responses)
            tokens.Add((await response.ReadJsonAsync()).GetProperty("tokensUsed").GetInt32());

        // Recording is one insert per call, so two parallel calls can neither lose one
        // another nor collide — the whole reason the counter was removed in M8.1 (ADR-39)
        (await UsageRowCountAsync(user.Id)).Should().Be(2);
        (await TokensUsedThisMonthAsync(user)).Should().Be(tokens.Sum());
    }
}
