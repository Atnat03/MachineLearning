using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class KartController : MonoBehaviour
{
    #region Properties

    public float HorizontalInput { get => _horizontalInput; set => _horizontalInput = value; }
    public float VerticalInput { get => _verticalInput; set => _verticalInput = value; }
    
    #endregion
    
    #region Variables

    [Header("References")]
    private Rigidbody _rb;
    [SerializeField] private Transform _groundRayPoint;
    [SerializeField] private Transform _leftFrontW;
    [SerializeField] private Transform _rightFrontW;
    [SerializeField] private Transform _kartModel;
    [SerializeField] private Transform _kartNormal;

    [Header("Settings")]
    [SerializeField] private float _accelForce = 1200f;
    [SerializeField] private float _maxSpeed = 25f;
    [SerializeField] private float _turnForce = 120f;
    [SerializeField] private float _dragOnGround = 4f;
    [SerializeField] private float _gravityForce = 30f;
    [SerializeField] private float _sandMultiplicator = 0.5f;

    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private float _groundRayLength = 0.8f;
    [SerializeField] private float _maxWheelTurn = 25f;

    [Header("Boost Collectibles")]
    [SerializeField] private float _boostForce = 15f;
    [SerializeField] private float _boostDuration = 1.5f;
    [SerializeField] private ParticleSystem[] _boostParticleParent;
    
    [Header("Drift System - Mario Kart Style")]
    [SerializeField] private bool _isDrifting = false;
    [SerializeField] private int _driftDirection = 0;
    [SerializeField] private float _driftPower = 0f;
    
    [SerializeField] private float _driftLevel1Threshold = 50f;
    [SerializeField] private float _driftLevel2Threshold = 100f;
    [SerializeField] private float _driftLevel3Threshold = 150f;
    
    [SerializeField] private float _driftBoostLevel1 = 8f;
    [SerializeField] private float _driftBoostLevel2 = 12f;
    [SerializeField] private float _driftBoostLevel3 = 18f;
    
    private int _currentDriftLevel = 0;
    
    [SerializeField] private float _driftSteerMultiplier = 1.5f;
    [SerializeField] private float _driftCounterSteerRange = 0.3f;
    
    [SerializeField] private Color[] _driftColors = new Color[3];
    [SerializeField] private ParticleSystem[] _driftParticles;
    [SerializeField] private ParticleSystem[] _driftFlashParticles;
    
    // Animation du kart
    [SerializeField] private float _kartTiltAmount = 15f;
    [SerializeField] private float _driftKartTiltAmount = 25f;
    
    [Header("Boost Visual Effects")]
    [SerializeField] private ParticleSystem[] _miniTurboParticles;
    [SerializeField] private GameObject[] _trailsTyre;
    [SerializeField] private GameObject[] _sandParticle;
    
    public bool isPlayerInput = true;
    
    private float _horizontalInput;
    private float _verticalInput;
    private bool _grounded;
    private bool _boosting = false;
    private RaycastHit _hitSand;

    #endregion

    #region Fonctions

    private void Start()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if(isPlayerInput)
        {
            _horizontalInput = Input.GetAxis("Horizontal");
            _verticalInput = Input.GetAxis("Vertical");
            
            // Démarrage du drift
            if (Input.GetKeyDown(KeyCode.LeftShift) && !_isDrifting && Mathf.Abs(_horizontalInput) > 0.1f && _grounded)
            {
                StartDrift();
            }

            // Pendant le drift
            if (_isDrifting)
            {
                AccumulateDriftPower();
                
                // Fin du drift
                if (Input.GetKeyUp(KeyCode.LeftShift))
                {
                    EndDrift();
                }
            }
        }

        UpdateWheels();
        UpdateKartVisuals();
    }

    private void FixedUpdate()
    {
        CheckGround();
        Move();
        Turn();
        LimitSpeed();
        CheckSand();
    }
    
    private void CheckSand()
    {
        if (_grounded && !_boosting)
        {
            bool isInSand = !Physics.Raycast(_groundRayPoint.position, -_groundRayPoint.up, out _hitSand, _groundRayLength,
                LayerMask.NameToLayer("Sand"));
            
            if (isInSand)
            {
                _rb.linearVelocity *= _sandMultiplicator;
            }
            
            foreach (GameObject g in _sandParticle)
            {
                g.SetActive(isInSand);
            }
        }
    }

    #region Movement

    void Move()
    {
        if (_grounded)
        {
            _rb.linearDamping = _dragOnGround;

            if (Mathf.Abs(_verticalInput) > 0.1f)
            {
                _rb.AddForce(transform.forward * _verticalInput * _accelForce);
            }
        }
        else
        {
            _rb.linearDamping = 0.1f;
            _rb.AddForce(Vector3.down * _gravityForce, ForceMode.Acceleration);
        }
    }

    void Turn()
    {
        if (!_grounded) return;

        if (Mathf.Abs(_horizontalInput) > 0.1f)
        {
            float turnAmount = _horizontalInput * _turnForce;
            
            if (_isDrifting)
            {
                float driftControl = RemapDriftControl(_horizontalInput);
                turnAmount = _driftDirection * _turnForce * _driftSteerMultiplier * driftControl;
            }
            
            float turn = turnAmount * Time.fixedDeltaTime;
            Quaternion rot = Quaternion.Euler(0f, turn, 0f);
            _rb.MoveRotation(_rb.rotation * rot);
        }
    }

    void LimitSpeed()
    {
        Vector3 flatVelocity = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);

        if (flatVelocity.magnitude > _maxSpeed)
        {
            Vector3 limited = flatVelocity.normalized * _maxSpeed;
            _rb.linearVelocity = new Vector3(limited.x, _rb.linearVelocity.y, limited.z);
        }
    }
    
    void CheckGround()
    {
        _grounded = Physics.Raycast(_groundRayPoint.position, -transform.up, _groundRayLength, _groundMask);
    }

    #endregion

    #region Drift

    void StartDrift()
    {
        _isDrifting = true;
        _driftDirection = _horizontalInput > 0 ? 1 : -1;
        _driftPower = 0f;
        _currentDriftLevel = 0;
        
        // Démarrage des particules de drift
        if (_driftParticles != null && _driftParticles.Length > 0)
        {
            foreach (ParticleSystem p in _driftParticles)
            {
                var main = p.main;
                main.startColor = Color.clear;
                p.Play();
            }
        }

        foreach (GameObject trail in _trailsTyre)
        {
            trail.SetActive(true);
        }
        
        // Animation hop
        if (_kartNormal != null)
        {
            StartCoroutine(DriftHopAnimation());
        }
        
        Debug.Log("Drift started! Direction: " + (_driftDirection == 1 ? "Right" : "Left"));
    }

    void AccumulateDriftPower()
    {
        if (!_isDrifting) return;

        float powerGain;
        
        if (_driftDirection == 1)
        {
            powerGain = Remap(_horizontalInput, -1f, 1f, 1f, _driftCounterSteerRange);
        }
        else
        {
            powerGain = Remap(_horizontalInput, -1f, 1f, _driftCounterSteerRange, 1f);
        }
        
        _driftPower += powerGain * Time.deltaTime * 50f;
        
        UpdateDriftLevel();
    }

    void UpdateDriftLevel()
    {
        int previousLevel = _currentDriftLevel;
        
        if (_driftPower >= _driftLevel3Threshold)
        {
            _currentDriftLevel = 3;
        }
        else if (_driftPower >= _driftLevel2Threshold)
        {
            _currentDriftLevel = 2;
        }
        else if (_driftPower >= _driftLevel1Threshold)
        {
            _currentDriftLevel = 1;
        }
        else
        {
            _currentDriftLevel = 0;
        }
        
        if (_currentDriftLevel > previousLevel && _currentDriftLevel > 0)
        {
            OnDriftLevelUp(_currentDriftLevel);
        }
        
        UpdateDriftParticleColor();
    }

    void OnDriftLevelUp(int level)
    {
        Debug.Log($"Drift Level UP! Now at level {level}");
        
        if (_driftFlashParticles != null && _driftFlashParticles.Length > 0)
        {
            Color flashColor = _driftColors[Mathf.Clamp(level - 1, 0, 2)];
            
            foreach (ParticleSystem p in _driftFlashParticles)
            {
                var main = p.main;
                main.startColor = flashColor;
                p.Play();
            }
        }
    }

    void UpdateDriftParticleColor()
    {
        if (_driftParticles == null || _driftParticles.Length == 0) return;
        
        Color currentColor = _currentDriftLevel > 0 
            ? _driftColors[Mathf.Clamp(_currentDriftLevel - 1, 0, 2)] 
            : Color.clear;
        
        foreach (ParticleSystem p in _driftParticles)
        {
            var main = p.main;
            main.startColor = currentColor;
        }
    }

    void EndDrift()
    {
        _isDrifting = false;
        
        // Application du Mini-Turbo selon le niveau
        if (_currentDriftLevel > 0 && !_boosting)
        {
            float boostForce = GetDriftBoostForce(_currentDriftLevel);
            StartCoroutine(DriftBoostCoroutine(boostForce, _currentDriftLevel));
        }
        
        // Arrêt des particules de drift
        if (_driftParticles != null)
        {
            foreach (ParticleSystem p in _driftParticles)
            {
                var main = p.main;
                main.startColor = Color.clear;
                p.Stop();
            }
        }
        
        foreach (GameObject trail in _trailsTyre)
        {
            trail.SetActive(false);
            trail.GetComponent<TrailRenderer>().Clear();
        }
        
        // Reset de l'inclinaison du kart
        if (_kartNormal != null)
        {
            StartCoroutine(ResetKartRotation());
        }
        
        // Reset
        _driftPower = 0f;
        _currentDriftLevel = 0;
        _driftDirection = 0;
        
        Debug.Log("Drift ended!");
    }

    float GetDriftBoostForce(int level)
    {
        switch (level)
        {
            case 1: return _driftBoostLevel1;
            case 2: return _driftBoostLevel2;
            case 3: return _driftBoostLevel3;
            default: return 0f;
        }
    }

    IEnumerator DriftBoostCoroutine(float boostForce, int level)
    {
        _rb.AddForce(transform.forward * boostForce, ForceMode.Impulse);
        
        if (_miniTurboParticles != null && _miniTurboParticles.Length > 0)
        {
            foreach (ParticleSystem p in _miniTurboParticles)
            {
                p.Play();
            }
        }
        
        float boostDuration = 0.5f + (level * 0.3f);
        
        yield return new WaitForSeconds(boostDuration * 0.5f);
        
        float elapsed = 0f;
        float decelerationTime = boostDuration * 0.5f;
        Vector3 startVelocity = _rb.linearVelocity;
        
        while (elapsed < decelerationTime)
        {
            elapsed += Time.fixedDeltaTime;
            float t = elapsed / decelerationTime;
            _rb.linearVelocity = Vector3.Lerp(startVelocity, startVelocity * 0.5f, t);
            yield return new WaitForFixedUpdate();
        }
    }

    float RemapDriftControl(float input)
    {
        if (_driftDirection == 1)
        {
            return Remap(input, -1f, 1f, 0.5f, 2f);
        }
        else
        {
            return Remap(input, -1f, 1f, 2f, 0.5f);
        }
    }

    float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        return (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
    }

    #endregion
    
    #region Visuals

    void UpdateWheels()
    {
        if (_leftFrontW == null) return;

        float wheelAngle = _horizontalInput * _maxWheelTurn;
        
        if (_isDrifting)
        {
            wheelAngle = _driftDirection * _maxWheelTurn * 1.5f;
        }

        _leftFrontW.localRotation = Quaternion.Euler(
            _leftFrontW.localRotation.eulerAngles.x,
            wheelAngle - 180,
            _leftFrontW.localRotation.eulerAngles.z
        );

        _rightFrontW.localRotation = Quaternion.Euler(
            _rightFrontW.localRotation.eulerAngles.x,
            wheelAngle,
            _rightFrontW.localRotation.eulerAngles.z
        );
    }

    void UpdateKartVisuals()
    {
        if (_kartModel == null) return;
        
        if (!_isDrifting)
        {
            float targetYAngle = _horizontalInput * _kartTiltAmount;
            targetYAngle = Mathf.Clamp(targetYAngle, -_kartTiltAmount, _kartTiltAmount);
            
            Vector3 currentEuler = _kartModel.localEulerAngles;
            float currentY = currentEuler.y > 180 ? currentEuler.y - 360 : currentEuler.y;
            float newY = Mathf.Lerp(currentY, targetYAngle, Time.deltaTime * 5f);
            
            _kartModel.localEulerAngles = new Vector3(currentEuler.x, newY, currentEuler.z);
            
            if (_kartNormal != null)
            {
                _kartNormal.localRotation = Quaternion.Slerp(_kartNormal.localRotation, Quaternion.identity, Time.deltaTime * 8f);
            }
        }
        else
        {
            float driftControl = RemapDriftControl(_horizontalInput);
            float targetAngle = (driftControl * _driftKartTiltAmount) * _driftDirection;
            
            targetAngle = Mathf.Clamp(targetAngle, -60f, 60f);
            
            if (_kartNormal != null)
            {
                Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);
                _kartNormal.localRotation = Quaternion.Slerp(_kartNormal.localRotation, targetRotation, Time.deltaTime * 8f);
            }
            
            Vector3 modelEuler = _kartModel.localEulerAngles;
            _kartModel.localEulerAngles = new Vector3(modelEuler.x, 0, modelEuler.z);
        }
    }

    IEnumerator DriftHopAnimation()
    {
        if (_kartNormal == null) yield break;
        
        Vector3 startPos = _kartNormal.localPosition;
        Vector3 hopPos = startPos + Vector3.up * 0.2f;
        float duration = 0.3f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curve = Mathf.Sin(t * Mathf.PI);
            _kartNormal.localPosition = Vector3.Lerp(startPos, hopPos, curve);
            yield return null;
        }
        
        _kartNormal.localPosition = startPos;
    }

    IEnumerator ResetKartRotation()
    {
        if (_kartNormal == null) yield break;
        
        float duration = 0.5f;
        float elapsed = 0f;
        Quaternion startRot = _kartNormal.localRotation;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _kartNormal.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, t);
            yield return null;
        }
        
        _kartNormal.localRotation = Quaternion.identity;
    }

    #endregion
    
    public void Reset()
    {
        _horizontalInput = 0;
        _verticalInput = 0;
        _isDrifting = false;
        _driftPower = 0;
        _currentDriftLevel = 0;
		
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        
        if (_kartNormal != null)
            _kartNormal.localRotation = Quaternion.identity;
        
        if (_kartModel != null)
            _kartModel.localEulerAngles = Vector3.zero;
    }
    #endregion

}