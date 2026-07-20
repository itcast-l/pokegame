using Godot;

public partial class BoardAsyncLifetimeDebugProbe : RefCounted
{
	public int GetActiveLifetimeCount()
	{
		return BoardAsyncLifetime.ActiveLifetimeCount;
	}
}
