using Moq;

namespace MediatR.Extensions.System.Threading.Tasks.Tests
{
    [TestFixture]
    public class MediatRExtensionsTests
    {
        private Mock<IMediator> _mediatorMock;

        [SetUp]
        public void SetUp()
        {
            _mediatorMock = new Mock<IMediator>();
        }

        [Test]
        public async Task SendAll_WithMultipleRequests_ReturnsAllResults()
        {
            // Arrange
            var requests = new[]
            {
                new TestRequest { Value = 1 },
                new TestRequest { Value = 2 },
                new TestRequest { Value = 3 }
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestRequest req, CancellationToken _) => new TestResponse { Result = req.Value * 2 });

            // Act
            var results = await _mediatorMock.Object.SendAll(requests);

            // Assert
            Assert.That(results, Has.Length.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(results[0].Result, Is.EqualTo(2));
                Assert.That(results[1].Result, Is.EqualTo(4));
                Assert.That(results[2].Result, Is.EqualTo(6));
            });
        }

        [Test]
        public async Task SendAll_WithEmptyRequests_ReturnsEmptyArray()
        {
            // Arrange
            var requests = Array.Empty<TestRequest>();

            // Act
            var results = await _mediatorMock.Object.SendAll(requests);

            // Assert
            Assert.That(results, Is.Empty);
        }

        [Test]
        public async Task SendAll_WithCancellationToken_PassesTokenToMediator()
        {
            // Arrange
            var requests = new[] { new TestRequest { Value = 1 } };
            var cts = new CancellationTokenSource();

            _mediatorMock.Setup(m => m.Send(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TestResponse { Result = 1 });

            // Act
            await _mediatorMock.Object.SendAll(requests, cts.Token);

            // Assert
            _mediatorMock.Verify(m => m.Send(It.IsAny<TestRequest>(), cts.Token), Times.Once);
        }

        [Test]
        public async Task SendAll_WithMaxDegreeOfParallelism_ReturnsAllResults()
        {
            // Arrange
            var requests = new[]
            {
                new TestRequest { Value = 1 },
                new TestRequest { Value = 2 },
                new TestRequest { Value = 3 },
                new TestRequest { Value = 4 },
                new TestRequest { Value = 5 }
            };

            _mediatorMock.Setup(m => m.Send(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestRequest req, CancellationToken _) => new TestResponse { Result = req.Value * 2 });

            // Act
            var results = await _mediatorMock.Object.SendAll(requests, maxDegreeOfParallelism: 2);

            // Assert
            Assert.That(results, Has.Length.EqualTo(5));
            Assert.That(results.Select(r => r.Result), Is.EquivalentTo(new[] { 2, 4, 6, 8, 10 }));
        }

        [Test]
        public async Task SendAll_WithMaxDegreeOfParallelism_LimitsParallelExecution()
        {
            // Arrange
            var concurrentExecutions = 0;
            var maxConcurrentExecutions = 0;
            var requests = Enumerable.Range(1, 10).Select(i => new TestRequest { Value = i }).ToArray();

            _mediatorMock.Setup(m => m.Send(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async (TestRequest req, CancellationToken _) =>
                {
                    Interlocked.Increment(ref concurrentExecutions);
                    var current = concurrentExecutions;
                    if (current > maxConcurrentExecutions)
                    {
                        maxConcurrentExecutions = current;
                    }
                    await Task.Delay(10);
                    Interlocked.Decrement(ref concurrentExecutions);
                    return new TestResponse { Result = req.Value };
                });

            // Act
            await _mediatorMock.Object.SendAll(requests, maxDegreeOfParallelism: 3);

            // Assert
            Assert.That(maxConcurrentExecutions, Is.LessThanOrEqualTo(3));
        }

        [Test]
        public async Task SendAsCompleted_ReturnsResultsAsTheyComplete()
        {
            // Arrange
            var requests = new[]
            {
                new TestRequest { Value = 1 },
                new TestRequest { Value = 2 },
                new TestRequest { Value = 3 }
            };

            _mediatorMock.Setup(m => m.Send(It.Is<TestRequest>(r => r.Value == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TestResponse { Result = 1 }, TimeSpan.FromMilliseconds(100));
            _mediatorMock.Setup(m => m.Send(It.Is<TestRequest>(r => r.Value == 2), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TestResponse { Result = 2 }, TimeSpan.FromMilliseconds(50));
            _mediatorMock.Setup(m => m.Send(It.Is<TestRequest>(r => r.Value == 3), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TestResponse { Result = 3 }, TimeSpan.FromMilliseconds(10));

            // Act
            var results = new List<TestResponse>();
            await foreach (var result in _mediatorMock.Object.SendAsCompleted(requests))
            {
                results.Add(result);
            }

            // Assert
            Assert.That(results, Has.Count.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(results[0].Result, Is.EqualTo(3));
                Assert.That(results[1].Result, Is.EqualTo(2));
                Assert.That(results[2].Result, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task SendAsCompleted_WithCancellation_StopsEnumeration()
        {
            // Arrange
            var requests = new[]
            {
                new TestRequest { Value = 1 },
                new TestRequest { Value = 2 }
            };

            var cts = new CancellationTokenSource();
            _mediatorMock.Setup(m => m.Send(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestRequest req, CancellationToken ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return new TestResponse { Result = req.Value };
                });

            // Act & Assert
            cts.Cancel();
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var result in _mediatorMock.Object.SendAsCompleted(requests, cts.Token))
                {
                    // Should not get here
                }
            });
        }

        [Test]
        public async Task PublishAll_WithMultipleNotifications_PublishesAll()
        {
            // Arrange
            var notifications = new[]
            {
                new TestNotification { Message = "Message 1" },
                new TestNotification { Message = "Message 2" },
                new TestNotification { Message = "Message 3" }
            };

            _mediatorMock.Setup(m => m.Publish(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _mediatorMock.Object.PublishAll(notifications);

            // Assert
            _mediatorMock.Verify(m => m.Publish(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Test]
        public async Task PublishAll_WithEmptyNotifications_DoesNothing()
        {
            // Arrange
            var notifications = Array.Empty<TestNotification>();

            // Act
            await _mediatorMock.Object.PublishAll(notifications);

            // Assert
            _mediatorMock.Verify(m => m.Publish(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task PublishAll_WithMaxDegreeOfParallelism_PublishesAll()
        {
            // Arrange
            var notifications = new[]
            {
                new TestNotification { Message = "Message 1" },
                new TestNotification { Message = "Message 2" },
                new TestNotification { Message = "Message 3" },
                new TestNotification { Message = "Message 4" },
                new TestNotification { Message = "Message 5" }
            };

            _mediatorMock.Setup(m => m.Publish(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _mediatorMock.Object.PublishAll(notifications, maxDegreeOfParallelism: 2);

            // Assert
            _mediatorMock.Verify(m => m.Publish(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
        }

        private class TestRequest : IRequest<TestResponse>
        {
            public int Value { get; set; }
        }

        private class TestResponse
        {
            public int Result { get; set; }
        }

        private class TestUnitRequest : IRequest
        {
            public int Value { get; set; }
        }

        private class TestNotification : INotification
        {
            public string Message { get; set; } = string.Empty;
        }
    }
}
