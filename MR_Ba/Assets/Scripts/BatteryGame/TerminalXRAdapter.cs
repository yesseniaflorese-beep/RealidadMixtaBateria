using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor), typeof(TerminalLogic))]
public class TerminalXRAdapter : MonoBehaviour
{
    public float ejectForce = 0.4f;

    XRSocketInteractor socket;
    TerminalLogic logic;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        logic = GetComponent<TerminalLogic>();
    }

    void OnEnable()
    {
        socket.selectEntered.AddListener(OnIn);
        socket.selectExited.AddListener(OnOut);
    }

    void OnDisable()
    {
        socket.selectEntered.RemoveListener(OnIn);
        socket.selectExited.RemoveListener(OnOut);
    }

    void OnIn(SelectEnterEventArgs a)
    {
        var item = a.interactableObject;
        var clamp = item.transform.GetComponent<ClampPolarity>();

        if (clamp != null && GameFlowManager.Playing && logic.TryConnect(clamp))
        {
            Lock(clamp);                 // conexión correcta: queda fija
            return;
        }
        StartCoroutine(Reject(item));    // incorrecta: sale despedida
    }

    void OnOut(SelectExitEventArgs a) => logic.Disconnect();

    // Sin colliders, las manos y el rayo ya no pueden agarrarla
    void Lock(ClampPolarity clamp)
    {
        foreach (var col in clamp.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    IEnumerator Reject(IXRSelectInteractable item)
    {
        yield return null;
        socket.socketActive = false;
        socket.interactionManager.SelectExit(socket, item);
        var rb = item.transform.GetComponent<Rigidbody>();
        if (rb) rb.AddForce(Vector3.up * ejectForce, ForceMode.Impulse);
        yield return new WaitForSeconds(1f);
        socket.socketActive = true;
    }
}