using FluentAssertions;
using StudyHub.Domain.Entities;
using StudyHub.Domain.Enums;

namespace StudyHub.Domain.Tests;

public class ItemTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    // يبني سلسلة متداخلة ويرجع أعمق عقدة فيها
    private static Item BuildChain(int depth)
    {
        var node = Note.Create(OwnerId, "level 0");

        for (var i = 1; i <= depth; i++)
            node = Note.Create(OwnerId, $"level {i}", parent: node);

        return node;
    }

    [Fact]
    public void Create_WithEmptyTitle_ShouldThrowArgumentException()
    {
        var act = () => Note.Create(OwnerId, "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithoutParent_ShouldBeRootAtDepthZero()
    {
        var note = Note.Create(OwnerId, "Root");

        note.ParentItemId.Should().BeNull();
        note.Depth.Should().Be(0);
    }

    [Fact]
    public void Create_WithParent_ShouldIncreaseDepthByOne()
    {
        var parent = Note.Create(OwnerId, "Parent");

        var child = Note.Create(OwnerId, "Child", parent: parent);

        child.Depth.Should().Be(1);
        child.ParentItemId.Should().Be(parent.Id);
    }

    [Fact]
    public void Create_WithParent_ShouldInheritCourseIdAndIgnoreTheGivenOne()
    {
        var courseId = Guid.NewGuid();
        var parent = Note.Create(OwnerId, "Parent", courseId: courseId);

        // نمرّر كورسًا آخر عمدًا — يجب أن يُتجاهل لصالح كورس الأب
        var child = Note.Create(OwnerId, "Child", parent: parent, courseId: Guid.NewGuid());

        child.CourseId.Should().Be(courseId);
    }

    [Fact]
    public void Create_UnderParentOfAnotherUser_ShouldThrow()
    {
        var strangersNote = Note.Create(StrangerId, "Not mine");

        var act = () => Note.Create(OwnerId, "Child", parent: strangersNote);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_UnderDeletedParent_ShouldThrow()
    {
        var parent = Note.Create(OwnerId, "Parent");
        parent.MarkAsDeleted();

        var act = () => Note.Create(OwnerId, "Child", parent: parent);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_AtMaximumDepth_ShouldSucceed()
    {
        var deepest = BuildChain(Item.MaxDepth);

        deepest.Depth.Should().Be(Item.MaxDepth);
    }

    [Fact]
    public void Create_BeyondMaximumDepth_ShouldThrow()
    {
        var deepest = BuildChain(Item.MaxDepth);

        var act = () => Note.Create(OwnerId, "One too deep", parent: deepest);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_TaskUnderNote_ShouldSucceed()
    {
        var note = Note.Create(OwnerId, "Lecture notes");

        var task = TaskItem.Create(OwnerId, "Review chapter 3", parent: note);

        task.ParentItemId.Should().Be(note.Id);
        task.Depth.Should().Be(1);
    }

    [Fact]
    public void Create_NoteUnderTask_ShouldSucceed()
    {
        var task = TaskItem.Create(OwnerId, "Build the parser");

        var note = Note.Create(OwnerId, "Ideas for the parser", parent: task);

        note.ParentItemId.Should().Be(task.Id);
    }

    [Fact]
    public void TaskItem_Create_ShouldStartAsPending()
    {
        var task = TaskItem.Create(OwnerId, "New task");

        task.Status.Should().Be(StudyTaskStatus.Pending);
        task.Priority.Should().Be(TaskPriority.Medium);
    }
    [Fact]
    public void IsAtMaxDepth_AtDepthFour_ShouldBeTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var d0 = Note.Create(userId, "Depth 0");
        var d1 = Note.Create(userId, "Depth 1", parent: d0);
        var d2 = Note.Create(userId, "Depth 2", parent: d1);
        var d3 = Note.Create(userId, "Depth 3", parent: d2);
        var d4 = Note.Create(userId, "Depth 4", parent: d3);

        // Act
        var result = d4.IsAtMaxDepth;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAtMaxDepth_BelowMaximum_ShouldBeFalse()
    {
        // Arrange
        var root = Note.Create(Guid.NewGuid(), "Depth 0");

        // Act
        var result = root.IsAtMaxDepth;

        // Assert
        result.Should().BeFalse();
    }
}