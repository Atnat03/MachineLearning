using System.Collections;
using System.Collections.Generic;
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

    [Header("Settings")]
    [SerializeField] private float _accelForce = 1200f;
    [SerializeField] private float _maxSpeed = 25f;
    [SerializeField] private float _turnForce = 120f;
    [SerializeField] private float _dragOnGround = 4f;
    [SerializeField] private float _gravityForce = 30f;

    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private float _groundRayLength = 0.8f;
    [SerializeField] private float _maxWheelTurn = 25f;

    [Header("Boost")]
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
    private bool _grounded;

    #endregion

    #region Fonction

    private void Start()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();
        
        _mushrooms = new List<MushroomAnimation>();

        for (int i = -1; i < _numberBoost-1; i++)
        {
            MushroomAnimation m = Instantiate(_boostUI, _parentBoostUI.position + (Vector3.right * (i*0.4f)), Quaternion.identity);
            m.transform.SetParent(_parentBoostUI);
            _mushrooms.Add(m);
        }
    }

    private void Update()
    {
        /*_horizontalInput = Input.GetAxis("Horizontal");
        _verticalInput = Input.GetAxis("Vertical");

        if (Input.GetButtonDown("Jump"))
        {
            ApplyBoost();
        }*/

        UpdateWheels();
    }

    private void FixedUpdate()
    {
        CheckGround();

        Move();
        Turn();
        LimitSpeed();
    }
    
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
            float turn = _horizontalInput * _turnForce * Time.fixedDeltaTime;

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
    
    void UpdateWheels()
    {
        if (_leftFrontW == null) return;

        _leftFrontW.localRotation = Quaternion.Euler(
            _leftFrontW.localRotation.eulerAngles.x,
            (_horizontalInput * _maxWheelTurn) - 180,
            _leftFrontW.localRotation.eulerAngles.z
        );

        _rightFrontW.localRotation = Quaternion.Euler(
            _rightFrontW.localRotation.eulerAngles.x,
            _horizontalInput * _maxWheelTurn,
            _rightFrontW.localRotation.eulerAngles.z
        );
    }
    
    public void ApplyBoost()
    {
        if (_boosting) return;

        if (_currentBoost >= _numberBoost) return;
		
        StartCoroutine(Boosting());
        _mushrooms[_currentBoost].Use();
    }

    IEnumerator Boosting()
    {
        _boosting = true;

        _rb.AddForce(transform.forward * _boostForce, ForceMode.Impulse);

        foreach (ParticleSystem p in  _boostParticleParent)
        {
            p.Play();
        }

        float time = _boostTime / 2;
		
        yield return new WaitForSeconds(time);

        float elapsed = 0f;
        float decelerationTime = time;
        Vector3 startVelocity = _rb.linearVelocity;

        while (elapsed < decelerationTime)
        {
            elapsed += Time.fixedDeltaTime;
            float t = elapsed / decelerationTime;
            _rb.linearVelocity = Vector3.Lerp(startVelocity, startVelocity * 0.3f, t);
            yield return new WaitForFixedUpdate();
        }

        _currentBoost++;

        _boosting = false;
    }
    
    public void Reset()
    {
        _horizontalInput = 0;
        _verticalInput = 0;
		
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }
    
    #endregion

}