internal struct VisualUpdateThrottle
{
	private double _accumulator;
	private bool _forceNextUpdate;

	internal bool Advance(double delta, int updatesPerSecond)
	{
		if (_forceNextUpdate)
		{
			_forceNextUpdate = false;
			return true;
		}

		if (delta < 0.0)
		{
			delta = 0.0;
		}

		_accumulator += delta;
		double interval = 1.0 / System.Math.Max(updatesPerSecond, 1);
		if (_accumulator < interval)
		{
			return false;
		}

		_accumulator -= interval;
		return true;
	}

	internal void ForceNextUpdate()
	{
		_forceNextUpdate = true;
	}

	internal void Reset()
	{
		_accumulator = 0.0;
		_forceNextUpdate = false;
	}
}
