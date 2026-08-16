using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private enum SelectionOverlayKind
	{
		ItemSelection = 0,
		AdventureUpgrade = 1,
		QuestionRejudge = 2,
		AdventureUpgradeReplacement = 3,
	}

	private sealed class SelectionOverlaySession
	{
		private readonly List<Action> _unsubscribeActions = new();
		private readonly Action _dispose;
		private bool _subscriptionsReleased;
		private bool _disposed;

		internal SelectionOverlaySession(
			SelectionOverlayKind kind,
			Control root,
			int optionCount,
			Func<int, bool> trySelect,
			Action cancel,
			Action dispose,
			Action abnormalRelease)
		{
			Kind = kind;
			Root = root;
			OptionCount = optionCount;
			TrySelect = trySelect;
			Cancel = cancel;
			_dispose = dispose;
			AbnormalRelease = abnormalRelease;
		}

		internal SelectionOverlayKind Kind { get; }
		internal Control Root { get; }
		internal int OptionCount { get; }
		internal Func<int, bool> TrySelect { get; }
		internal Action Cancel { get; }
		internal Action AbnormalRelease { get; }
		internal bool ExpectedRelease { get; set; }
		internal bool AbnormalReleaseHandled { get; set; }
		internal int ActiveSubscriptionCount => _subscriptionsReleased ? 0 : _unsubscribeActions.Count;

		internal void RegisterUnsubscribe(Action unsubscribe)
		{
			if (_subscriptionsReleased)
			{
				unsubscribe();
				return;
			}
			_unsubscribeActions.Add(unsubscribe);
		}

		internal void ReleaseSubscriptions()
		{
			if (_subscriptionsReleased)
			{
				return;
			}

			_subscriptionsReleased = true;
			for (int index = _unsubscribeActions.Count - 1; index >= 0; index--)
			{
				_unsubscribeActions[index]();
			}
			_unsubscribeActions.Clear();
		}

		internal void DisposeContent()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			_dispose();
		}
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
			session.ExpectedRelease = true;
			_activeSelectionOverlaySession = null;
			ReleaseSelectionOverlaySession(session);
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
		Action treeExiting = () => HandleSelectionOverlayTreeExiting(session);
		session.Root.TreeExiting += treeExiting;
		session.RegisterUnsubscribe(() =>
		{
			if (GodotObject.IsInstanceValid(session.Root))
			{
				session.Root.TreeExiting -= treeExiting;
			}
		});
	}

	private void BindSelectionOverlayButton(
		SelectionOverlaySession session,
		Button button,
		int optionIndex,
		Action<Button> refreshHighlight,
		Action<Button, bool> setPointerHover)
	{
		Action pressed = () => TrySelectActiveSelectionOverlayOption(session.Kind, optionIndex);
		Action focusEntered = () => refreshHighlight(button);
		Action focusExited = () => refreshHighlight(button);
		Action mouseEntered = () => setPointerHover(button, true);
		Action mouseExited = () => setPointerHover(button, false);

		button.Pressed += pressed;
		button.FocusEntered += focusEntered;
		button.FocusExited += focusExited;
		button.MouseEntered += mouseEntered;
		button.MouseExited += mouseExited;
		session.RegisterUnsubscribe(() =>
		{
			if (!GodotObject.IsInstanceValid(button))
			{
				return;
			}
			button.Pressed -= pressed;
			button.FocusEntered -= focusEntered;
			button.FocusExited -= focusExited;
			button.MouseEntered -= mouseEntered;
			button.MouseExited -= mouseExited;
		});
	}

	private void HandleSelectionOverlayTreeExiting(SelectionOverlaySession session)
	{
		if (IsQueuedForDeletion() || _isLeavingGame || _networkMigrationInputLocked)
		{
			session.ExpectedRelease = true;
			if (ReferenceEquals(_activeSelectionOverlaySession, session))
			{
				_activeSelectionOverlaySession = null;
			}
			session.ReleaseSubscriptions();
			session.DisposeContent();
			return;
		}

		if (session.ExpectedRelease
			|| session.AbnormalReleaseHandled
			|| !ReferenceEquals(_activeSelectionOverlaySession, session))
		{
			return;
		}

		session.AbnormalReleaseHandled = true;
		session.ExpectedRelease = true;
		_activeSelectionOverlaySession = null;
		session.ReleaseSubscriptions();
		session.AbnormalRelease();
		Callable.From(session.DisposeContent).CallDeferred();
	}

	private static void ReleaseSelectionOverlaySession(SelectionOverlaySession session)
	{
		session.ReleaseSubscriptions();
		session.DisposeContent();
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
