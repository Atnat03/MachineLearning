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
		_driftDuringGeneration = 0;
		drifting = false;
		elapsedTimeSpaming = 0;
		isSpamming = false;
		wasTouchingWallLastFrame = false;
		_straightLineDriftTime = 0f;

		// Reset anti-stagnation
		_lastProgressCheckTime = 0f;
		_distanceTraveledAtLastCheck = 0f;
		_stagnationPenaltyAccumulator = 0f;
		_spawnAreaTime = 0f;
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
		inputs[1] = RaySensor(pos+Vector3.up*0.2f, transform.right, 1.5f);
		inputs[2] = RaySensor(pos+Vector3.up*0.2f, -transform.right, 1.5f);
		inputs[3] = RaySensor(pos+Vector3.up*0.2f, transform.forward + transform.right, 2);
		inputs[4] = RaySensor(pos+Vector3.up*0.2f, transform.forward - transform.right, 2);
		inputs[5] = 1;
		inputs[6] = _carController.DriftRatio;
		inputs[7] = _carController.CanDrift;

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
			Color rayColor = Color.Lerp(Color.red, Color.green, 1 - hit.distance / realLenght);
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
	private bool drifting = false;
	private bool wasTouchingWallLastFrame = false;
	private float _straightLineDriftTime = 0f;

	// --- Anti-stagnation ---
	private float _lastProgressCheckTime = 0f;       // Temps du dernier check de progression
	private float _distanceTraveledAtLastCheck = 0f;  // Distance au dernier check
	private float _stagnationPenaltyAccumulator = 0f; // Pénalité cumulée croissante
	private const float PROGRESS_CHECK_INTERVAL = 3f; // Vérifie toutes les 3 secondes
	private const float MIN_PROGRESS_REQUIRED = 2f;   // Doit avancer d'au moins 2 unités

	// --- Anti-spawn-loop ---
	private float _spawnAreaTime = 0f;                // Temps passé près du spawn
	private const float SPAWN_RADIUS = 10f;           // Rayon de la zone spawn
	private const float MAX_SPAWN_TIME = 5f;          // Max 5s toléré près du spawn

	private void FitnessUpdate()
	{
		#region Distance - BASE IMPORTANTE
		
		_distanceTraveled = _totalCheckpointDist + (_nextCheckpointDist - (_nextCheckpoint.position - transform.position).magnitude);

		if (_fitness < _distanceTraveled * 0.8f)
		{
			_fitness = _distanceTraveled * 0.8f;
		}

		#endregion

		#region Anti-Stagnation (tourne sur soi-même / reste au même endroit)

		_lastProgressCheckTime += Time.deltaTime;

		if (_lastProgressCheckTime >= PROGRESS_CHECK_INTERVAL)
		{
			float progressMade = _distanceTraveled - _distanceTraveledAtLastCheck;

			if (progressMade < MIN_PROGRESS_REQUIRED)
			{
				// L'IA n'avance pas assez : pénalité croissante à chaque check raté
				_stagnationPenaltyAccumulator += 300f;
				_fitness -= _stagnationPenaltyAccumulator;
			}
			else
			{
				// L'IA avance bien : on réinitialise l'accumulateur de pénalité
				_stagnationPenaltyAccumulator = Mathf.Max(0f, _stagnationPenaltyAccumulator - 150f);
			}

			_distanceTraveledAtLastCheck = _distanceTraveled;
			_lastProgressCheckTime = 0f;
		}

		// Pénalité passive continue si déjà en stagnation confirmée
		if (_stagnationPenaltyAccumulator > 0f)
		{
			_fitness -= _stagnationPenaltyAccumulator * 0.1f * Time.deltaTime;
		}

		#endregion

		#region Anti-Spawn-Loop (reste près du point de départ)

		// Si l'IA est encore proche du spawn après les premières secondes
		float distToSpawn = transform.position.magnitude; // spawn = Vector3.zero

		if (distToSpawn < SPAWN_RADIUS)
		{
			_spawnAreaTime += Time.deltaTime;

			if (_spawnAreaTime > MAX_SPAWN_TIME)
			{
				// Pénalité croissante et rapide : l'IA DOIT s'éloigner du spawn
				float spawnPenalty = (_spawnAreaTime - MAX_SPAWN_TIME) * 80f;
				_fitness -= spawnPenalty * Time.deltaTime;
			}
		}
		else
		{
			// L'IA s'est éloignée : on réduit progressivement le compteur
			_spawnAreaTime = Mathf.Max(0f, _spawnAreaTime - Time.deltaTime * 2f);
		}

		#endregion

		#region Vitesse et Direction
		
		float speed = _carController.RB.linearVelocity.magnitude;
		Vector3 forward = transform.forward;
		Vector3 velocity = _carController.RB.linearVelocity.normalized;
		
		float forwardDot = Vector3.Dot(forward, velocity);
		float horizontalAbs = Mathf.Abs(_carController.HorizontalInput);

		if (_carController.VerticalInput < -0.1f)
		{
			_fitness -= 300f * Time.deltaTime;

			if (drifting)
				_fitness -= 350f * Time.deltaTime;
		}
		
		if (forwardDot < -0.2f && speed > 1f)
		{
			_fitness -= 500f * Time.deltaTime;
		}
		
		if (forwardDot > 0.5f && speed > 5f)
		{
			_fitness += 3f * Time.deltaTime; 
		}
		
		if (speed < 2f && !(drifting && speed > 1f))
		{
			_fitness -= 20f * Time.deltaTime;
		}

		#endregion

		#region Drift - Contextuel (virage vs ligne droite)

		bool isTurning = horizontalAbs > 0.35f;

		if (drifting && speed > 5f && forwardDot > 0.3f)
		{
			if (isTurning)
			{
				// === BON DRIFT : virage + bonne vitesse + bonne orientation ===
				_fitness += 12f * Time.deltaTime;
				_driftDuringGeneration += 1f * Time.deltaTime;

				float driftBonus = Mathf.Min(_driftDuringGeneration * 0.3f, 6f);
				_fitness += driftBonus * Time.deltaTime;

				_straightLineDriftTime = 0f;

				if (horizontalAbs > 0.3f)
				{
					_fitness += 5f * Time.deltaTime;
				}
			}
			else
			{
				// === MAUVAIS DRIFT : ligne droite → pénalité croissante ===
				_straightLineDriftTime += Time.deltaTime;
				float straightPenalty = Mathf.Min(_straightLineDriftTime * 8f, 25f);
				_fitness -= straightPenalty * Time.deltaTime;
			}
		}
		else if (drifting && (speed < 5f || forwardDot < 0.3f))
		{
			_fitness -= 10f * Time.deltaTime;
		}
		else
		{
			_straightLineDriftTime = 0f;
		}
		
		if (!_carController.IsDriftPowerFull && speed > 5f && forwardDot > 0.3f)
		{
			_fitness += _carController.DriftAmount * 0.01f * Time.deltaTime;
		}

		#endregion

		#region Contrôle de direction
		
		Vector3 directionToCheckpoint = (_nextCheckpoint.position - transform.position).normalized;
		float angleToCheckpoint = Vector3.Dot(forward, directionToCheckpoint);
		
		if (angleToCheckpoint < -0.5f)
		{
			_fitness -= 30f * Time.deltaTime;
		}
		else if (angleToCheckpoint > 0.7f && speed > 5f)
		{
			_fitness += 2f * Time.deltaTime;
		}

		#endregion

		#region Pénalités MURS
		
		if (elapsedTimeSpaming > 0)
		{
			elapsedTimeSpaming -= Time.deltaTime;
		}

		bool touchingWall = false;
		int numberOfWallRaysHit = 0; 
		
		foreach (float f in _wallDetect)
		{
			if (f >= 0.9f)
			{
				touchingWall = true;
				numberOfWallRaysHit++;
			}
		}
		
		if (touchingWall)
		{
			_fitness -= 50f * Time.deltaTime;
			_fitness -= numberOfWallRaysHit * 20f * Time.deltaTime;
	        
			if (drifting)
			{
				_fitness -= 100f * Time.deltaTime;
			}
	        
			if (!wasTouchingWallLastFrame)
			{
				_fitness -= 150f;
			}
	        
			numberWallTouch += 1f * Time.deltaTime;
		}
		
		wasTouchingWallLastFrame = touchingWall;

		if (tr.position.y < -1)
		{
			_fitness = -1000;
		}
		
		#endregion
	}

	public void FitnessEndGeneration()
	{
		// Pénalité finale si l'IA n'a toujours pas quitté la zone spawn
		if (_distanceTraveled < MIN_PROGRESS_REQUIRED * 2f)
		{
			_fitness -= 2000f;
		}

		if (_driftDuringGeneration < 1f)
		{
			_fitness -= 600f;
		}
		else if (_driftDuringGeneration < 3f)
		{
			_fitness -= 200f;
		}
		
		if (numberWallTouch > 5f)
		{
			_fitness -= 500f;
		}
		else if (numberWallTouch > 2f)
		{
			_fitness -= 200f;
		}

		if (numberLevelBoost == 0)
		{
			_fitness -= 1000f;
		}
		else if (numberLevelBoost < 3)
		{
			_fitness *= 0.5f;
		}
	}

	private void AddBoostFitness(float amount)
	{
		numberLevelBoost += 1;
		_fitness += amount;
	}

	private void CheckLevelBoost()
	{
		int level = _carController._currentDriftLevel;

		switch (level)
		{
			case 0: 
				_fitness -= 300f;
				break;
			case 1: 
				_fitness += 25f;
				break;
			case 2: 
				_fitness += 75f;
				break;
			case 3: 
				_fitness += 150f;
				break;
		}

		drifting = false;
		_straightLineDriftTime = 0f;
	}

	private void CheckSpamming()
	{
		drifting = true;
		
		if (elapsedTimeSpaming > 0)
		{
			_fitness -= 100f;
			isSpamming = true;
		}
		else
		{
			isSpamming = false;
		}
		
		elapsedTimeSpaming = 2f;
	}

	public void CheckpointReach(Transform checkpoint)
	{
		_fitness += 200f;
		
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