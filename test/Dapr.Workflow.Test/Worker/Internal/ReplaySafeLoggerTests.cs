// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.Workflow.Worker.Internal;
using Microsoft.Extensions.Logging;
using Moq;

namespace Dapr.Workflow.Test.Worker.Internal;

public class ReplaySafeLoggerTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenInnerLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ReplaySafeLogger(null!, () => false));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenIsReplayingFuncIsNull()
    {
        var inner = Mock.Of<ILogger>();
        Assert.Throws<ArgumentNullException>(() => new ReplaySafeLogger(inner, null!));
    }

    [Fact]
    public void IsEnabled_ShouldReturnFalse_WhenReplayingEvenIfInnerIsEnabled()
    {
        var innerMock = new Mock<ILogger>(MockBehavior.Strict);
        innerMock.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);

        var logger = new ReplaySafeLogger(innerMock.Object, () => true);

        Assert.False(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void IsEnabled_ShouldReturnInnerResult_WhenNotReplaying()
    {
        var innerMock = new Mock<ILogger>(MockBehavior.Strict);
        innerMock.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(false);

        var logger = new ReplaySafeLogger(innerMock.Object, () => false);

        Assert.False(logger.IsEnabled(LogLevel.Debug));
    }

    [Fact]
    public void Log_ShouldNotCallInnerLogger_WhenReplaying()
    {
        var innerMock = new Mock<ILogger>(MockBehavior.Strict);

        var logger = new ReplaySafeLogger(innerMock.Object, () => true);

        logger.Log(LogLevel.Information, new EventId(1, "e"), "state", null, (s, _) => s);

        innerMock.Verify(
            x => x.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void Log_ShouldCallInnerLogger_WhenNotReplaying()
    {
        var innerMock = new Mock<ILogger>(MockBehavior.Strict);
        innerMock
            .Setup(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

        var logger = new ReplaySafeLogger(innerMock.Object, () => false);

        logger.Log(LogLevel.Warning, new EventId(2, "warn"), "state", null, (s, _) => s);

        innerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
