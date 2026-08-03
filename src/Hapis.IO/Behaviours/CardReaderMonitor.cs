using UnityEngine;

namespace Moose.Hapis.IO.Behaviours;

internal class CardReaderMonitor : MonoBehaviour
{
	private CardReader _cardReader;

	private bool _timeOut;
	
	public float TimerLength;

	private void Awake()
	{
		_cardReader = GetComponent<CardReader>();
		enabled = false;
		_cardReader.Invoke(CheckTimerLength, 1f);
	}

	private void Update()
	{
		if (_timeOut || !_cardReader.HasFlag(BaseEntity.Flags.On))
		{
			return;
		}
		
		_timeOut = true;
		_cardReader.Invoke(RestState, TimerLength);
	}

	public void CheckTimerLength()
	{
		if (TimerLength < 0.01f)
		{
			Destroy(this);
			return;
		}
		RestState();
		enabled = true;
	}

	private void RestState()
	{
		_cardReader.ResetIOState();
		_timeOut = false;
	}
}
