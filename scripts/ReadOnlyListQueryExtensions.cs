using System;
using System.Collections.Generic;

internal static class ReadOnlyListQueryExtensions
{
	internal static int IndexOf<T>(this IReadOnlyList<T> source, T value)
	{
		EqualityComparer<T> comparer = EqualityComparer<T>.Default;
		for (int index = 0; index < source.Count; index++)
		{
			if (comparer.Equals(source[index], value))
			{
				return index;
			}
		}
		return -1;
	}

	internal static bool Exists<T>(this IReadOnlyList<T> source, Predicate<T> predicate)
	{
		for (int index = 0; index < source.Count; index++)
		{
			if (predicate(source[index]))
			{
				return true;
			}
		}
		return false;
	}

	internal static void CopyTo<T>(this IReadOnlyList<T> source, T[] destination)
	{
		if (destination.Length < source.Count)
		{
			throw new ArgumentException("The destination is shorter than the source.", nameof(destination));
		}
		for (int index = 0; index < source.Count; index++)
		{
			destination[index] = source[index];
		}
	}
}
