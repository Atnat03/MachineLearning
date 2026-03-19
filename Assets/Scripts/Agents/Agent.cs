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
	private float[] _voidDetect = new float[5];
	public AnimationCurve curveByFitness;

	
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
		
		inputs[0] = RaySensor(pos+Vector3.up*0.2f, 
			transform.forward, 4);
		inputs[1] =RaySensor(pos+Vector3.up*0.2f, 
			transform.right, 1.5f);
		inputs[2] =RaySensor(pos+Vector3.up*0.2f, 
			-transform.right, 1.5f);
		inputs[3] =RaySensor(pos+Vector3.up*0.2f, 
			transform.forward + transform.right, 2);
		inputs[4] =RaySensor(pos+Vector3.up*0.2f, 
			transform.forward - transform.right, 2);
		inputs[5] = 1;
		inputs[6] = _carController.DriftRatio;
		inputs[7] = _carController.CanDrift;

		inputs[8] = GroundSensor(transform.forward * 4f, 1f, 3f);
		inputs[9] = GroundSensor(transform.right * 3f, 1f, 3f);
		inputs[10] = GroundSensor(-transform.right * 3f, 1f, 3f);
		
		//Unuse...
		inputs[11] = GroundSensor(transform.forward + transform.right * 3f, 1f, 3f);
		inputs[12] = GroundSensor(transform.forward - transform.right * 3f, 1f, 3f);
		
		_voidDetect[0] = inputs[8];
		_voidDetect[1] = inputs[9];
		_voidDetect[2] = inputs[10];
		_voidDetect[3] = inputs[11];
		_voidDetect[4] = inputs[12];

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
	
	float GroundSensor(Vector3 offset, float height, float maxDistance)
	{
		Vector3 origin = transform.position + offset + Vector3.up * height;

		if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, _layerMaskRay))
		{
			float value = 1 - (hit.distance / maxDistance);
			Debug.DrawRay(origin, Vector3.down * hit.distance, Color.Lerp(Color.red, Color.green, value));
			return value;
		}

		Debug.DrawRay(origin, Vector3.down * maxDistance, Color.red);
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

private void FitnessUpdate()
{
    #region Distance - BASE
    
    _distanceTraveled = _totalCheckpointDist + (_nextCheckpointDist - (_nextCheckpoint.position - transform.position).magnitude);
    if (_fitness < _distanceTraveled)
    {
        _fitness = _distanceTraveled;
    }

    #endregion

    #region Vitesse et Direction
    
    float speed = _carController.RB.linearVelocity.magnitude;
    Vector3 velocity = _carController.RB.linearVelocity.normalized;
    float forwardDot = Vector3.Dot(transform.forward, velocity);
    
    if (_carController.VerticalInput < -0.2f)
    {
        _fitness -= 500f * Time.deltaTime;
    }
    
    if (forwardDot < -0.2f && speed > 1f)
    {
        _fitness -= 800f * Time.deltaTime;
    }
    
    if (forwardDot > 0.5f && speed > 5f && !drifting)
    {
        _fitness += 8f * Time.deltaTime;
    }
    
    if (speed < 2f && !drifting)
    {
        _fitness -= 60f * Time.deltaTime;
    }

    #endregion

    #region Drift
	
    if (drifting && speed > 5f && forwardDot > 0.3f)
    {
	    _fitness += 60f * Time.deltaTime;
	    _driftDuringGeneration += Time.deltaTime;
    
	    if (Mathf.Abs(_carController.HorizontalInput) > 0.3f)
	    {
		    _fitness += 50f * Time.deltaTime;
	    }
    }
    else if (drifting && (speed < 5f || forwardDot < 0.3f))
    {
	    _fitness -= 5f * Time.deltaTime;
    }

    if (drifting && speed < 3f)
    {
	    _fitness -= 50f * Time.deltaTime; 
    }

    if (!_carController.IsDriftPowerFull && speed > 5f && forwardDot > 0.3f)
    {
	    _fitness += _carController.DriftAmount * 0.3f * Time.deltaTime;
    }

    #endregion

    #region Détection VIDE
    
    bool voidAhead = _voidDetect[0] < 0.3f;
    bool voidRight = _voidDetect[1] < 0.3f;
    bool voidLeft = _voidDetect[2] < 0.3f;
    
    if (voidAhead && speed > 3f && forwardDot > 0)
    {
        _fitness -= 200f * Time.deltaTime;
        
        if (_carController.VerticalInput > 0.5f)
        {
            _fitness -= 300f * Time.deltaTime;
        }
    }
    
    if ((voidRight && _carController.HorizontalInput > 0.5f) || 
        (voidLeft && _carController.HorizontalInput < -0.5f))
    {
        _fitness -= 150f * Time.deltaTime;
    }
    
    if (_voidDetect[0] > 0.5f && _voidDetect[1] > 0.3f && _voidDetect[2] > 0.3f)
    {
        _fitness += 3f * Time.deltaTime;
    }

    #endregion

    #region Pénalités MURS
    
    bool isTouchingWall = false;
    bool isTouchingLeftWall = false;
    
    if (_wallDetect[2] >= 0.5f)
    { 
        isTouchingLeftWall = true;
        isTouchingWall = true;
        _fitness -= 200f * Time.deltaTime;
    }
    
    for (int i = 0; i < 5; i++)
    {
        if (i == 2) continue; 
        
        if (_wallDetect[i] >= 0.7f)
        { 
            isTouchingWall = true;
            _fitness -= 150f * Time.deltaTime;
        }
    }
    
    if (isTouchingWall && !wasTouchingWallLastFrame)
    {
        numberWallTouch += 1;
        
        if (isTouchingLeftWall)
        {
            _fitness -= 600f;
            _distanceTraveled -= 50f;
        }
        else
        {
	        _fitness -= 400f;
	        _distanceTraveled -= 30f;
        }
    }
    
    if (_wallDetect[2] >= 0.5f && _carController.HorizontalInput < -0.1f)
    {
        _fitness -= 150f * Time.deltaTime; 
    }
    
    if (drifting && _wallDetect[2] >= 0.6f)
    {
        _fitness -= 300f * Time.deltaTime; 
    }
    
    wasTouchingWallLastFrame = isTouchingWall;
    
    if (elapsedTimeSpaming > 0)
    {
        elapsedTimeSpaming -= Time.deltaTime;
    }
    
    if (tr.position.y < -4)
    {
        _fitness = -2000;
    }
    
    #endregion
}

public void FitnessEndGeneration()
{
    if (_driftDuringGeneration < 2f && _fitness > 300f)
    {
        _fitness -= 500f;
    }
    else if (_driftDuringGeneration < 5f && _fitness > 500f)
    {
        _fitness -= 200f;
    }
    
    if (numberWallTouch > 3f)
    {
        _fitness -= 1500f;
    }
    else if (numberWallTouch > 1f)
    {
        _fitness -= 600f;
    }

    if (numberLevelBoost == 0 || _driftDuringGeneration < 2f)
    {
        _fitness -= 1500f;
    }
    else if (numberLevelBoost < 3 && _fitness > 400f)
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
			_fitness += 100f;
			break;
		case 2: 
			_fitness += 200f;
			break;
		case 3: 
			_fitness += 400f;
			break;
	}

	drifting = false;
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
    _fitness += 300f;
    
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