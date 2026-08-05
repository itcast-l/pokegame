using System;
using System.Collections.Generic;

internal sealed class GameBoardFrameScheduler : IDisposable
{
	private sealed class Registration : IDisposable
	{
		private GameBoardFrameScheduler? _owner;
		private readonly long _id;

		internal Registration(GameBoardFrameScheduler owner, long id)
		{
			_owner = owner;
			_id = id;
		}

		public void Dispose()
		{
			GameBoardFrameScheduler? owner = _owner;
			_owner = null;
			owner?.Unregister(_id);
		}
	}

	private struct Entry
	{
		internal Entry(long id, Action<double> update)
		{
			Id = id;
			Update = update;
		}

		internal long Id { get; }
		internal Action<double>? Update { get; set; }
	}

	private readonly List<Entry> _entries = new();
	private long _nextId;
	private int _activeCount;
	private bool _disposed;

	internal bool HasActiveRegistrations => _activeCount > 0;

	internal IDisposable Register(Action<double> update)
	{
		ArgumentNullException.ThrowIfNull(update);
		ObjectDisposedException.ThrowIf(_disposed, this);

		long id = checked(++_nextId);
		_entries.Add(new Entry(id, update));
		_activeCount++;
		return new Registration(this, id);
	}

	internal void Tick(double delta)
	{
		if (_disposed || _activeCount == 0)
		{
			return;
		}

		int countAtStart = _entries.Count;
		for (int index = 0; index < countAtStart; index++)
		{
			_entries[index].Update?.Invoke(delta);
		}

		CompactInactiveEntries();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_entries.Clear();
		_activeCount = 0;
	}

	private void Unregister(long id)
	{
		if (_disposed)
		{
			return;
		}

		for (int index = 0; index < _entries.Count; index++)
		{
			Entry entry = _entries[index];
			if (entry.Id != id || entry.Update == null)
			{
				continue;
			}

			entry.Update = null;
			_entries[index] = entry;
			_activeCount--;
			return;
		}
	}

	private void CompactInactiveEntries()
	{
		if (_activeCount == _entries.Count)
		{
			return;
		}

		int nextIndex = 0;
		for (int index = 0; index < _entries.Count; index++)
		{
			Entry entry = _entries[index];
			if (entry.Update == null)
			{
				continue;
			}

			if (nextIndex != index)
			{
				_entries[nextIndex] = entry;
			}
			nextIndex++;
		}

		if (nextIndex < _entries.Count)
		{
			_entries.RemoveRange(nextIndex, _entries.Count - nextIndex);
		}
	}
}
