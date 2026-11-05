using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
public class Birlo : MonoBehaviour
{
    // EnRueda: puesto en la llanta vieja | Suelto: fuera, con fisicas
    // Colocado: metido en el hueco de la llanta nueva (falta apretarlo) | Apretado: terminado
    public enum Estado { EnRueda, Suelto, Colocado, Apretado }

    // Eventos que escuchan el GestorMinijuego y la LlantaCae
    public static event Action OnAflojado;
    public static event Action OnApretado;

    [Header("Estado")]
    public Estado estado = Estado.EnRueda;

    [Header("Apriete: 0 = suelto, 1 = apretado por completo")]
    [Range(0f, 1f)] public float apriete = 1f;
    public float velocidad = 0.4f;       // 0.4 = tarda 2.5 s en aflojar o apretar

    [Header("Visual")]
    public Vector3 ejeGiro = Vector3.up; // eje del esparrago (local)
    public float giroTotal = 720f;       // grados que gira de 0 a 1
    public float recorrido = 0.02f;      // cuanto sale al aflojarse

    [Header("Aviso al apretar")]
    public Color colorFlojo = new Color(1f, 0.55f, 0f);      // naranja: colocado pero flojo
    public Color colorApretado = new Color(0.1f, 1f, 0.2f);  // verde: listo
    public float brilloApretado = 0.6f;                      // brillo del verde al terminar
    public AudioClip sonidoApretado;                         // opcional

    [Header("Fisicas")]
    public bool usarFisicas = true;      // true = al aflojarse cae, rueda y se puede agarrar
    public float masa = 0.05f;
    public float rebote = 0.5f;          // mas rebote = rueda y se va mas lejos
    public float friccion = 0.1f;        // menos friccion = rueda mas

    private Rigidbody rb;
    private XRGrabInteractable agarre;
    private Renderer[] renderers;

    // true mientras lo tienes en la mano
    public bool EstaAgarrado => agarre != null && agarre.isSelected;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agarre = GetComponent<XRGrabInteractable>();
        renderers = GetComponentsInChildren<Renderer>();

        if (estado == Estado.EnRueda)
        {
            rb.isKinematic = true;
            gameObject.tag = "Untagged";

            // Mientras esta en la rueda no se puede agarrar con la mano
            if (agarre != null) agarre.enabled = false;
        }
    }

    // direccion: -1 = aflojar, +1 = apretar
    public void Girar(float direccion)
    {
        if (estado == Estado.Suelto || estado == Estado.Apretado) return;

        // Ya metido en el hueco de la llanta nueva, solo se aprieta
        if (estado == Estado.Colocado && direccion < 0f) return;

        float nuevo = Mathf.Clamp01(apriete + direccion * velocidad * Time.deltaTime);
        float delta = nuevo - apriete;
        if (Mathf.Approximately(delta, 0f)) return;

        apriete = nuevo;

        transform.Rotate(ejeGiro, delta * giroTotal, Space.Self);
        transform.Translate(ejeGiro * (-delta * recorrido), Space.Self);

        // Mientras se aprieta, el color va de naranja a verde
        if (estado == Estado.Colocado)
            Pintar(Color.Lerp(colorFlojo, colorApretado, apriete), 0f);

        if (estado == Estado.EnRueda && apriete <= 0f)
            Soltar();
        else if (estado == Estado.Colocado && apriete >= 1f)
            Apretar();
    }

    void Soltar()
    {
        estado = Estado.Suelto;
        apriete = 0f;
        transform.SetParent(null);

        if (usarFisicas)
        {
            gameObject.tag = "Agarrable";

            // Material que rebota y casi no frena, para que ruede lejos
            PhysicsMaterial mat = new PhysicsMaterial("BirloRebote");
            mat.dynamicFriction = friccion;
            mat.staticFriction = friccion;
            mat.bounciness = rebote;
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
            mat.bounceCombine = PhysicsMaterialCombine.Maximum;

            foreach (Collider c in GetComponentsInChildren<Collider>())
                c.material = mat;

            // Ahora si se puede agarrar con la mano
            if (agarre != null) agarre.enabled = true;

            rb.mass = masa;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.AddForce(transform.TransformDirection(ejeGiro) * 1f, ForceMode.VelocityChange);
        }
        else
        {
            gameObject.SetActive(false); // sin fisicas: simplemente se quita
        }

        OnAflojado?.Invoke();
    }

    // Lo llama HuecoBirlo cuando sueltas el birlo cerca de un hueco
    public void Colocar(Transform hueco)
    {
        if (estado != Estado.Suelto) return;

        estado = Estado.Colocado;
        apriete = 0f;
        gameObject.tag = "Untagged";

        // Ya no se puede agarrar con la mano
        if (agarre != null) agarre.enabled = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        // Se acomoda en el hueco, un poco salido (como un birlo flojo)
        transform.SetParent(hueco);
        transform.position = hueco.position;
        transform.rotation = hueco.rotation;
        transform.Translate(ejeGiro * recorrido, Space.Self);

        // Naranja = colocado pero flojo
        Pintar(colorFlojo, 0f);
    }

    void Apretar()
    {
        estado = Estado.Apretado;
        apriete = 1f;

        // Aviso: verde brillante, saltito y sonido
        Pintar(colorApretado, brilloApretado);
        StartCoroutine(Pulso());
        if (sonidoApretado != null)
            AudioSource.PlayClipAtPoint(sonidoApretado, transform.position);

        OnApretado?.Invoke();
    }

    void Pintar(Color color, float brillo)
    {
        foreach (Renderer r in renderers)
        {
            Material m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);

            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * brillo);
            }
        }
    }

    // Pequeno "inflado" del birlo al quedar apretado
    IEnumerator Pulso()
    {
        Vector3 escala = transform.localScale;
        float t = 0f;
        const float duracion = 0.25f;

        while (t < duracion)
        {
            t += Time.deltaTime;
            float k = 1f + 0.4f * Mathf.Sin(t / duracion * Mathf.PI);
            transform.localScale = escala * k;
            yield return null;
        }

        transform.localScale = escala;
    }
}