using FluentAssertions;
using Ghost.Core.Models;
using Xunit;

namespace Ghost.Core.Tests.Models;

public class PhysicalRectTests
{
    [Fact]
    public void Center_ReturnsMidpoint()
    {
        var rect = new PhysicalRect(220, 88, 1180, 34);

        rect.Center.Should().Be(new PhysicalPoint(220 + 590, 88 + 17));
    }

    [Fact]
    public void Contains_PointInside_ReturnsTrue()
    {
        var rect = new PhysicalRect(0, 0, 100, 100);

        rect.Contains(new PhysicalPoint(50, 50)).Should().BeTrue();
    }

    [Fact]
    public void Contains_PointOutside_ReturnsFalse()
    {
        var rect = new PhysicalRect(0, 0, 100, 100);

        rect.Contains(new PhysicalPoint(150, 50)).Should().BeFalse();
    }

    [Fact]
    public void DistanceTo_PointInside_ReturnsZero()
    {
        var rect = new PhysicalRect(0, 0, 100, 100);

        rect.DistanceTo(new PhysicalPoint(50, 50)).Should().Be(0);
    }

    [Fact]
    public void DistanceTo_PointOutsideOnAxis_ReturnsEdgeDistance()
    {
        var rect = new PhysicalRect(0, 0, 100, 100);

        rect.DistanceTo(new PhysicalPoint(140, 50)).Should().Be(40);
    }

    [Fact]
    public void IsDegenerate_ZeroWidth_ReturnsTrue()
    {
        var rect = new PhysicalRect(0, 0, 0, 20);

        rect.IsDegenerate.Should().BeTrue();
    }

    [Fact]
    public void NegativeOrigin_SupportsVirtualDesktopCoordinates()
    {
        var rect = new PhysicalRect(-1850, -120, 220, 34);

        rect.Right.Should().Be(-1630);
        rect.IsDegenerate.Should().BeFalse();
    }
}
