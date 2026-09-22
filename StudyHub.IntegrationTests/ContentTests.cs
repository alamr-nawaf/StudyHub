using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StudyHub.IntegrationTests.Infrastructure;

namespace StudyHub.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class ContentTests
{
    private readonly IntegrationTestFixture _fixture;

    public ContentTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DeleteCourse_WithNestedItems_ShouldSoftDeleteAll()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();

        var course = await user.Client.PostAsJsonAsync("/api/courses",
            new { title = "Cascade", description = (string?)null });
        var courseId = (await course.ReadJsonAsync()).GetProperty("courseId").GetGuid();

        var note = await user.Client.PostAsJsonAsync("/api/notes",
            new { title = "Note", content = "body", parentItemId = (Guid?)null, courseId });
        var noteId = (await note.ReadJsonAsync()).GetProperty("noteId").GetGuid();

        var task = await user.Client.PostAsJsonAsync("/api/tasks",
            new { title = "Task", content = (string?)null, parentItemId = noteId, courseId = (Guid?)null, priority = 1, dueDate = (string?)null });
        var taskId = (await task.ReadJsonAsync()).GetProperty("taskId").GetGuid();

        // Act
        var delete = await user.Client.DeleteAsync($"/api/courses/{courseId}");

        // Assert
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Gone from the API: the course, and every item under it at any depth
        (await user.Client.GetAsync($"/api/courses/{courseId}/tree")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await user.Client.GetAsync($"/api/items/{noteId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await user.Client.GetAsync($"/api/items/{taskId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        // Still there in the database: soft delete, not deletion (ADR-06). Reading them
        // needs IgnoreQueryFilters, which is exactly what the query filter is for
        await _fixture.Factory.InScopeAsync(async context =>
        {
            var courseRow = await context.Courses.IgnoreQueryFilters()
                .SingleAsync(c => c.Id == courseId);
            var itemRows = await context.Items.IgnoreQueryFilters()
                .Where(i => i.Id == noteId || i.Id == taskId)
                .ToListAsync();

            courseRow.IsDeleted.Should().BeTrue();
            itemRows.Should().HaveCount(2);
            itemRows.Should().OnlyContain(i => i.IsDeleted);
        });
    }
}
