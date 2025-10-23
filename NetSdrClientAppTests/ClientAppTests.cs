﻿using NUnit.Framework;
using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class NetSdrClientFullCoverageTests
    {
        private Mock<ITcpClient> _mockTcpClient;
        private Mock<IUdpClient> _mockUdpClient;
        private NetSdrClient _netSdrClient;
        private StringWriter _consoleOutput;
        private const string TestFilePath = "samples.bin";

        [SetUp]
        public void Setup()
        {
            _mockTcpClient = new Mock<ITcpClient>();
            _mockUdpClient = new Mock<IUdpClient>();

            _mockTcpClient.Setup(x => x.Connected).Returns(true);

            // Автоматично симулюємо відповідь при SendMessageAsync
            _mockTcpClient.Setup(x => x.SendMessageAsync(It.IsAny<byte[]>()))
                .Callback<byte[]>(msg =>
                {
                    // Симулюємо отримання відповіді
                    var response = new byte[] { 0x01, 0x02 };
                    _mockTcpClient.Raise(x => x.MessageReceived += null, this, response);
                })
                .Returns(Task.CompletedTask);

            _netSdrClient = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            _consoleOutput = new StringWriter();
            Console.SetOut(_consoleOutput);

            if (File.Exists(TestFilePath))
            {
                File.Delete(TestFilePath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            _consoleOutput?.Dispose();

            var standardOutput = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(standardOutput);

            if (File.Exists(TestFilePath))
            {
                File.Delete(TestFilePath);
            }
        }

        #region ChangeFrequencyAsync Tests

        [Test]
        public async Task ChangeFrequencyAsync_ValidFrequencyAndChannel_SendsCorrectMessage()
        {
            // Arrange
            long frequency = 100000000;
            int channel = 1;

            // Act
            await _netSdrClient.ChangeFrequencyAsync(frequency, channel);

            // Assert
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Once);
        }

        [Test]
        public async Task ChangeFrequencyAsync_FrequencyZero_SendsMessage()
        {
            // Arrange & Act
            await _netSdrClient.ChangeFrequencyAsync(0, 0);

            // Assert
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Once);
        }

        [Test]
        public async Task ChangeFrequencyAsync_MaxFrequency_SendsMessage()
        {
            // Arrange & Act
            await _netSdrClient.ChangeFrequencyAsync(long.MaxValue, 255);

            // Assert
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Once);
        }

        [Test]
        public async Task ChangeFrequencyAsync_NegativeFrequency_SendsMessage()
        {
            // Arrange & Act
            await _netSdrClient.ChangeFrequencyAsync(-1000000, 1);

            // Assert
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Once);
        }

        [Test]
        public async Task ChangeFrequencyAsync_DifferentChannel_SendsMessage()
        {
            // Arrange & Act
            await _netSdrClient.ChangeFrequencyAsync(50000000, 128);

            // Assert
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Once);
        }

        [Test]
        public async Task ChangeFrequencyAsync_VerifyByteConversion_CallsSendMessage()
        {
            // Arrange & Act
            await _netSdrClient.ChangeFrequencyAsync(123456789, 1);

            // Assert
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Once);
        }

        [Test]
        public async Task ChangeFrequencyAsync_WhenNotConnected_PrintsMessage()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(false);

            // Act
            await _netSdrClient.ChangeFrequencyAsync(10000000, 1);

            // Assert
            var output = _consoleOutput.ToString();
            Assert.IsTrue(output.Contains("No active connection"));
        }

        [Test]
        public async Task ChangeFrequencyAsync_MultipleCalls_EachSendsMessage()
        {
            // Arrange & Act
            await _netSdrClient.ChangeFrequencyAsync(1000000, 1);
            await _netSdrClient.ChangeFrequencyAsync(2000000, 1);

            // Assert
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(2));
        }

        #endregion

        #region _udpClient_MessageReceived Tests

        [Test]
        public void MessageReceived_ValidMessage_WritesToFile()
        {
            // Arrange
            var testMessage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };

            // Act
            _mockUdpClient.Raise(x => x.MessageReceived += null, this, testMessage);

            // Assert
            Assert.IsTrue(File.Exists(TestFilePath));
            var fileContent = File.ReadAllBytes(TestFilePath);
            Assert.IsTrue(fileContent.Length > 0);
        }


        [Test]
        public void MessageReceived_LargeMessage_WritesAllSamples()
        {
            // Arrange
            var testMessage = new byte[256];
            for (int i = 0; i < testMessage.Length; i++)
            {
                testMessage[i] = (byte)(i % 256);
            }

            // Act
            _mockUdpClient.Raise(x => x.MessageReceived += null, this, testMessage);

            // Assert
            Assert.IsTrue(File.Exists(TestFilePath));
        }

       

        [Test]
        public void MessageReceived_GetSamplesCallWithBitDepth16_ProcessesCorrectly()
        {
            // Arrange
            var testMessage = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 };

            // Act
            _mockUdpClient.Raise(x => x.MessageReceived += null, this, testMessage);

            // Assert
            var output = _consoleOutput.ToString();
            Assert.IsTrue(output.Contains("Samples recieved:"));
        }

 

        [Test]
        public void MessageReceived_WithVeryLargeBody_HandlesCorrectly()
        {
            // Arrange
            var largeMessage = new byte[10000];
            new Random().NextBytes(largeMessage);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _mockUdpClient.Raise(x => x.MessageReceived += null, this, largeMessage);
            });
        }

       
        #endregion

        #region TcpClient MessageReceived Tests

        [Test]
        public void TcpMessageReceived_PrintsResponse()
        {
            // Arrange
            var response = new byte[] { 0x09, 0x00, 0xb8, 0x00, 0xa0, 0x86, 0x01, 0x00, 0x00 };

            // Act
            _mockTcpClient.Raise(x => x.MessageReceived += null, this, response);

            // Assert
            var output = _consoleOutput.ToString();
            Assert.IsTrue(output.Contains("Response recieved:"));
        }

        [Test]
        public void TcpMessageReceived_WithDifferentData_PrintsHex()
        {
            // Arrange
            var response = new byte[] { 0xFF, 0xEE, 0xDD };

            // Act
            _mockTcpClient.Raise(x => x.MessageReceived += null, this, response);

            // Assert
            var output = _consoleOutput.ToString();
            Assert.IsTrue(output.Contains("ff") || output.Contains("FF"));
        }

        #endregion

        #region Integration Tests

        #endregion
    }
}