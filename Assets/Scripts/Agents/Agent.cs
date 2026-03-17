using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Agent : MonoBehaviour
{
	#region Properties

	public float Fitness {get => _fitness; set => _fitness = value; }
	public NeuralNetwork Network { get => _net; set => _net = value; }
	public Transform NextCheckpoint { get => _nextCheckpoint; set => _nextCheckpoint = value; }

	#endregion


	#region Variables

	[SerializeField] NeuralNetwork _net;
	[SerializeField] float _fitness;
	[SerializeField] KartController _carController;
	[SerializeField] private float _rayRange = 4;
	[SerializeField] private LayerMask _layerMaskRay;
	
	[Header("Color Changing")]
	[SerializeField] private Renderer _kartRenderer;
	[SerializeField] private Renderer _marioRenderer;
	[SerializeField] private Material[] _materialsKart;
	[SerializeField] private Material[] _materialsMario;
	
	float[] inputs;
	private RaycastHit hit;
	private Vector3 pos;
	
	[Header("Fitness calculation")]
	private float _distanceTraveled;
	private float _totalCheckpointDist;
	private float _nextCheckpointDist;
	private Transform _nextCheckpoint;
	
	#endregion
	
	#region Fonctions
	

	public void ResetAgent()
	{
		StartCoroutine(ResetCoroutine());
	}

	private IEnumerator ResetCoroutine()
	{
		yield return new WaitForFixedUpdate();
    
		transform.position = Vector3.zero;
		transform.rotation = Quaternion.identity;

		inputs = new float[_net._layers[0]];
		_fitness = 0;
		_totalCheckpointDist = 0;
		_nextCheckpoint = CheckpointManager.instance.FirstCheckpoint;
		_nextCheckpointDist = (_nextCheckpoint.position - transform.position).magnitude;
	}

	void FixedUpdate()
	{
		if (inputs == null) return;
		
		InputsUpdate();
		OutputsUpdate();
		FitnessUpdate();
	}

	private void InputsUpdate()
	{
		pos = transform.position;

		Debug.Log(inputs.Length);
		
		inputs[0] = RaySensor(pos+Vector3.up*0.2f, transform.forward, 4);
		inputs[1] = RaySensor(pos+Vector3.up*0.2f, transform.right, 1.5f);
		inputs[2] = RaySensor(pos+Vector3.up*0.2f, -transform.right, 1.5f);
		inputs[3] = RaySensor(pos+Vector3.up*0.2f, transform.forward + transform.right, 2);
		inputs[4] = RaySensor(pos+Vector3.up*0.2f, transform.forward - transform.right, 2);
		inputs[5] = 1;
		inputs[6] = _carController.DriftAmount;
	}

	private float RaySensor(Vector3 origin, Vector3 direction, float lenght)
	{
		float realLenght = lenght * _rayRange;

		if (Physics.Raycast(origin, direction, out hit, realLenght, _layerMaskRay))
		{
			Color rayColor = Color.Lerp(Color.red, Color.green, 1- hit.distance / realLenght);
			Debug.DrawRay(origin, direction.normalized * hit.distance, rayColor);
			
			return 1 - hit.distance / realLenght;
		}
		
		Debug.DrawRay(origin, direction.normalized * realLenght, Color.red);

		return 0;
	}

	private void OutputsUpdate()
	{
		_net.FeedForward(inputs);

		_carController.HorizontalInput = _net._neurons[^1][0];
		_carController.VerticalInput = _net._neurons[^1][1];
		_carController.IsDrifing = _net._neurons[^1][2] > 0.9f;
	}
	
	
	private void FitnessUpdate()
	{
		_distanceTraveled = _totalCheckpointDist + (_nextCheckpointDist - (_nextCheckpoint.position - transform.position).magnitude);

		if (_fitness < _distanceTraveled)
		{
			_fitness = _distanceTraveled;
		}
	}
	

	public void CheckpointReach(Transform checkpoint)
	{
		_totalCheckpointDist += _nextCheckpointDist;
		_nextCheckpoint = checkpoint;
		_nextCheckpointDist = (_nextCheckpoint.position - transform.position).magnitude;
	}

	//0 = first
	//1 = default
	//2 = mutant
	public void SetMaterial(int colorID)
	{
		_kartRenderer.material = _materialsKart[colorID];
		_marioRenderer.material = _materialsMario[colorID];
	}
	
	
	#endregion
}
