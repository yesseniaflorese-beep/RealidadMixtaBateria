using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Va en SocketLlanta (junto a SocketSoloLlanta y un Sphere Collider con Is Trigger)
[RequireComponent(typeof(XRSocketInteractor))]
public class MontajeLlanta : MonoBehaviour
{
    // Avisos de ESTE eje: los escuchan los HuecoBirlo que son hijos de este SocketLlanta
    public event Action LlantaMontada;
    public event Action LlantaRetirada;

    [Tooltip("La llanta vieja de este eje. Si lo dejas vacio, usa la mas cercana.")]
    public LlantaCae llantaVieja;

    private XRSocketInteractor socket;
    private Component llantaActual;   // la llanta nueva que esta encajada
    private bool bloqueada;           // true = ya tiene un birlo puesto, ya no se puede quitar

    // Llanta ya fija: se vigila para siempre que no se mueva ni se apague
    private bool fijando;
    private Transform llantaFija;
    private Rigidbody rbFija;
    private Vector3 posFija;
    private Quaternion rotFija;
    private bool avisoDesactivada, avisoFisica, avisoMovida;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();

        // El socket esta apagado hasta que caiga la llanta vieja de este eje
        socket.socketActive = false;

        socket.selectEntered.AddListener(a =>
        {
            llantaActual = a.interactableObject as Component;
            Debug.Log("[MONTAJE] " + name + ": llanta nueva montada (" + llantaActual.name + ") en " + llantaActual.transform.position);
            LlantaMontada?.Invoke();
        });

