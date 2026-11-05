using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    public Transform cameraTransform;
    public float speed = 2.5f;
    public float sensitivity = 0.1f;

    CharacterController cc;
    float pitch, vy;

    void Start() => cc = GetComponent<CharacterController>();

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (mouse.rightButton.isPressed)
        {
            Vector2 look = mouse.delta.ReadValue() * sensitivity;
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        Vector3 move = (transform.right * x + transform.forward * z).normalized * speed;

        vy = cc.isGrounded ? -1f : vy + Physics.gravity.y * Time.deltaTime;
        move.y = vy;
        cc.Move(move * Time.deltaTime);
    }
}