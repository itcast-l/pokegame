using Godot;

public partial class GameBoardScene
{
	private ulong BeginPerformanceSample(string category = "")
	{
		return debug_performance_trace ? Time.GetTicksUsec() : 0UL;
	}

	private void EndPerformanceSample(string label, ulong startUsec)
	{
		if (!debug_performance_trace || startUsec == 0UL)
		{
			return;
		}

		ulong elapsedUsec = Time.GetTicksUsec() - startUsec;
		float elapsedMs = elapsedUsec / 1000.0f;
		if (elapsedMs >= Mathf.Max(0.0f, debug_performance_trace_min_ms))
		{
			GD.Print($"[Perf][{GetPerformanceCategory(label)}] {label}: {elapsedMs:0.###} ms");
		}
	}

	private static string GetPerformanceCategory(string label)
	{
		if (label.StartsWith("Render", System.StringComparison.OrdinalIgnoreCase))
		{
			return "Render";
		}

		if (label.StartsWith("Warmup", System.StringComparison.OrdinalIgnoreCase)
			|| label.StartsWith("Load", System.StringComparison.OrdinalIgnoreCase))
		{
			return "ResourceLoad";
		}

		if (label.Contains("Animation", System.StringComparison.OrdinalIgnoreCase)
			|| label.Contains("Tween", System.StringComparison.OrdinalIgnoreCase))
		{
			return "Animation";
		}

		return "Rules";
	}
}
