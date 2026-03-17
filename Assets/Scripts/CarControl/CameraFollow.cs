using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform Target
    {
        get => _target;
        set => _target = value;
    }

    #region Variables

    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _localPositionToMove = new Vector3(0, 5, -15);
    [SerializeField] private Vector3 _localPositionToLook = new Vector3(0, -1, -15);
    
    [SerializeField] private float _smoothMove = 0.02f;
    [SerializeField] private float _smoothRotation = 0.01f;
    
    Vector3 _wantedPosition;
    Quaternion _wantedRotation;

    #endregion

    #region Fonctions

    void LateUpdate()
    {
        if (_target == null) return;
        
        _wantedPosition = _target.TransformPoint(_localPositionToMove);
        _wantedPosition.y = _target.position.y + _localPositionToMove.y;
        
        _wantedRotation = Quaternion.LookRotation(_target.TransformPoint(_localPositionToLook) - transform.position);
        
        transform.position = Vector3.Lerp(transform.position, _wantedPosition, _smoothMove * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, _wantedRotation, _smoothRotation * Time.deltaTime);
    }

    #endregion
}