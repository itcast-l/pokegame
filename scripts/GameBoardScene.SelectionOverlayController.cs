using System;
using System.Collections.Generic;
using Godot;

internal enum SelectionOverlayKind
{
	ItemSelection = 0,
	AdventureUpgrade = 1,
	QuestionRejudge = 2,
	AdventureUpgradeReplacement = 3,
}

internal sealed class SelectionOverlaySession
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

internal sealed class SelectionOverlayCoordinator
{
	private readonly Func<bool> _isOwnerStopping;
	private SelectionOverlaySession? _activeSession;

	internal SelectionOverlayCoordinator(Func<bool> isOwnerStopping)
	{
		_isOwnerStopping = isOwnerStopping;
	}

	internal SelectionOverlaySession? ActiveSession => _activeSession;

	internal bool HasVisibleSession()
	{
		return _activeSession != null && IsSessionVisible(_activeSession);
	}

	internal bool IsVisible(SelectionOverlayKind kind)
	{
		return _activeSession != null
			&& _activeSession.Kind == kind
			&& IsSessionVisible(_activeSession);
	}

	internal bool TrySelect(SelectionOverlayKind expectedKind, int optionIndex)
	{
		SelectionOverlaySession? session = _activeSession;
		if (session == null
			|| session.Kind != expectedKind
			|| !IsSessionVisible(session)
			|| optionIndex < 0
			|| optionIndex >= session.OptionCount)
		{
			return false;
		}

		return session.TrySelect(optionIndex);
	}

	internal void Dismiss(bool invokeCancel)
	{
		SelectionOverlaySession? session = _activeSession;
		if (session == null)
		{
			return;
		}

		if (invokeCancel)
		{
			session.Cancel();
		}

		if (ReferenceEquals(_activeSession, session))
		{
			session.ExpectedRelease = true;
			_activeSession = null;
			ReleaseSession(session);
		}
	}

	internal void DisposeActive()
	{
		Dismiss(invokeCancel: false);
	}

	internal void Register(SelectionOverlaySession session)
	{
		DisposeActive();
		_activeSession = session;
		Action treeExiting = () => HandleTreeExiting(session);
		session.Root.TreeExiting += treeExiting;
		session.RegisterUnsubscribe(() =>
		{
			if (GodotObject.IsInstanceValid(session.Root))
			{
				session.Root.TreeExiting -= treeExiting;
			}
		});
	}

	internal void BindButton(
		SelectionOverlaySession session,
		Button button,
		int optionIndex,
		Action<Button> refreshHighlight,
		Action<Button, bool> setPointerHover)
	{
		Action pressed = () => TrySelect(session.Kind, optionIndex);
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

	internal SelectionOverlaySession? GetActive(SelectionOverlayKind expectedKind)
	{
		SelectionOverlaySession? session = _activeSession;
		return session != null && session.Kind == expectedKind ? session : null;
	}

	internal static bool IsSessionVisible(SelectionOverlaySession session)
	{
		return GodotObject.IsInstanceValid(session.Root) && session.Root.IsVisibleInTree();
	}

	private void HandleTreeExiting(SelectionOverlaySession session)
	{
		if (_isOwnerStopping())
		{
			session.ExpectedRelease = true;
			if (ReferenceEquals(_activeSession, session))
			{
				_activeSession = null;
			}
			session.ReleaseSubscriptions();
			session.DisposeContent();
			return;
		}

		if (session.ExpectedRelease
			|| session.AbnormalReleaseHandled
			|| !ReferenceEquals(_activeSession, session))
		{
			return;
		}

		session.AbnormalReleaseHandled = true;
		session.ExpectedRelease = true;
		_activeSession = null;
		session.ReleaseSubscriptions();
		session.AbnormalRelease();
		Callable.From(session.DisposeContent).CallDeferred();
	}

	private static void ReleaseSession(SelectionOverlaySession session)
	{
		session.ReleaseSubscriptions();
		session.DisposeContent();
	}
}

public partial class GameBoardScene
{
	private SelectionOverlayCoordinator? _selectionOverlayCoordinator;

	private SelectionOverlayCoordinator SelectionOverlays
		=> _selectionOverlayCoordinator ??= new SelectionOverlayCoordinator(
			() => IsQueuedForDeletion() || _isLeavingGame || _networkMigrationInputLocked);

	private SelectionOverlaySession? ActiveSelectionOverlaySession
		=> _selectionOverlayCoordinator?.ActiveSession;

	private bool HasVisibleSelectionOverlay()
	{
		return _selectionOverlayCoordinator?.HasVisibleSession() == true;
	}

	private bool IsSelectionOverlayVisible(SelectionOverlayKind kind)
	{
		return _selectionOverlayCoordinator?.IsVisible(kind) == true;
	}

	private bool TrySelectActiveSelectionOverlayOption(SelectionOverlayKind expectedKind, int optionIndex)
	{
		return _selectionOverlayCoordinator?.TrySelect(expectedKind, optionIndex) == true;
	}

	private void DismissActiveSelectionOverlay(bool invokeCancel)
	{
		_selectionOverlayCoordinator?.Dismiss(invokeCancel);
	}

	private void DisposeActiveSelectionOverlay()
	{
		_selectionOverlayCoordinator?.DisposeActive();
	}

	private void RegisterSelectionOverlaySession(SelectionOverlaySession session)
	{
		SelectionOverlays.Register(session);
	}

	private void BindSelectionOverlayButton(
		SelectionOverlaySession session,
		Button button,
		int optionIndex,
		Action<Button> refreshHighlight,
		Action<Button, bool> setPointerHover)
	{
		SelectionOverlays.BindButton(
			session,
			button,
			optionIndex,
			refreshHighlight,
			setPointerHover);
	}

	private SelectionOverlaySession? GetActiveSelectionOverlaySession(SelectionOverlayKind expectedKind)
	{
		return _selectionOverlayCoordinator?.GetActive(expectedKind);
	}

	private static bool IsSelectionOverlaySessionVisible(SelectionOverlaySession session)
	{
		return SelectionOverlayCoordinator.IsSessionVisible(session);
	}
}
