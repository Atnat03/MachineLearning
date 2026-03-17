using UnityEngine;

public class PlayerControllerInput : MonoBehaviour 
{
    [SerializeField] CarController _carController;

    void Update()
    {
        /*
        _carController._horizontalInput = Input.GetAxisRaw("Horizontal");
        _carController._verticalInput = Input.GetAxisRaw("Vertical");*/
        
        _carController.HorizontalInput = Input.GetAxis("Joystick");
        _carController.VerticalInput = Input.GetAxis("Vertical");
        
        _carController.IsBreaking = Input.GetButton("Submit");

        if (Input.GetButtonUp("Cancel"))
        {
            _carController.ApplyOntBoost();
        }
    }
}
