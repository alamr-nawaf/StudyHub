using FluentAssertions;
using StudyHub.Domain.Entities;
using Xunit;

namespace StudyHub.Domain.Tests;

public class CourseTests
{
    [Fact]
    public void Create_WithEmptyTitle_ShouldThrowArgumentException()
    {
        var act = () => Course.Create(Guid.NewGuid(), title: "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithValidTitle_ShouldSucceed()
    {
        var course = Course.Create(Guid.NewGuid(), "OOP 101");

        course.Title.Should().Be("OOP 101");
        course.IsDeleted.Should().BeFalse();
    }
}