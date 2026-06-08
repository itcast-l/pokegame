using Godot;

internal interface IPlayerInputHandler
{
	void ConfirmHandCard(int handIndex, int turnSequenceGeneration, Vector2? startGlobalPosition);
	bool IsHandCardLocked(int playerIndex, int handIndex);
}

internal sealed class LocalPlayerInputHandler : IPlayerInputHandler
{
	private readonly GameBoardScene _scene;

	internal LocalPlayerInputHandler(GameBoardScene scene)
	{
		_scene = scene;
	}

	public void ConfirmHandCard(int handIndex, int turnSequenceGeneration, Vector2? startGlobalPosition)
	{
		_scene.ConfirmLocalHandCardPlay(handIndex, turnSequenceGeneration, startGlobalPosition);
	}

	public bool IsHandCardLocked(int playerIndex, int handIndex)
	{
		return false;
	}
}

internal sealed class NetworkPlayerInputHandler : IPlayerInputHandler
{
	private readonly GameBoardScene _scene;

	internal NetworkPlayerInputHandler(GameBoardScene scene)
	{
		_scene = scene;
	}

	public void ConfirmHandCard(int handIndex, int turnSequenceGeneration, Vector2? startGlobalPosition)
	{
		_scene.RequestNetworkHandCardPlay(handIndex, turnSequenceGeneration);
	}

	public bool IsHandCardLocked(int playerIndex, int handIndex)
	{
		return _scene.IsNetworkHandCardLocked(playerIndex, handIndex);
	}
}

internal sealed class NetworkHostInputHandler : IPlayerInputHandler
{
	private readonly GameBoardScene _scene;

	internal NetworkHostInputHandler(GameBoardScene scene)
	{
		_scene = scene;
	}

	public void ConfirmHandCard(int handIndex, int turnSequenceGeneration, Vector2? startGlobalPosition)
	{
		_scene.ConfirmNetworkHostHandCardPlay(handIndex, turnSequenceGeneration);
	}

	public bool IsHandCardLocked(int playerIndex, int handIndex)
	{
		return false;
	}
}
