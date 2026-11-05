using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Puntero laser propio para los menus en VR (principal y de pausa).
// Dibuja un rayo RECTO desde cada control, muestra un punto donde toca el menu,
// resalta el boton al apuntarlo y lo activa con el gatillo.
// La direccion sale de la "pose de apuntado" que manda el propio control (la misma que usan los rayos de XR).
public class PunteroMenuVR : MonoBehaviour
{
    public Canvas canvas;                          // el canvas del menu
    public float alcance = 8f;

    [Tooltip("Si el laser sale inclinado respecto a tu control, corrigelo aqui (en grados). Positivo = hacia abajo.")]
    public float correccionInclinacion = 0f;

    [Tooltip("Marcado: usa la pose de apuntado del control. Desmarcado: usa el objeto del rayo del rig.")]
    public bool usarPoseDeApuntado = true;

    [Tooltip("Escribe en la Console, cada segundo, que esta pasando con cada laser.")]
    public bool diagnostico = false;

    public Color colorNormal = new Color(0.14f, 0.20f, 0.32f, 1f);
    public Color colorResaltado = new Color(0.22f, 0.38f, 0.62f, 1f);
    public Color colorPresionado = new Color(0.10f, 0.60f, 0.90f, 1f);

    private class Mano
    {
        public bool izquierda;
        public Transform origen;             // plan B: objeto del rig
        public LineRenderer linea;
        public Transform punto;
        public InputAction gatillo;
        public InputAction posApuntado;
        public InputAction rotApuntado;
        public bool avisoPose;
        public bool avisoPlanB;
        public float proximaBusqueda;
        public float proximoLog;
    }

    private readonly List<Mano> manos = new List<Mano>();
    private readonly List<Behaviour> lineasDelRig = new List<Behaviour>();
    private Material materialLinea;
    private bool iniciado;

    // ------------------------------------------------------------ activacion

    void OnEnable()
    {
        if (!iniciado) Iniciar();

        PrepararBotones();

        foreach (Mano m in manos)
        {
            m.gatillo.Enable();
            m.posApuntado.Enable();
            m.rotApuntado.Enable();
            m.linea.gameObject.SetActive(true);
            m.punto.gameObject.SetActive(false);
        }

        // Se apagan los rayos curvos del rig mientras el menu esta abierto, para que solo se vea el laser nuevo
        lineasDelRig.Clear();
        foreach (MonoBehaviour m in FindObjectsByType<MonoBehaviour>())
        {
            if (m.GetType().Name == "XRInteractorLineVisual" && m.enabled)
            {
                m.enabled = false;
                lineasDelRig.Add(m);
            }
        }
    }

    void OnDisable()
    {
        foreach (Mano m in manos)
        {
            m.gatillo.Disable();
            m.posApuntado.Disable();
            m.rotApuntado.Disable();
            if (m.linea != null) m.linea.gameObject.SetActive(false);
            if (m.punto != null) m.punto.gameObject.SetActive(false);
        }

        foreach (Behaviour b in lineasDelRig)
            if (b != null) b.enabled = true;
        lineasDelRig.Clear();
    }

    void OnDestroy()
    {
        foreach (Mano m in manos)
        {
            m.gatillo.Dispose();
            m.posApuntado.Dispose();
            m.rotApuntado.Dispose();
            if (m.linea != null) Destroy(m.linea.gameObject);
            if (m.punto != null) Destroy(m.punto.gameObject);
        }
    }

    void Iniciar()
    {
        iniciado = true;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        materialLinea = new Material(shader);

        manos.Add(CrearMano(true));
        manos.Add(CrearMano(false));
    }

    Mano CrearMano(bool izquierda)
    {
        Mano m = new Mano { izquierda = izquierda };
        string lado = izquierda ? "LeftHand" : "RightHand";

        GameObject lg = new GameObject(izquierda ? "LaserIzquierdo" : "LaserDerecho");
        m.linea = lg.AddComponent<LineRenderer>();
        m.linea.useWorldSpace = true;
        m.linea.positionCount = 2;
        m.linea.startWidth = 0.006f;
        m.linea.endWidth = 0.004f;
        m.linea.material = materialLinea;
        m.linea.numCapVertices = 4;

        GameObject pg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pg.name = izquierda ? "PuntoIzquierdo" : "PuntoDerecho";
        Destroy(pg.GetComponent<Collider>());
        pg.transform.localScale = Vector3.one * 0.03f;
        pg.GetComponent<Renderer>().material.color = Color.white;
        m.punto = pg.transform;

        m.gatillo = new InputAction(type: InputActionType.Button);
        m.gatillo.AddBinding("<XRController>{" + lado + "}/{TriggerButton}");
        m.gatillo.AddBinding("<XRController>{" + lado + "}/triggerPressed");

        // Pose de apuntado (aim) del control
        m.posApuntado = new InputAction(type: InputActionType.Value, expectedControlType: "Vector3");
        m.posApuntado.AddBinding("<XRController>{" + lado + "}/pointerPosition");

        m.rotApuntado = new InputAction(type: InputActionType.Value, expectedControlType: "Quaternion");
        m.rotApuntado.AddBinding("<XRController>{" + lado + "}/pointerRotation");

        return m;
    }

