using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DesktopGrabber : MonoBehaviour
{
    public Camera cam;                 // vacío = Camera.main
    public Key grabKey = Key.F;
    public float reach = 2.5f;         // qué tan lejos puedes agarrar
    public float holdDistance = 0.6f;  // a qué distancia de la cámara queda el objeto

    XRGrabInteractable held;
    Rigidbody heldRb;

    void Start()
    {
        if (!Application.isEditor) { enabled = false; return; }   // no hace nada en el Quest
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb[grabKey].wasPressedThisFrame) return;

        if (held == null) TryGrab();
        else { held = null; heldRb = null; }   // soltar
    }

    void TryGrab()
    {
        if (!Physics.Raycast(cam.transform.position, cam.transform.forward,
                out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore)) return;

        var g = hit.collider.GetComponentInParent<XRGrabInteractable>();
        if (g == null) return;

        // Si estaba encajada en un borne, la sacamos
        if (g.isSelected)
            g.interactionManager.CancelInteractableSelection((IXRSelectInteractable)g);

        held = g;
        heldRb = g.GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (held == null) return;

        if (held.isSelected) { held = null; heldRb = null; return; }  // un borne la tomó

        Vector3 target = cam.transform.position + cam.transform.forward * holdDistance;
        Vector3 v = (target - heldRb.position) / Time.fixedDeltaTime;
        heldRb.linearVelocity = Vector3.ClampMagnitude(v, 20f);
        heldRb.angularVelocity = Vector3.zero;
        heldRb.MoveRotation(Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f));
    }
}
