using System;
using TreeEditor;
using UnityEngine;
using UnityEngine.InputSystem.Android;

public class Checkpoint : MonoBehaviour
{
	#region Properties

	public Transform NextCheckpoint => _nextCheckpoint;
	
	#endregion


	#region Variables

	[SerializeField]private Transform _nextCheckpoint;

	#endregion


	#region Fonctions

	private void Awake()
	{
		Transform parent = transform.parent;
		int id = 0;
		for (int i = 0; i < parent.childCount; i++)
		{
			if (parent.GetChild(i) != gameObject.transform)
			{
				id++;
			}
			else
			{
				break;
			}
		}

		if (id == parent.childCount-1)
			id = 0;
		else
		{
			id += 1;
		}
		
		_nextCheckpoint = parent.GetChild(id);
	}

	public void OnTriggerEnter(Collider other)
	{
		if (other.TryGetComponent<Agent>(out Agent agent))
		{
			if (agent.NextCheckpoint == transform)
			{
				agent.CheckpointReach(_nextCheckpoint);
			}
		}
	}

	#endregion
}
