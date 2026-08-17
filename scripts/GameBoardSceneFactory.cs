using System;
using Godot;

public static class GameBoardSceneFactory
{
	public static bool TryCreate(
		PackedScene packedScene,
		GameBoardLaunchOptions options,
		out GameBoardScene gameBoard,
		out string error)
	{
		gameBoard = default!;
		error = string.Empty;
		if (packedScene == null)
		{
			error = "Game board scene is missing.";
			return false;
		}

		try
		{
			GameBoardLaunchOptions.Validate(options);
			Node instance = packedScene.Instantiate();
			if (instance is not GameBoardScene board)
			{
				instance.Free();
				error = "Game board scene root is not a GameBoardScene.";
				return false;
			}

			if (!board.ConfigureForLaunch(options, out error))
			{
				board.Free();
				return false;
			}

			gameBoard = board;
			return true;
		}
		catch (Exception exception)
		{
			error = exception.Message;
			return false;
		}
	}

	public static GameBoardScene Create(PackedScene packedScene, GameBoardLaunchOptions options)
	{
		if (TryCreate(packedScene, options, out GameBoardScene gameBoard, out string error))
		{
			return gameBoard;
		}

		throw new InvalidOperationException($"Unable to create game board: {error}");
	}
}
