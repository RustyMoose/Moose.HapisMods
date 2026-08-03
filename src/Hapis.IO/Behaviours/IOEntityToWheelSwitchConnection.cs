using System.Collections.Generic;
using UnityEngine;

namespace Moose.Hapis.IO.Behaviours;

internal class IOEntityToWheelSwitchConnection : MonoBehaviour
{
	private IOEntity _ioTrigger;

	private List<WheelSwitch> _connectedWheelSwitches = [];

	private bool _running;

	private BaseEntity.Flags _triggered;

	private void Awake()
	{
		enabled = false;
		_ioTrigger = GetComponent<IOEntity>();
		_triggered = _ioTrigger is WheelSwitch ? BaseEntity.Flags.Reserved1 : BaseEntity.Flags.On;
	}

	private void Update()
	{
		if (_running)
		{
			if (_ioTrigger.HasFlag(_triggered))
			{
				return;
			}
			
			_running = false;
			
			for (var i = 0; i < _connectedWheelSwitches.Count; i++)
			{
				var wheelSwitch = _connectedWheelSwitches[i];
				if (wheelSwitch != null && wheelSwitch.IsInvoking(wheelSwitch.Powered))
				{
					wheelSwitch.CancelInvoke(wheelSwitch.Powered);
				}
			}
		}
		else
		{
			if (!_ioTrigger.HasFlag(_triggered))
			{
				return;
			}
			
			_running = true;
			
			for (var j = 0; j < _connectedWheelSwitches.Count; j++)
			{
				var wheelSwitch2 = _connectedWheelSwitches[j];
				if (wheelSwitch2 != null)
				{
					wheelSwitch2.InvokeRepeating(wheelSwitch2.Powered, 0f, 0.1f);
				}
			}
		}
	}

	public void SetTarget(WheelSwitch target)
	{
		if (target == null)
		{
			return;
		}
		
		_connectedWheelSwitches.Add(target);
		enabled = true;
	}
}