        socket.selectExited.AddListener(_ =>
        {
            // Si ya esta bloqueada, esto es parte del bloqueo y no un retiro de verdad
            if (bloqueada) return;

            llantaActual = null;
            Debug.Log("[MONTAJE] " + name + ": llanta retirada");
            LlantaRetirada?.Invoke();
        });
    }

    void Start()
    {
        if (llantaVieja == null) llantaVieja = BuscarMasCercana();

        if (llantaVieja != null)
            llantaVieja.Cayo += ActivarSocket;
        else
            Debug.LogWarning(name + ": no encontre ninguna llanta vieja para este eje.");
    }

    void OnDestroy()
    {
        if (llantaVieja != null) llantaVieja.Cayo -= ActivarSocket;
    }

    LlantaCae BuscarMasCercana()
    {
        LlantaCae mejor = null;
        float mejorDistancia = float.MaxValue;

        foreach (LlantaCae l in FindObjectsByType<LlantaCae>())
        {
            float d = Vector3.Distance(transform.position, l.transform.position);
            if (d < mejorDistancia)
            {
                mejorDistancia = d;
                mejor = l;
            }
        }
        return mejor;
    }

    void ActivarSocket()
    {
        socket.socketActive = true;
        Debug.Log("[MONTAJE] " + name + ": ya puedes montar la llanta nueva");
    }

    // Lo llama HuecoBirlo cuando se pone el primer birlo: desde ese momento la llanta ya no se puede quitar
    public void BloquearLlanta()
    {
        if (bloqueada || llantaActual == null) return;

        bloqueada = true;
        StartCoroutine(Bloquear());
    }

    IEnumerator Bloquear()
    {
        // El Rigidbody puede estar en el mismo objeto del agarre o en uno de sus padres
        Rigidbody rbLlanta = llantaActual.GetComponentInParent<Rigidbody>();
        XRGrabInteractable agarre = llantaActual.GetComponent<XRGrabInteractable>();

        // Se guarda la posicion del objeto que de verdad se mueve (el del Rigidbody)
        Transform llanta = rbLlanta != null ? rbLlanta.transform : llantaActual.transform;
        Vector3 pos = llanta.position;
        Quaternion rot = llanta.rotation;

        Debug.Log("[DIAGNOSTICO] " + name + " va a bloquear '" + llanta.name + "' | posicion: " + pos
                  + " | tiene Rigidbody: " + (rbLlanta != null) + " | tiene XRGrab: " + (agarre != null));

        // Se apagan los colliders un momento: asi, al soltarla, no choca con nada
        Collider[] colliders = llanta.GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders) c.enabled = false;

        // Se apaga el socket y el agarre: ninguna mano puede volver a tomarla
        socket.socketActive = false;
        if (agarre != null) agarre.enabled = false;

        // Se espera un par de frames a que el toolkit termine de soltarla...
        yield return null;
        yield return null;

        if (llanta == null)
        {
            Debug.LogWarning("[SEGUIMIENTO] " + name + ": la llanta desaparecio mientras se bloqueaba.");
            yield break;
        }

        // ...y se deja firme, en el mismo lugar donde estaba
        if (rbLlanta != null)
        {
            rbLlanta.linearVelocity = Vector3.zero;
            rbLlanta.angularVelocity = Vector3.zero;
            rbLlanta.useGravity = false;
            rbLlanta.isKinematic = true;
        }
        llanta.SetPositionAndRotation(pos, rot);

        // Se prenden otra vez los colliders, pero sin chocar con las llantas viejas ni con el carro
        foreach (Collider c in colliders) c.enabled = true;
        IgnorarChoques(llanta, colliders);

        // Desde aqui la llanta se vigila para siempre (ver LateUpdate)
        llantaFija = llanta;
        rbFija = rbLlanta;
        posFija = pos;
        rotFija = rot;
        fijando = true;

        Debug.Log("[MONTAJE] " + name + ": llanta bloqueada (" + llanta.name + "), ya no se puede quitar");
    }

    // Para que ni las llantas viejas que ya cayeron ni el carro empujen (o lancen) a la llanta nueva fija
    void IgnorarChoques(Transform llanta, Collider[] nuevos)
    {
        foreach (LlantaCae vieja in FindObjectsByType<LlantaCae>())
        {
            if (vieja.transform.IsChildOf(llanta) || llanta.IsChildOf(vieja.transform)) continue;

            foreach (Collider cv in vieja.GetComponentsInChildren<Collider>())
                Ignorar(nuevos, cv);
        }

        GameObject carro = GameObject.Find("Carro");
        if (carro != null)
        {
            foreach (Collider cc in carro.GetComponentsInChildren<Collider>())
                Ignorar(nuevos, cc);
        }
    }

    void Ignorar(Collider[] nuevos, Collider otro)
    {
        if (otro == null || !otro.enabled) return;

        foreach (Collider n in nuevos)
        {
            if (n != null && n != otro && n.enabled)
                Physics.IgnoreCollision(n, otro);
        }
    }

    void LateUpdate()
    {
        if (!fijando) return;

        if (llantaFija == null)
        {
            fijando = false;
            Debug.LogWarning("[SEGUIMIENTO] " + name + ": la llanta fija fue DESTRUIDA.");
            return;
        }

        if (!llantaFija.gameObject.activeSelf)
        {
            if (!avisoDesactivada)
            {
                Debug.LogWarning("[SEGUIMIENTO] " + name + ": '" + llantaFija.name + "' fue DESACTIVADA por algo. La vuelvo a activar.");
                avisoDesactivada = true;
            }
            llantaFija.gameObject.SetActive(true);
        }

        if (rbFija != null && !rbFija.isKinematic)
        {
            if (!avisoFisica)
            {
                Debug.LogWarning("[SEGUIMIENTO] " + name + ": '" + llantaFija.name + "' volvio a tener fisica (Is Kinematic apagado). La fijo otra vez.");
                avisoFisica = true;
            }
            rbFija.linearVelocity = Vector3.zero;
            rbFija.angularVelocity = Vector3.zero;
            rbFija.useGravity = false;
            rbFija.isKinematic = true;
        }

        if (Vector3.Distance(llantaFija.position, posFija) > 0.02f || Quaternion.Angle(llantaFija.rotation, rotFija) > 1f)
        {
            if (!avisoMovida)
            {
                Debug.LogWarning("[SEGUIMIENTO] " + name + ": '" + llantaFija.name + "' se movio a " + llantaFija.position
                                 + ". La regreso a " + posFija);
                avisoMovida = true;
            }
            llantaFija.SetPositionAndRotation(posFija, rotFija);
        }
    }
}