    // Los botones se manejan aqui: se quita la transicion de color del Button y se pinta a mano
    void PrepararBotones()
    {
        if (canvas == null) return;

        foreach (Button b in canvas.GetComponentsInChildren<Button>(true))
        {
            b.transition = Selectable.Transition.None;
            Image img = b.targetGraphic as Image;
            if (img != null) img.color = colorNormal;
        }
    }

    // ------------------------------------------------------------ cada frame

    void Update()
    {
        if (canvas == null) return;

        RectTransform cr = canvas.GetComponent<RectTransform>();
        Plane plano = new Plane(cr.forward, cr.position);
        Button[] botones = canvas.GetComponentsInChildren<Button>(false);

        // Todos los botones en su color normal; luego se resaltan los apuntados
        foreach (Button b in botones)
        {
            Image img = b.targetGraphic as Image;
            if (img != null) img.color = colorNormal;
        }

        Button clic = null;

        foreach (Mano m in manos)
        {
            Vector3 pos;
            Quaternion rot;

            if (!ObtenerPose(m, out pos, out rot))
            {
                m.linea.enabled = false;
                m.punto.gameObject.SetActive(false);
                continue;
            }

            rot = rot * Quaternion.Euler(correccionInclinacion, 0f, 0f);
            Vector3 dir = rot * Vector3.forward;
            Ray rayo = new Ray(pos, dir);

            Button apuntado = null;
            Vector3 fin = pos + dir * 2.5f;
            bool tocaMenu = false;
            bool cruzaPlano = false;
            Vector3 local = Vector3.zero;

            float dist;
            if (plano.Raycast(rayo, out dist) && dist > 0f && dist <= alcance)
            {
                cruzaPlano = true;
                Vector3 hit = rayo.GetPoint(dist);
                local = cr.InverseTransformPoint(hit);

                if (cr.rect.Contains(new Vector2(local.x, local.y)))
                {
                    tocaMenu = true;
                    fin = hit;
                    apuntado = BotonEn(botones, hit);
                }
            }

            bool presionado = m.gatillo.IsPressed();

            if (apuntado != null)
            {
                Image img = apuntado.targetGraphic as Image;
                if (img != null) img.color = presionado ? colorPresionado : colorResaltado;

                if (m.gatillo.WasPressedThisFrame() && clic == null)
                    clic = apuntado;
            }

            // Laser y punto
            m.linea.enabled = true;
            m.linea.SetPosition(0, pos);
            m.linea.SetPosition(1, fin);
            Color colorLaser = apuntado != null ? new Color(0.3f, 1f, 0.6f, 1f) : new Color(0.6f, 0.8f, 1f, 0.9f);
            m.linea.startColor = colorLaser;
            m.linea.endColor = colorLaser;

            m.punto.gameObject.SetActive(tocaMenu);
            if (tocaMenu) m.punto.position = fin;

            if (diagnostico && Time.unscaledTime >= m.proximoLog)
            {
                m.proximoLog = Time.unscaledTime + 1f;
                Debug.Log("[PUNTERO-DIAG] " + (m.izquierda ? "izq" : "der")
                          + " origen=" + pos.ToString("F2") + " dir=" + dir.ToString("F2")
                          + " | cruza el plano del menu: " + cruzaPlano
                          + (cruzaPlano ? " (local " + local.x.ToString("F0") + "," + local.y.ToString("F0")
                              + " | el menu mide " + cr.rect.width.ToString("F0") + "x" + cr.rect.height.ToString("F0") + ")" : "")
                          + " | dentro del menu: " + tocaMenu
                          + " | boton: " + (apuntado != null ? apuntado.name : "ninguno")
                          + " | gatillo: " + presionado);
            }
        }

        // El clic se hace al final, porque puede cambiar de panel y cambiar la lista de botones
        if (clic != null) clic.onClick.Invoke();
    }

