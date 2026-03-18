using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditorInternal;
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
	[SerializeField] public KartController _carController;
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
	private float _driftAmount;
	private float _driftBoostPrice;
	private float[] _wallDetect;

	
	#endregion
	
	#region Fonctions

	public void Start()
	{
		_carController.OnDriftBoost += AddBoostFitness;
		_carController.OnDriftStart += CheckSpamming;
		_carController.OnDriftEnd += CheckLevelBoost;
		_wallDetect = new float[5];

		tr = transform;
	}
	
	private void OnDisable()
	{
		_carController.OnDriftBoost -= AddBoostFitness;
		_carController.OnDriftEnd -= CheckLevelBoost;
	}

	public void ResetAgent()
	{
		StartCoroutine(ResetCoroutine());
	}

	private IEnumerator ResetCoroutine()
	{
		yield return new WaitForFixedUpdate();
    
		transform.position = Vector3.zero;
		transform.rotation = Quaternion.identity;
		
		_carController.Reset();

		inputs = new float[_net._layers[0]];
		_fitness = 0;
		_totalCheckpointDist = 0;
		_nextCheckpoint = CheckpointManager.instance.FirstCheckpoint;
		_nextCheckpointDist = (_nextCheckpoint.position - transform.position).magnitude;
		numberWallTouch = 0;
		numberLevelBoost = 0;
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
		
		inputs[0] = RaySensor(pos+Vector3.up*0.2f, transform.forward, 4);
		inputs[1] =RaySensor(pos+Vector3.up*0.2f, transform.right, 1.5f);
		inputs[2] =RaySensor(pos+Vector3.up*0.2f, -transform.right, 1.5f);
		inputs[3] =RaySensor(pos+Vector3.up*0.2f, transform.forward + transform.right, 2);
		inputs[4] =RaySensor(pos+Vector3.up*0.2f, transform.forward - transform.right, 2);
		inputs[5] = 1;
		inputs[6] = _carController.DriftRatio;
		inputs[7] = _carController.CanDrift;
		//inputs[8] = _carController._currentDriftLevel / 3f;

		for (int i = 0; i < 5; i++)
		{
			_wallDetect[i] = inputs[i];
		}
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
		_carController.IsDrifing = Mathf.Abs(_net._neurons[^1][2]) > 0.8f;
	}

	private float _driftDuringGeneration = 0;
	private float elapsedTimeSpaming = 0;
	private bool isSpamming = false;
	private Transform tr;
	private float numberWallTouch = 0;
	public float numberLevelBoost;
	
	private void FitnessUpdate()
	{
		#region Distance
		_distanceTraveled = _totalCheckpointDist + (_nextCheckpointDist - (_nextCheckpoint.position - transform.position).magnitude);

		if (_fitness < _distanceTraveled)
		{
			_fitness = _distanceTraveled - numberWallTouch;
		}

		#endregion

		if (elapsedTimeSpaming <= 0)
		{
			elapsedTimeSpaming += Time.deltaTime;

			isSpamming = true;

			if (elapsedTimeSpaming >= 2)
				isSpamming = false;
		}

		if(!_carController.IsDriftPowerFull)
			_fitness += _carController.DriftAmount * 0.01f;

		float IsDriftFitness = 0;
		if (_carController.IsDrifing)
		{
			IsDriftFitness += 2;
			_driftDuringGeneration += 1*Time.deltaTime;
		}
		if(_carController.RB.linearVelocity.magnitude < 5f)
		{
			IsDriftFitness -= 100f;
		}
		
		if(_carController.HorizontalInput < 0)
			IsDriftFitness -= 100f;
		
		_fitness += IsDriftFitness;

		foreach (float f in _wallDetect)
		{
			if (f >= 0.9f)
			{
				numberWallTouch += 1f;
			}
		}

		if (tr.position.y < -1)
			_fitness = -100;
	}

	public void FitnessEndGeneration()
	{
		if (_driftDuringGeneration < 1f)
		{
			_fitness -= 500f;
		}

		if (numberLevelBoost == 0)
			_fitness = 0;

		if (numberLevelBoost < 5)
			_fitness /= 2;

	}
	
	private void AddBoostFitness(float amount)
	{
		numberLevelBoost += _carController._currentDriftLevel;
		_fitness += amount;
	}
	
	private void CheckLevelBoost()
	{
		int level = _carController._currentDriftLevel;

		switch (level)
		{
			case 0: _fitness -= 500f;break;
			case 1: _fitness += 50f;break;
			case 2: _fitness += 150f;break;
			case 3: _fitness += 300f;break;
		}
	}
	
	private void CheckSpamming()
	{
		if (isSpamming)
		{
			_fitness -= 100f;
		}
		else
		{
			elapsedTimeSpaming = 0;
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
