using System;
using System.Collections.Generic;
using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
	#region Properties

	public static CheckpointManager instance;
	public Transform LastCheckpoint => _lastCheckpoint;
	public Transform FirstCheckpoint => _lastCheckpoint.GetComponent<Checkpoint>().NextCheckpoint;
	
	#endregion


	#region Variables

	private Transform _lastCheckpoint;
	
	#endregion


	#region Fonctions

	private void Awake()
	{
		instance = this;
		
		_lastCheckpoint = transform.GetChild(transform.childCount - 1);
	}


	#endregion
}
