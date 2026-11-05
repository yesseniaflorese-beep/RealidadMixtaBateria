using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LlantaCae : MonoBehaviour
{
    // Aviso de ESTA llanta (no de todas): lo escucha el MontajeLlanta de su mismo eje
    public event Action Cayo;

    // Los llena solo la EstacionRueda mas cercana al empezar el juego.
    // NonSerialized = no se guarda en la escena, asi nunca quedan birlos viejos de pruebas anteriores.
    [NonSerialized] public Birlo[] misBirlos = new Birlo[0];

    public float retraso = 0.3f;                 // espera antes de caer
    public float masa = 10f;

    [Tooltip("Si lo dejas vacio, usa los colliders del objeto llamado 'Carro'")]
    public Collider[] ignorarColisiones;

    private Rigidbody rb;
    private bool yaCayo;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = masa;
        rb.useGravity = true;
        rb.isKinematic = true; // se queda quieta hasta que se suelten sus birlos

        // Con Rigidbody, un Mesh Collider debe ser Convex: se marca solo
        foreach (MeshCollider mc in GetComponentsInChildren<MeshCollider>()) mc.convex = true;

        // Si no se asigno nada, se ignoran los colliders del carro
        if (ignorarColisiones == null || ignorarColisiones.Length == 0)
        {
            GameObject carro = GameObject.Find("Carro");
            if (carro != null) ignorarColisiones = carro.GetComponentsInChildren<Collider>();
        }

        // Sin collider mientras esta montada, para que la llave llegue a los birlos
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    void Start()
    {
        Invoke(nameof(Revisar), 1f);
    }

    // Avisos para encontrar llantas repetidas o sin estacion
    void Revisar()
    {
        if (misBirlos == null || misBirlos.Length == 0)
            Debug.LogWarning("[LLANTA] " + Ruta(transform) + " no tiene birlos: falta una EstacionRueda cerca.");

        foreach (LlantaCae otra in FindObjectsByType<LlantaCae>())
        {
            if (otra == this) continue;
            if (Vector3.Distance(transform.position, otra.transform.position) < 0.3f)
                Debug.LogWarning("[LLANTA] Hay DOS llantas con LlantaCae en el mismo lugar: "
                                 + Ruta(transform) + " y " + Ruta(otra.transform) + ". Borra una.");
        }
    }

    public static string Ruta(Transform t)
    {
        string ruta = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            ruta = t.name + "/" + ruta;
        }
        return ruta;
    }

    void OnEnable()
    {
        Birlo.OnAflojado += BirloAflojado;
    }

    void OnDisable()
    {
        Birlo.OnAflojado -= BirloAflojado;
    }

    void BirloAflojado()
    {
        if (yaCayo) return;
        if (misBirlos == null || misBirlos.Length == 0) return;

        int sueltos = 0;
        foreach (Birlo b in misBirlos)
        {
            if (b != null && b.estado == Birlo.Estado.Suelto) sueltos++;
        }

        if (sueltos >= misBirlos.Length)
        {
            yaCayo = true;
            Invoke(nameof(Caer), retraso);
        }
    }

    [ContextMenu("Caer ahora")]
    void Caer()
    {
        yaCayo = true;
        Debug.Log("[LLANTA] Cae: " + Ruta(transform));

        // Primero se prenden los colliders
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = true;

        // Evita que los colliders del carro la empujen o la atoren al caer
        foreach (Collider mio in GetComponentsInChildren<Collider>())
        {
            foreach (Collider otro in ignorarColisiones)
            {
                if (otro != null && otro != mio) Physics.IgnoreCollision(mio, otro);
            }
        }

        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Cayo?.Invoke();
    }
}