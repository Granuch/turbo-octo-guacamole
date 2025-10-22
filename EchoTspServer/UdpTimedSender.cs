﻿using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TestServerApp
{
    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly UdpClient _udpClient;
        private Timer? _timer;
        private ushort _sequenceNumber;

        public UdpTimedSender(string host, int port)
        {
            _host = host;
            _port = port;
            _udpClient = new UdpClient();
            _sequenceNumber = 0;
        }

        public void StartSending(int intervalMilliseconds)
        {
            if (_timer != null)
                throw new InvalidOperationException("Sender is already running.");

            _timer = new Timer(SendMessageCallback, null, 0, intervalMilliseconds);
        }

        private void SendMessageCallback(object? state)
        {
            try
            {
                byte[] samples = new byte[1024];
                Random.Shared.NextBytes(samples);
                _sequenceNumber++;

                byte[] msg = (new byte[] { 0x04, 0x84 })
                    .Concat(BitConverter.GetBytes(_sequenceNumber))
                    .Concat(samples)
                    .ToArray();

                var endpoint = new IPEndPoint(IPAddress.Parse(_host), _port);

                _udpClient.Send(msg, msg.Length, endpoint);
                Console.WriteLine($"Message sent to {_host}:{_port} ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        public void StopSending()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose()
        {
            StopSending();
            _udpClient?.Dispose();
        }
    }
}