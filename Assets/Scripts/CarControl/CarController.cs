using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController : MonoBehaviour
{
	#region Properties

	public float HorizontalInput { get => _horizontalInput; set => _horizontalInput = value; }
	public float VerticalInput { get => _verticalInput; set => _verticalInput = value; }
	
	public bool IsBreaking { get => _isBreaking; set => _isBreaking = value; }

	#endregion


	#region Variables

	Rigidbody _rigidbody;
	
	[Header("Movement")]
	[SerializeField] private float _maxSteerAngle = 42;
	[SerializeField] private float _motorForce = 800;
	[SerializeField] private float _breakForce = 800;
	[SerializeField] private bool _isBreaking = false;
	[SerializeField] private float _sandMultiplicator = 0.25f;
	[SerializeField] private float _sandMaxSpeed = 10f;
	
	[SerializeField] Transform _centerOfMass;
	
	[Header("Wheel Collider")]
	[SerializeField] WheelCollider _leftBackWheelCollider;
	[SerializeField] WheelCollider _leftFrontWheelCollider;
	[SerializeField] WheelCollider _rightBackWheelCollider;
	[SerializeField] WheelCollider _rightFrontWheelCollider;
	
	[Header("Wheel Transform")]
	[SerializeField] Transform _leftBackWheelTransform;
	[SerializeField] Transform _leftFrontWheelTransform;
	[SerializeField] Transform _rightBackWheelTransform;
	[SerializeField] Transform _rightFrontWheelTransform;
	
	[SerializeField] private float _boostForce;
	[SerializeField] private int _numberBoost = 3;
	[SerializeField] private int _currentBoost = 0;
	[SerializeField] private float _boostTime = 1.5f;
	[SerializeField] private bool _boosting = false;
	[SerializeField] private MushroomAnimation _boostUI;
	[SerializeField] private Transform _parentBoostUI;
	[SerializeField] private ParticleSystem[] _boostParticleParent;
	private List<MushroomAnimation> _mushrooms;
	
	private float _horizontalInput;
	private float _verticalInput;

	private Vector3 pos;
	private Quaternion rot;
	
	#endregion

	#region Fonctions

	private void Awake()
	{
		_rigidbody = GetComponent<Rigidbody>();
		
		_rigidbody.centerOfMass = _centerOfMass.position;
		
		_mushrooms = new List<MushroomAnimation>();

		for (int i = -1; i < _numberBoost-1; i++)
		{
			MushroomAnimation m = Instantiate(_boostUI, _parentBoostUI.position + (Vector3.right * (i*0.4f)), Quaternion.identity);
			m.transform.SetParent(_parentBoostUI);
			_mushrooms.Add(m);
		}
	}

	#region Visual
	private void Update()
	{
		UpdateWheelsModels();
	}

	private void UpdateWheelsModels()
	{
		UpdateSoloWheel(_leftBackWheelCollider, _leftFrontWheelTransform);
		UpdateSoloWheel(_leftFrontWheelCollider, _leftBackWheelTransform);
		UpdateSoloWheel(_rightBackWheelCollider, _rightFrontWheelTransform);
		UpdateSoloWheel(_rightFrontWheelCollider, _rightBackWheelTransform);
	}

	void UpdateSoloWheel(WheelCollider wheelCollider, Transform wheelTransform)
	{
		wheelCollider.GetWorldPose(out pos, out rot);
		
		wheelTransform.position = pos;
		wheelTransform.rotation = rot;
	}

	#endregion

	#region Physic

	private void FixedUpdate()
	{
		Steering();
		ApplyForce();
		AddBreaking();
		ApplySurfaceDrag();
	}

	private void ApplyForce()
	{
		if (_boosting) return;
		
		_leftBackWheelCollider.motorTorque = _verticalInput * _motorForce;
		_rightBackWheelCollider.motorTorque = _verticalInput * _motorForce;
	}
	
	private void ApplySurfaceDrag()
	{
		if (_boosting) return;
		
		WheelHit hit;
		if (_leftBackWheelCollider.GetGroundHit(out hit))
		{
			if (hit.collider.CompareTag("Sand"))
			{
				if (_rigidbody.linearVelocity.magnitude > _sandMaxSpeed)
				{
					_rigidbody.linearVelocity = _rigidbody.linearVelocity.normalized * _sandMaxSpeed;
				}
			}
		}
	}

	public void AddBreaking()
	{
		if (_isBreaking)
		{
			_leftBackWheelCollider.brakeTorque = _breakForce;
			_rightBackWheelCollider.brakeTorque = _breakForce;
		}
		else
		{
			_leftBackWheelCollider.brakeTorque = 0;
			_rightBackWheelCollider.brakeTorque = 0;
		}
	}
	
	private void Steering()
	{
		_leftFrontWheelCollider.steerAngle = _horizontalInput * _maxSteerAngle;
		_rightFrontWheelCollider.steerAngle = _horizontalInput * _maxSteerAngle;
	}
	
	public void ApplyOntBoost()
	{
		if (_boosting) return;

		if (_currentBoost >= _numberBoost) return;
		
		StartCoroutine(Boosting());
		_mushrooms[_currentBoost].Use();
	}

	IEnumerator Boosting()
	{
		_boosting = true;

		_rigidbody.AddForce(transform.forward * _boostForce, ForceMode.Impulse);

		foreach (ParticleSystem p in  _boostParticleParent)
		{
			p.Play();
		}

		float time = _boostTime / 2;
		
		yield return new WaitForSeconds(time);

		float elapsed = 0f;
		float decelerationTime = time;
		Vector3 startVelocity = _rigidbody.linearVelocity;

		while (elapsed < decelerationTime)
		{
			elapsed += Time.fixedDeltaTime;
			float t = elapsed / decelerationTime;
			_rigidbody.linearVelocity = Vector3.Lerp(startVelocity, startVelocity * 0.3f, t);
			yield return new WaitForFixedUpdate();
		}

		_currentBoost++;

		_boosting = false;
	}

	#endregion

	public void Reset()
	{
		_horizontalInput = 0;
		_verticalInput = 0;
		
		_rigidbody.linearVelocity = Vector3.zero;
		_rigidbody.angularVelocity = Vector3.zero;
	}

	#endregion
}
