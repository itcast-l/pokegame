using Godot;

public partial class GameBoardScene
{
#if DECKLAND_TEST_HOOKS
	private int _debugResourceLoadCount;
#endif

	private ulong BeginPerformanceSample(string category = "")
	{
#if DECKLAND_TEST_HOOKS
		if (string.Equals(category, "ResourceLoad", System.StringComparison.Ordinal))
		{
			_debugResourceLoadCount++;
		}

		return debug_performance_trace ? Time.GetTicksUsec() : 0UL;
#else
		return 0UL;
#endif
	}

#if DECKLAND_TEST_HOOKS
	public int DebugGetResourceLoadCount()
	{
		return _debugResourceLoadCount;
	}

	public int DebugGetDynamicBackgroundResourceLoadCount()
	{
		int resourceLoadCount = 0;
		int rebuildCount = 0;
		AccumulateDynamicBackgroundCounters(this, ref resourceLoadCount, ref rebuildCount);
		return resourceLoadCount;
	}

	public int DebugGetDynamicBackgroundRebuildCount()
	{
		int resourceLoadCount = 0;
		int rebuildCount = 0;
		AccumulateDynamicBackgroundCounters(this, ref resourceLoadCount, ref rebuildCount);
		return rebuildCount;
	}

	public long DebugGetCurrentThreadAllocatedBytes()
	{
		return System.GC.GetAllocatedBytesForCurrentThread();
	}

	private static void AccumulateDynamicBackgroundCounters(
		Node node,
		ref int resourceLoadCount,
		ref int rebuildCount)
	{
		switch (node)
		{
			case AdventureMapDynamicLayer adventureMapLayer:
				resourceLoadCount += adventureMapLayer.DebugGetResourceLoadCount();
				rebuildCount += adventureMapLayer.DebugGetRebuildCount();
				break;
			case UnderwaterAmbientLayer underwaterLayer:
				resourceLoadCount += underwaterLayer.DebugGetResourceLoadCount();
				rebuildCount += underwaterLayer.DebugGetRebuildCount();
				break;
			case MainMenuOceanDynamics oceanDynamics:
				resourceLoadCount += oceanDynamics.DebugGetResourceLoadCount();
				rebuildCount += oceanDynamics.DebugGetRebuildCount();
				break;
		}

		foreach (Node child in node.GetChildren())
		{
			AccumulateDynamicBackgroundCounters(child, ref resourceLoadCount, ref rebuildCount);
		}
	}
#endif

	private void EndPerformanceSample(string label, ulong startUsec)
	{
#if DECKLAND_TEST_HOOKS
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
#endif
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
