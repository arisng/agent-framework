// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoServer.Tests;

/// <summary>
/// Basic smoke test to verify xUnit test discovery is working.
/// </summary>
public class BasicTests
{
    [Fact]
    public void SimpleTest_ShouldPass()
    {
        // Arrange
        const int expected = 2;

        // Act
        const int result = 1 + 1;

        // Assert
        Assert.Equal(expected, result);
    }
}
