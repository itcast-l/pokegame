using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

internal sealed class LanDiscoverySocket : IDisposable
{
	private readonly Socket _socket;
	private readonly int _maximumPacketBytes;
	private readonly ConcurrentQueue<ReceivedDatagram> _receivedDatagrams = new();
	private readonly Dictionary<string, IPEndPoint> _endpointsByAddress = new(StringComparer.Ordinal);
	private readonly Thread _receiveThread;
	private int _queuedDatagramCount;
	private volatile bool _disposed;
	private const int MaximumQueuedDatagrams = 256;

	private readonly record struct ReceivedDatagram(byte[] Payload, IPEndPoint Sender);

	private LanDiscoverySocket(Socket socket, int maximumPacketBytes)
	{
		_socket = socket;
		_maximumPacketBytes = maximumPacketBytes;
		_receiveThread = new Thread(ReceiveLoop)
		{
			IsBackground = true,
			Name = "LanDiscoveryReceiver",
		};
		_receiveThread.Start();
	}

	public static bool TryBind(
		int port,
		int maximumPacketBytes,
		out LanDiscoverySocket? socket,
		out string error)
	{
		socket = null;
		error = string.Empty;
		Socket? nativeSocket = null;
		try
		{
			nativeSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
			{
				Blocking = true,
				EnableBroadcast = true,
			};
			nativeSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			nativeSocket.Bind(new IPEndPoint(IPAddress.Any, port));
			socket = new LanDiscoverySocket(nativeSocket, maximumPacketBytes);
			return true;
		}
		catch (SocketException exception)
		{
			error = exception.SocketErrorCode.ToString();
			nativeSocket?.Dispose();
			return false;
		}
		catch (ObjectDisposedException)
		{
			error = SocketError.NotSocket.ToString();
			nativeSocket?.Dispose();
			return false;
		}
	}

	public bool HasAvailablePacket()
	{
		return !_disposed && !_receivedDatagrams.IsEmpty;
	}

	public bool TryReceive(out ReadOnlyMemory<byte> payload, out IPEndPoint sender)
	{
		payload = ReadOnlyMemory<byte>.Empty;
		sender = null!;
		if (_disposed || !_receivedDatagrams.TryDequeue(out ReceivedDatagram datagram))
		{
			return false;
		}

		Interlocked.Decrement(ref _queuedDatagramCount);
		payload = datagram.Payload;
		sender = datagram.Sender;
		return true;
	}

	public bool TrySend(ReadOnlySpan<byte> payload, string address, int port)
	{
		if (_disposed || string.IsNullOrWhiteSpace(address))
		{
			return false;
		}

		if (!_endpointsByAddress.TryGetValue(address, out IPEndPoint? endpoint))
		{
			if (!IPAddress.TryParse(address, out IPAddress? ipAddress)
				|| ipAddress.AddressFamily != AddressFamily.InterNetwork)
			{
				return false;
			}
			endpoint = new IPEndPoint(ipAddress, port);
			_endpointsByAddress[address] = endpoint;
		}

		return TrySend(payload, endpoint);
	}

	public bool TrySend(ReadOnlySpan<byte> payload, IPEndPoint endpoint)
	{
		if (_disposed)
		{
			return false;
		}

		try
		{
			return _socket.SendTo(payload, SocketFlags.None, endpoint) == payload.Length;
		}
		catch (SocketException)
		{
			return false;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		if (Thread.CurrentThread != _receiveThread)
		{
			_receiveThread.Join(millisecondsTimeout: 150);
		}
		while (_receivedDatagrams.TryDequeue(out _))
		{
		}
		Volatile.Write(ref _queuedDatagramCount, 0);
		_endpointsByAddress.Clear();
	}

	private void ReceiveLoop()
	{
		byte[] receiveBuffer = new byte[_maximumPacketBytes + 1];
		EndPoint receiveEndpoint = new IPEndPoint(IPAddress.Any, 0);
		try
		{
			while (!_disposed)
			{
				try
				{
					if (!_socket.Poll(50_000, SelectMode.SelectRead))
					{
						continue;
					}

					int receivedBytes = _socket.ReceiveFrom(
						receiveBuffer,
						0,
						receiveBuffer.Length,
						SocketFlags.None,
						ref receiveEndpoint);
					if (receiveEndpoint is not IPEndPoint sender)
					{
						continue;
					}

					if (Interlocked.Increment(ref _queuedDatagramCount) > MaximumQueuedDatagrams)
					{
						Interlocked.Decrement(ref _queuedDatagramCount);
						continue;
					}
					byte[] payload = receiveBuffer.AsSpan(0, receivedBytes).ToArray();
					_receivedDatagrams.Enqueue(new ReceivedDatagram(payload, sender));
				}
				catch (SocketException)
				{
					if (_disposed)
					{
						break;
					}

					Thread.Sleep(millisecondsTimeout: 10);
				}
				catch (ObjectDisposedException)
				{
					break;
				}
			}
		}
		finally
		{
			_socket.Dispose();
		}
	}
}
