using UnityEngine;

public class AgarrarObjeto : MonoBehaviour
{
    public Transform puntoAgarre;
    public float distanciaMaxima = 3f;
    public KeyCode teclaAgarrar = KeyCode.F;
    public Vector3 rotacionEnMano = Vector3.zero;
    public float fuerzaSoltar = 1f; // empujoncito hacia adelante al soltar (m/s)

    private Rigidbody objetoAgarrado;
    private Collider[] colliders;

    // Otros scripts (como UsarLlave) leen esto para saber que tienes en la mano
    public Rigidbody ObjetoAgarrado => objetoAgarrado;

    void Update()
    {
        if (Input.GetKeyDown(teclaAgarrar))
        {
            if (objetoAgarrado == null)
                Agarrar();
            else
                Soltar();
        }
    }

    void Agarrar()
    {
        Ray rayo = new Ray(transform.position, transform.forward);

        if (!Physics.Raycast(rayo, out RaycastHit hit, distanciaMaxima)) return;
        if (!hit.collider.CompareTag("Agarrable")) return;

        objetoAgarrado = hit.collider.GetComponentInParent<Rigidbody>();
        if (objetoAgarrado == null) return;

        // Se apagan los colliders mientras lo llevas para que el rayo
        // de la camara no choque con el objeto que tienes en la mano
        colliders = objetoAgarrado.GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders) c.enabled = false;

        objetoAgarrado.useGravity = false;
        objetoAgarrado.isKinematic = true;

        objetoAgarrado.transform.SetParent(puntoAgarre);
        objetoAgarrado.transform.position = puntoAgarre.position;
        objetoAgarrado.transform.rotation = puntoAgarre.rotation * Quaternion.Euler(rotacionEnMano);
    }

    void Soltar()
    {
        foreach (Collider c in colliders) c.enabled = true;

        objetoAgarrado.transform.SetParent(null);
        objetoAgarrado.isKinematic = false;
        objetoAgarrado.useGravity = true;
        objetoAgarrado.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        objetoAgarrado.AddForce(transform.forward * fuerzaSoltar, ForceMode.VelocityChange);

        objetoAgarrado = null;
    }
}