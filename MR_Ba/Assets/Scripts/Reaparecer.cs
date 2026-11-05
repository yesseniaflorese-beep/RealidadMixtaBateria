using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Si el objeto se cae del mapa o se va demasiado lejos, vuelve a aparecer.
// Sirve para la llave, los birlos y las llantas nuevas.
[RequireComponent(typeof(Rigidbody))]
public class Reaparecer : MonoBehaviour
{
    [Header("Cuando reaparece")]
    public float alturaMinima = -3f;       // si baja de esta altura (Y), reaparece. Ponla debajo del piso
    public float distanciaMaxima = 30f;    // si se aleja mas de esto de donde empezo, reaparece

    [Header("Donde reaparece")]
    [Tooltip("Opcional: objeto vacio donde reaparece. Si esta vacio, vuelve a su lugar inicial.")]
    public Transform puntoReaparicion;
    [Tooltip("Opcional: nombre de un objeto de la escena (por ejemplo PuntoBirlos). Sirve dentro de prefabs.")]
    public string nombrePuntoReaparicion = "";
    public float dispersion = 0.15f;       // para que varios objetos no reaparezcan encimados

    [Header("Solo para la llave")]
    [Tooltip("Marcalo en la llave: frena el movimiento, evita que atraviese el piso y que salga lanzada al soltarla.")]
    public bool reducirMovimiento = false;

    private Rigidbody rb;
    private XRGrabInteractable agarre;
    private Vector3 posInicial;
    private Quaternion rotInicial;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agarre = GetComponent<XRGrabInteractable>();
        posInicial = transform.position;
        rotInicial = transform.rotation;

        if (reducirMovimiento)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = 0.3f;
            rb.angularDamping = 1f;
            if (agarre != null) agarre.throwOnDetach = false;
        }
    }

    void Update()
    {
        // En la mano nunca reaparece
        if (agarre != null && agarre.isSelected) return;

        float lejos = Vector3.Distance(transform.position, posInicial);
        if (transform.position.y < alturaMinima || lejos > distanciaMaxima)
            Volver();
    }

    void Volver()
    {
        if (puntoReaparicion == null && !string.IsNullOrEmpty(nombrePuntoReaparicion))
        {
            GameObject punto = GameObject.Find(nombrePuntoReaparicion);
            if (punto != null) puntoReaparicion = punto.transform;
        }

        Vector3 pos = posInicial;
        Quaternion rot = rotInicial;

        if (puntoReaparicion != null)
        {
            pos = puntoReaparicion.position;
            rot = puntoReaparicion.rotation;
        }

        pos += new Vector3(Random.Range(-dispersion, dispersion), 0f, Random.Range(-dispersion, dispersion));

        transform.SetParent(null);

        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.SetPositionAndRotation(pos, rot);
        Debug.Log("[REAPARECER] " + name + " volvio a aparecer en " + pos);
    }
}
