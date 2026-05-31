using Godot;

public partial class GameBoardScene
{
	private ulong BeginPerformanceSample()
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
			GD.Print($"[Perf] {label}: {elapsedMs:0.###} ms");
		}
	}
}
