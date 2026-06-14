using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

internal static class LanAddressHelper
{
	internal static IReadOnlyList<string> GetLocalIpv4Addresses()
	{
		var addresses = new HashSet<string>(StringComparer.Ordinal);
		foreach ((IPAddress address, _) in EnumerateLanIpv4Addresses())
		{
			addresses.Add(address.ToString());
		}

		return addresses.Count == 0
			? Array.Empty<string>()
			: addresses.OrderBy(value => value, StringComparer.Ordinal).ToArray();
	}

	internal static IReadOnlyList<string> GetBroadcastAddresses()
	{
		var addresses = new HashSet<string>(StringComparer.Ordinal)
		{
			"255.255.255.255",
		};
		foreach ((IPAddress address, IPAddress mask) in EnumerateLanIpv4Addresses())
		{
			IPAddress? broadcastAddress = TryComputeBroadcastAddress(address, mask);
			if (broadcastAddress != null)
			{
				addresses.Add(broadcastAddress.ToString());
			}
		}

		return addresses.OrderBy(value => value, StringComparer.Ordinal).ToArray();
	}

	internal static IPAddress? TryComputeBroadcastAddress(IPAddress address, IPAddress subnetMask)
	{
		if (address.AddressFamily != AddressFamily.InterNetwork
			|| subnetMask.AddressFamily != AddressFamily.InterNetwork)
		{
			return null;
		}

		byte[] addressBytes = address.GetAddressBytes();
		byte[] maskBytes = subnetMask.GetAddressBytes();
		if (addressBytes.Length != 4 || maskBytes.Length != 4)
		{
			return null;
		}

		var broadcastBytes = new byte[4];
		for (int index = 0; index < 4; index++)
		{
			broadcastBytes[index] = (byte)(addressBytes[index] | ~maskBytes[index]);
		}

		return new IPAddress(broadcastBytes);
	}

	private static IEnumerable<(IPAddress Address, IPAddress Mask)> EnumerateLanIpv4Addresses()
	{
		foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
		{
			if (networkInterface.OperationalStatus != OperationalStatus.Up
				|| networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback
				|| networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
			{
				continue;
			}

			IPInterfaceProperties properties;
			try
			{
				properties = networkInterface.GetIPProperties();
			}
			catch (NetworkInformationException)
			{
				continue;
			}

			foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
			{
				if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork
					|| IPAddress.IsLoopback(unicastAddress.Address)
					|| unicastAddress.IPv4Mask == null)
				{
					continue;
				}

				yield return (unicastAddress.Address, unicastAddress.IPv4Mask);
			}
		}
	}
}