    // El boton que contiene el punto tocado (si hay varios, el ultimo dibujado, que queda encima)
    static Button BotonEn(Button[] botones, Vector3 puntoMundo)
    {
        Button encontrado = null;
        foreach (Button b in botones)
        {
            if (!b.interactable) continue;
            RectTransform r = b.transform as RectTransform;
            Vector3 local = r.InverseTransformPoint(puntoMundo);
            if (r.rect.Contains(new Vector2(local.x, local.y))) encontrado = b;
        }
        return encontrado;
    }

    // ------------------------------------------------------------ de donde sale el rayo

    bool ObtenerPose(Mano m, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;

        // 1) La pose de apuntado que manda el control (en el espacio de seguimiento del XR Origin)
        if (usarPoseDeApuntado)
        {
            Quaternion q = m.rotApuntado.ReadValue<Quaternion>();
            bool valida = !(Mathf.Approximately(q.x, 0f) && Mathf.Approximately(q.y, 0f)
                            && Mathf.Approximately(q.z, 0f) && Mathf.Approximately(q.w, 0f));

            if (valida)
            {
                Vector3 p = m.posApuntado.ReadValue<Vector3>();
                Transform espacio = EspacioDeSeguimiento();

                if (espacio != null)
                {
                    pos = espacio.TransformPoint(p);
                    rot = espacio.rotation * q;
                }
                else
                {
                    pos = p;
                    rot = q;
                }

                if (!m.avisoPose)
                {
                    m.avisoPose = true;
                    Debug.Log("[PUNTERO] Mano " + (m.izquierda ? "izquierda" : "derecha") + ": uso la pose de apuntado del control.");
                }
                return true;
            }
        }

        // 2) Plan B: el objeto del rayo del rig
        if (m.origen == null && Time.unscaledTime >= m.proximaBusqueda)
        {
            m.origen = BuscarOrigen(m.izquierda);
            m.proximaBusqueda = Time.unscaledTime + 1f;
        }

        if (m.origen != null)
        {
            pos = m.origen.position;
            rot = m.origen.rotation;

            if (!m.avisoPlanB)
            {
                m.avisoPlanB = true;
                Debug.LogWarning("[PUNTERO] Mano " + (m.izquierda ? "izquierda" : "derecha")
                                 + ": no llego la pose de apuntado, uso '" + Ruta(m.origen) + "'.");
            }
            return true;
        }

        return false;
    }

    // El "Camera Offset" del XR Origin: el espacio donde se miden las posiciones de cabeza y controles
    static Transform EspacioDeSeguimiento()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        return cam != null ? cam.transform.parent : null;
    }

    Transform BuscarOrigen(bool izquierda)
    {
        string lado = izquierda ? "left" : "right";
        MonoBehaviour mejor = null;

        foreach (MonoBehaviour m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            string tipo = m.GetType().Name;
            if (tipo != "NearFarInteractor" && tipo != "XRRayInteractor") continue;

            string ruta = Ruta(m.transform).ToLower();
            if (ruta.Contains("teleport") || !ruta.Contains(lado)) continue;

            if (mejor == null || tipo == "NearFarInteractor") mejor = m;
        }

        if (mejor != null)
        {
            Transform t = OrigenDelRayo(mejor);
            return t != null ? t : mejor.transform;
        }

        GameObject control = GameObject.Find(izquierda ? "Left Controller" : "Right Controller");
        return control != null ? control.transform : null;
    }

    static Transform OrigenDelRayo(object interactor)
    {
        Transform t = Leer(interactor, "rayOriginTransform") as Transform;
        if (t != null) return t;

        foreach (string nombre in new[] { "curveInteractionCaster", "farInteractionCaster" })
        {
            object caster = Leer(interactor, nombre);
            if (caster != null)
            {
                t = Leer(caster, "castOrigin") as Transform;
                if (t != null) return t;
            }
        }
        return null;
    }

    static object Leer(object objeto, string nombre)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        PropertyInfo prop = objeto.GetType().GetProperty(nombre, flags);
        if (prop != null && prop.CanRead) return prop.GetValue(objeto);

        FieldInfo campo = objeto.GetType().GetField(nombre, flags);
        return campo != null ? campo.GetValue(objeto) : null;
    }

    static string Ruta(Transform t)
    {
        string ruta = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            ruta = t.name + "/" + ruta;
        }
        return ruta;
    }
}