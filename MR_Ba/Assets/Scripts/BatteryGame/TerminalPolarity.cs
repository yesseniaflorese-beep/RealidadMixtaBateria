using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

//REVISA LA CONEXION DE LOS CABLES
[RequireComponent(typeof(XRSocketInteractor))]
public class TerminalPolarity : MonoBehaviour
{
    public Polarity polarity;
    public ParticleSystem sparks;
    public float penalty = 3f;
    public static int connectedCorrect;

    XRSocketInteractor socket;
    bool correct;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        connectedCorrect = 0;
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
        var clamp = a.interactableObject.transform.GetComponent<ClampPolarity>();
        if (clamp == null) { StartCoroutine(Reject(a.interactableObject, false)); return; }

        if (clamp.polarity == polarity)
        {
            correct = true;
            connectedCorrect++;
            Debug.Log($"Conexión correcta ({connectedCorrect}/4)");
            if (connectedCorrect >= 4) Debug.Log("¡Las 4 pinzas bien conectadas! Ganaste");
        }
        else StartCoroutine(Reject(a.interactableObject, true));
    }

    void OnOut(SelectExitEventArgs a)
    {
        if (correct) { correct = false; connectedCorrect--; }
    }

    IEnumerator Reject(IXRSelectInteractable item, bool penalize)
    {
        if (penalize)
        {
            if (sparks) sparks.Play();
            Debug.Log($"Polaridad incorrecta: penalización de {penalty} s");
        }
        yield return null;
        socket.socketActive = false;
        socket.interactionManager.SelectExit(socket, item);
        var rb = item.transform.GetComponent<Rigidbody>();
        if (rb) rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
        yield return new WaitForSeconds(1f);
        socket.socketActive = true;
    }
}