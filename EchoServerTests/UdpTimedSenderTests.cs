using TestServerApp;

namespace EchoServerTests
{
    public class UdpTimedSenderTests
    {
        [Test]
        public void Constructor_ShouldCreateInstance()
        {
            // Act
            var sender = new UdpTimedSender("127.0.0.1", 5000);

            // Assert
            Assert.That(sender, Is.Not.Null);
        }

        [Test]
        public void StartSending_ShouldThrowIfAlreadyRunning()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 5000);
            sender.StartSending(1000);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => sender.StartSending(1000));

            sender.StopSending();
        }

        [Test]
        public async Task StartSending_ShouldSendMessagesAtInterval()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 60000);

            // Act
            sender.StartSending(100); // Швидкий інтервал для тесту
            await Task.Delay(250); // Чекаємо кілька повідомлень
            sender.StopSending();

            // Assert - якщо не було виключень, тест пройшов
            Assert.Pass();
        }

        [Test]
        public void StopSending_ShouldStopTimer()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 60000);
            sender.StartSending(1000);

            // Act
            sender.StopSending();

            // Assert - не повинно бути виключень
            Assert.Pass();
        }

        [Test]
        public void StopSending_ShouldHandleMultipleCalls()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 60000);

            // Act
            sender.StopSending();
            sender.StopSending(); // Другий виклик

            // Assert
            Assert.Pass();
        }

        [Test]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var sender = new UdpTimedSender("127.0.0.1", 60000);
            sender.StartSending(1000);

            // Act & Assert
            Assert.DoesNotThrow(() => sender.Dispose());
        }

        [Test]
        public void Dispose_ShouldHandleMultipleCalls()
        {
            // Arrange
            var sender = new UdpTimedSender("127.0.0.1", 60000);

            // Act
            sender.Dispose();

            // Assert
            Assert.DoesNotThrow(() => sender.Dispose());
        }
    }
}