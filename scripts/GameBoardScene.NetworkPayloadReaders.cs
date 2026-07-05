using Godot;

public partial class GameBoardScene
{
	private static string ReadNetworkString(Godot.Collections.Dictionary payload, string key, string fallback)
	{
		return payload.TryGetValue(key, out Variant value) ? value.AsString() : fallback;
	}

	private static int ReadNetworkInt(Godot.Collections.Dictionary payload, string key, int fallback)
	{
		return payload.TryGetValue(key, out Variant value) ? (int)value.AsInt64() : fallback;
	}

	private static long ReadNetworkLong(Godot.Collections.Dictionary payload, string key, long fallback)
	{
		return payload.TryGetValue(key, out Variant value) ? value.AsInt64() : fallback;
	}

	private static bool ReadNetworkBool(Godot.Collections.Dictionary payload, string key, bool fallback)
	{
		return payload.TryGetValue(key, out Variant value) ? value.AsBool() : fallback;
	}

	private static float ReadNetworkFloat(Godot.Collections.Dictionary payload, string key, float fallback)
	{
		return payload.TryGetValue(key, out Variant value) ? value.AsSingle() : fallback;
	}
}
