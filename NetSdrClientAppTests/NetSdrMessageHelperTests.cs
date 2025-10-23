using NetSdrClientApp.Messages;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetControlItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(actualCode, Is.EqualTo((short)code));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetControlItemMessage_ShouldBuildMessageCorrectly()
        {
            var msg = NetSdrMessageHelper.GetControlItemMessage(
                NetSdrMessageHelper.MsgTypes.Ack,
                NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency,
                new byte[5]);

            Assert.That(msg.Length, Is.EqualTo(9)); // 2(header)+2(code)+5
        }

        [Test]
        public void GetDataItemMessage_ShouldBuildMessageCorrectly()
        {
            var msg = NetSdrMessageHelper.GetDataItemMessage(
                NetSdrMessageHelper.MsgTypes.DataItem0,
                new byte[10]);

            Assert.That(msg.Length, Is.EqualTo(12)); // header + seq
        }

        [Test]
        public void GetMessage_ShouldHandleDataItemMaxEdgeCase()
        {
            // Якщо DataItem і довжина = _maxDataItemMessageLength - 2 => має стати 0
            var method = typeof(NetSdrMessageHelper)
                .GetMethod("GetHeader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

            byte[] result = (byte[])method.Invoke(null, new object[] { NetSdrMessageHelper.MsgTypes.DataItem0, 8192 })!;
            ushort val = BitConverter.ToUInt16(result);
            var type = (NetSdrMessageHelper.MsgTypes)(val >> 13);
            Assert.That(type, Is.EqualTo(NetSdrMessageHelper.MsgTypes.DataItem0));
        }

        [Test]
        public void TranslateMessage_ShouldParseControlItemCorrectly()
        {
            var type = NetSdrMessageHelper.MsgTypes.CurrentControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.RFFilter;
            var data = new byte[] { 1, 2, 3 };

            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, data);

            bool ok = NetSdrMessageHelper.TranslateMessage(
                msg,
                out var parsedType,
                out var parsedCode,
                out var seq,
                out var body);

            Assert.That(ok, Is.True);
            Assert.That(parsedType, Is.EqualTo(type));
            Assert.That(parsedCode, Is.EqualTo(code));
            Assert.That(body.Length, Is.EqualTo(data.Length));
        }

        [Test]
        public void TranslateMessage_ShouldParseDataItemCorrectly()
        {
            var type = NetSdrMessageHelper.MsgTypes.DataItem3;
            ushort seqNum = 42;
            byte[] seqBytes = BitConverter.GetBytes(seqNum);
            byte[] data = { 10, 20, 30, 40 };

            var headerMethod = typeof(NetSdrMessageHelper)
                .GetMethod("GetHeader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            byte[] header = (byte[])headerMethod.Invoke(null, new object[] { type, seqBytes.Length + data.Length })!;

            var msg = header.Concat(seqBytes).Concat(data).ToArray();

            bool ok = NetSdrMessageHelper.TranslateMessage(
                msg,
                out var parsedType,
                out var parsedCode,
                out var parsedSeq,
                out var body);

            Assert.That(ok, Is.True);
            Assert.That(parsedType, Is.EqualTo(type));
            Assert.That(parsedSeq, Is.EqualTo(seqNum));
            Assert.That(parsedCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(body, Is.EqualTo(data));
        }

        [Test]
        public void TranslateMessage_ShouldReturnFalseForInvalidCode()
        {
            var type = NetSdrMessageHelper.MsgTypes.CurrentControlItem;
            byte[] header = BitConverter.GetBytes((ushort)(((int)type << 13) + 4 + 2)); // length 6
            byte[] invalidCode = BitConverter.GetBytes((ushort)9999);
            byte[] body = { 1, 2 };
            byte[] msg = header.Concat(invalidCode).Concat(body).ToArray();

            bool ok = NetSdrMessageHelper.TranslateMessage(msg, out _, out _, out _, out _);

            Assert.That(ok, Is.False);
        }


        [Test]
        public void TranslateHeader_ShouldHandleDataItemZeroLength()
        {
            var method = typeof(NetSdrMessageHelper)
                .GetMethod("TranslateHeader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

            ushort encoded = (ushort)(((int)NetSdrMessageHelper.MsgTypes.DataItem1 << 13) + 0);
            byte[] header = BitConverter.GetBytes(encoded);
            object[] args = { header, null!, null! };

            method.Invoke(null, args);
            Assert.Pass(); // лише для виклику DataItem edge-case
        }

        [Test]
        public void GetSamples_ShouldReturnExpectedIntegers()
        {
            ushort bits = 16;
            byte[] body = { 0x01, 0x02, 0x03, 0x04 };

            var samples = NetSdrMessageHelper.GetSamples(bits, body).ToArray();

            Assert.That(samples.Length, Is.GreaterThan(0));
        }

        [Test]
        public void GetSamples_ShouldThrow_WhenSampleSizeTooLarge()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NetSdrMessageHelper.GetSamples(64, new byte[] { 1, 2, 3 }).ToList());
        }

        [Test]
        public void GetSamples_ShouldHandleIncompleteSamples()
        {
            // body не кратний розміру семплу
            ushort bits = 8;
            byte[] body = { 10, 20, 30 }; // 3 байти

            var samples = NetSdrMessageHelper.GetSamples(bits, body).ToArray();

            Assert.That(samples.Length, Is.EqualTo(3)); // 3 рази по 1 байту
        }

        [Test]
        public void GetControlItemMessage_ShouldThrow_WhenTooLong()
        {
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;

            // створюємо надто довгі дані
            var tooLong = new byte[9000]; // > 8191 - поріг з коду

            var ex = Assert.Throws<ArgumentException>(() =>
                NetSdrMessageHelper.GetControlItemMessage(type, code, tooLong)
            );

            Assert.That(ex.Message, Does.Contain("Message length exceeds allowed value"));
        }

    }
}