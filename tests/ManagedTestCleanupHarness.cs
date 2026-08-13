using System;
using Godot;

public partial class ManagedTestCleanupHarness : RefCounted
{
	public void DrainFinalizers()
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
	}
}
