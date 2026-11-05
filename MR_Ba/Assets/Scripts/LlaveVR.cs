using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Va en QuitaBirlos (junto a XR Grab Interactable y LlaveBirlos)
[RequireComponent(typeof(XRGrabInteractable))]
public class LlaveVR : MonoBehaviour
{
    // Aviso de la primera vez que se agarra la llave: el GestorMinijuego empieza el tiempo
    public static event Action OnLlaveAgarrada;

    public Transform punta;          // objeto vacio en la cabeza de la llave
    public float radio = 0.06f;      // que tan cerca de un birlo debe estar la punta
    public bool mostrarMensajes = true;

    private XRGrabInteractable grab;
    private InputAction gatilloAccion;
    private InputAction botonAccion;
    private bool avisoSinBirlo;
    private bool yaAgarrada;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        // Gatillo de cualquiera de los dos controles
        gatilloAccion = new InputAction(type: InputActionType.Button);
        gatilloAccion.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
        gatilloAccion.AddBinding("<XRController>{RightHand}/{TriggerButton}");

        // Boton A / X de cualquiera de los dos controles (apretar, opcional)
        botonAccion = new InputAction(type: InputActionType.Button);
        botonAccion.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
        botonAccion.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
    }

    void OnEnable()
    {
        gatilloAccion.Enable();
        botonAccion.Enable();
    }

    void OnDisable()
    {
        gatilloAccion.Disable();
        botonAccion.Disable();
    }

    void OnDestroy()
    {
        gatilloAccion.Dispose();
        botonAccion.Dispose();
    }

    void Update()
    {
        if (grab.isSelected && !yaAgarrada)
        {
            yaAgarrada = true;
            OnLlaveAgarrada?.Invoke();
        }

        // Solo funciona si la llave esta agarrada
        if (!grab.isSelected || punta == null) return;

        bool gatillo = gatilloAccion.IsPressed();
        bool boton = botonAccion.IsPressed();

        if (!gatillo && !boton)
        {
            avisoSinBirlo = false;
            return;
        }

        Birlo birlo = BuscarBirlo();

        if (birlo == null)
        {
            if (mostrarMensajes && !avisoSinBirlo)
            {
                Debug.Log("LlaveVR: boton apretado, pero no hay ningun birlo en la punta.");
                avisoSinBirlo = true;
            }
            return;
        }

        avisoSinBirlo = false;

        // GATILLO: afloja si el birlo esta en la llanta vieja, aprieta si ya esta colocado en la nueva
        if (gatillo)
        {
            if (birlo.estado == Birlo.Estado.EnRueda) birlo.Girar(-1f);
            else if (birlo.estado == Birlo.Estado.Colocado) birlo.Girar(1f);
        }

        // Boton A/X: siempre aprieta
        if (boton) birlo.Girar(1f);
    }

    Birlo BuscarBirlo()
    {
        Collider[] cercanos = Physics.OverlapSphere(punta.position, radio);

        foreach (Collider c in cercanos)
        {
            Birlo b = c.GetComponentInParent<Birlo>();
            if (b != null && (b.estado == Birlo.Estado.EnRueda || b.estado == Birlo.Estado.Colocado)) return b;
        }
        return null;
    }

    // Para ver la esfera de la punta en la Scene
    void OnDrawGizmosSelected()
    {
        if (punta == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(punta.position, radio);
    }
}