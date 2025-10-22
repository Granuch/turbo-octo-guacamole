using Moq;
using System.Net;
using System.Net.Sockets;
using TestServerApp;

namespace EchoServerTests
{
    public class EchoServerTests
    {
        private Mock<ITcpListenerWrapper> _mockListener;
        private Mock<ITcpClientWrapper> _mockClient;
        private Mock<INetworkStreamWrapper> _mockStream;
        private EchoServer? _server;
        private CancellationTokenSource _cts;

        [SetUp]
        public void Setup()
        {
            _mockListener = new Mock<ITcpListenerWrapper>();
            _mockClient = new Mock<ITcpClientWrapper>();
            _mockStream = new Mock<INetworkStreamWrapper>();
            _cts = new CancellationTokenSource();

            _mockClient.Setup(c => c.GetStream()).Returns(_mockStream.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _cts?.Dispose();
        }

        [Test]
        public async Task StartAsync_ShouldStartListenerAndAcceptClients()
        {
            // Arrange
            var mockTcpClient = new Mock<TcpClient>();
            _mockListener.Setup(l => l.Start());
            _mockListener
                .SetupSequence(l => l.AcceptTcpClientAsync())
                .ReturnsAsync(mockTcpClient.Object)
                .ThrowsAsync(new ObjectDisposedException("Listener stopped"));

            _server = new EchoServer(
                5000,
                (addr, port) => _mockListener.Object,
                (client) => _mockClient.Object);

            // Act
            var startTask = Task.Run(() => _server.StartAsync());
            await Task.Delay(100); // Дати час на старт
            _server.Stop();
            await startTask;

            // Assert
            _mockListener.Verify(l => l.Start(), Times.Once);
            _mockListener.Verify(l => l.AcceptTcpClientAsync(), Times.AtLeastOnce);
            _mockListener.Verify(l => l.Stop(), Times.Once);
        }

        [Test]
        public async Task HandleClientAsync_ShouldStopWhenCancellationRequested()
        {
            // Arrange
            _mockStream
                .Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);

            _server = new EchoServer(
                5000,
                (addr, port) => _mockListener.Object,
                (client) => _mockClient.Object);

            _cts.CancelAfter(50);

            // Act
            await _server.HandleClientAsync(_mockClient.Object, _cts.Token);

            // Assert
            _mockClient.Verify(c => c.Close(), Times.Once);
        }

        [Test]
        public async Task HandleClientAsync_ShouldHandleExceptionGracefully()
        {
            // Arrange
            _mockStream
                .Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Network error"));

            _server = new EchoServer(
                5000,
                (addr, port) => _mockListener.Object,
                (client) => _mockClient.Object);

            // Act & Assert - не повинно бути виключень
            Assert.DoesNotThrowAsync(async () =>
                await _server.HandleClientAsync(_mockClient.Object, _cts.Token));

            _mockClient.Verify(c => c.Close(), Times.Once);
        }

        [Test]
        public void Stop_ShouldCancelAndDisposeResources()
        {
            // Arrange
            _server = new EchoServer(
                5000,
                (addr, port) => _mockListener.Object,
                (client) => _mockClient.Object);

            var startTask = Task.Run(() => _server.StartAsync());
            Thread.Sleep(50); // Дати час на старт

            // Act
            _server.Stop();

            // Assert
            _mockListener.Verify(l => l.Stop(), Times.Once);
        }

        [Test]
        public void Stop_ShouldHandleMultipleCalls()
        {
            // Arrange
            _server = new EchoServer(
                5000,
                (addr, port) => _mockListener.Object,
                (client) => _mockClient.Object);

            // Act
            _server.Stop();
            _server.Stop(); // Другий виклик

            // Assert - не повинно бути виключень
            Assert.Pass();
        }

        [Test]
        public void Constructor_ShouldCreateInstanceWithDefaultFactories()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new EchoServer(5000));
        }
    }
}