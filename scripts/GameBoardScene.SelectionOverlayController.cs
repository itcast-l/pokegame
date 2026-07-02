using System;
using Godot;

public partial class GameBoardScene
{
	private enum SelectionOverlayKind
	{
		ItemSelection = 0,
		AdventureUpgrade = 1,
	}

	private sealed class SelectionOverlaySession
	{
		internal SelectionOverlaySession(
			SelectionOverlayKind kind,
			Control root,
			int optionCount,
			Func<int, bool> trySelect,
			Action cancel,
			Action dispose)
		{
			Kind = kind;
			Root = root;
			OptionCount = optionCount;
			TrySelect = trySelect;
			Cancel = cancel;
			Dispose = dispose;
		}

		internal SelectionOverlayKind Kind { get; }
		internal Control Root { get; }
		internal int OptionCount { get; }
		internal Func<int, bool> TrySelect { get; }
		internal Action Cancel { get; }
		internal Action Dispose { get; }
	}

	private bool HasVisibleSelectionOverlay()
	{
		return _activeSelectionOverlaySession != null
			&& IsSelectionOverlaySessionVisible(_activeSelectionOverlaySession);
	}

	private bool IsSelectionOverlayVisible(SelectionOverlayKind kind)
	{
		return _activeSelectionOverlaySession != null
			&& _activeSelectionOverlaySession.Kind == kind
			&& IsSelectionOverlaySessionVisible(_activeSelectionOverlaySession);
	}

	private bool TrySelectActiveSelectionOverlayOption(SelectionOverlayKind expectedKind, int optionIndex)
	{
		SelectionOverlaySession? session = _activeSelectionOverlaySession;
		if (session == null
			|| session.Kind != expectedKind
			|| !IsSelectionOverlaySessionVisible(session)
			|| optionIndex < 0
			|| optionIndex >= session.OptionCount)
		{
			return false;
		}

		return session.TrySelect(optionIndex);
	}

	private void DismissActiveSelectionOverlay(bool invokeCancel)
	{
		SelectionOverlaySession? session = _activeSelectionOverlaySession;
		if (session == null)
		{
			return;
		}

		if (invokeCancel)
		{
			session.Cancel();
		}

		if (ReferenceEquals(_activeSelectionOverlaySession, session))
		{
			_activeSelectionOverlaySession = null;
			session.Dispose();
		}
	}

	private void DisposeActiveSelectionOverlay()
	{
		DismissActiveSelectionOverlay(invokeCancel: false);
	}

	private void RegisterSelectionOverlaySession(SelectionOverlaySession session)
	{
		DisposeActiveSelectionOverlay();
		_activeSelectionOverlaySession = session;
	}

	private SelectionOverlaySession? GetActiveSelectionOverlaySession(SelectionOverlayKind expectedKind)
	{
		SelectionOverlaySession? session = _activeSelectionOverlaySession;
		if (session == null || session.Kind != expectedKind)
		{
			return null;
		}

		return session;
	}

	private static bool IsSelectionOverlaySessionVisible(SelectionOverlaySession session)
	{
		return GodotObject.IsInstanceValid(session.Root) && session.Root.IsVisibleInTree();
	}
}
