using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu de pausa en VR. Se abre con el boton Y (control izquierdo).
// Se arma solo por codigo: basta con poner este script en un objeto vacio de la escena.
public class MenuPausa : MonoBehaviour
{
    [Header("Escena del menu principal (debe estar agregada en Build Profiles)")]
    public string escenaMenuPrincipal = "MenuPrincipal";

    [Header("Donde aparece el menu")]
    public float distancia = 1.4f;      // metros frente a la cara
    public float anchoMetros = 1.1f;    // que tan ancho se ve

    private InputAction botonPausa;
    private bool pausado;

    private GameObject canvasGO;
    private GameObject panelPausa;
    private GameObject panelOpciones;
    private TextMeshProUGUI textoEstado;
    private TextMeshProUGUI textoActual;
    private TextMeshProUGUI txtMovSuave, txtMovTeleport, txtGiroSuave, txtGiroSnap;

    private static readonly Color colorBoton = new Color(0.14f, 0.20f, 0.32f, 1f);
    private static readonly Color colorResaltado = new Color(0.22f, 0.38f, 0.62f, 1f);
    private static readonly Color colorPresionado = new Color(0.10f, 0.60f, 0.90f, 1f);
    private static readonly Color colorActivo = new Color(0.35f, 1f, 0.45f, 1f);

    void Awake()
    {
        botonPausa = new InputAction(type: InputActionType.Button);

        // Boton Y del control izquierdo (en Quest: X es el primario, Y el secundario)
        botonPausa.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
        botonPausa.AddBinding("<XRController>{LeftHand}/secondaryButton");
        // Boton de menu del control izquierdo (el de las tres rayitas)
        botonPausa.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        // Para probar en el editor con el teclado
        botonPausa.AddBinding("<Keyboard>/escape");
        botonPausa.AddBinding("<Keyboard>/p");
    }

    void OnEnable()
    {
        botonPausa.Enable();
    }

    void OnDisable()
    {
        botonPausa.Disable();
    }

    void OnDestroy()
    {
        botonPausa.Dispose();

        // Por si se cambia de escena estando en pausa
        if (pausado)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    void Start()
    {
        // Aplica las opciones de movimiento guardadas
        StartCoroutine(AplicarAjustes());
    }

    IEnumerator AplicarAjustes()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        AjustesMovimiento.Aplicar();
    }

    void Update()
    {
        if (botonPausa.WasPressedThisFrame())
        {
            if (pausado) Reanudar();
            else Pausar();
        }
    }

    // ---------------------------------------------------------------- pausa

    void Pausar()
    {
        pausado = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (canvasGO == null) ConstruirMenu();

        ColocarFrenteAlJugador();
        canvasGO.SetActive(true);
        MostrarPausa();

        Debug.Log("[PAUSA] Juego en pausa");
    }

    void Reanudar()
    {
        pausado = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (canvasGO != null) canvasGO.SetActive(false);
        Debug.Log("[PAUSA] Juego reanudado");
    }

    void IrAlMenuPrincipal()
    {
        if (!Application.CanStreamedLevelBeLoaded(escenaMenuPrincipal))
        {
            MostrarEstado("No existe la escena \"" + escenaMenuPrincipal + "\". Agrégala en Build Profiles.");
            Debug.LogWarning("[PAUSA] La escena '" + escenaMenuPrincipal + "' no existe o no esta en Build Profiles.");
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(escenaMenuPrincipal);
    }

    void MostrarPausa()
    {
        panelPausa.SetActive(true);
        panelOpciones.SetActive(false);
        MostrarEstado("");
    }

    void MostrarOpciones()
    {
        panelPausa.SetActive(false);
        panelOpciones.SetActive(true);
        ActualizarOpciones();
    }

    void MostrarEstado(string mensaje)
    {
        if (textoEstado != null) textoEstado.text = mensaje;
    }

    // ------------------------------------------------------- opciones de movimiento

    void ElegirMovimiento(bool suave)
    {
        AjustesMovimiento.MovimientoSuave = suave;
        AjustesMovimiento.Aplicar();
        ActualizarOpciones();
    }

    void ElegirGiro(bool suave)
    {
        AjustesMovimiento.GiroSuave = suave;
        AjustesMovimiento.Aplicar();
        ActualizarOpciones();
    }

    void ActualizarOpciones()
    {
        bool movSuave = AjustesMovimiento.MovimientoSuave;
        bool giroSuave = AjustesMovimiento.GiroSuave;

        Marcar(txtMovSuave, movSuave);
        Marcar(txtMovTeleport, !movSuave);
        Marcar(txtGiroSuave, giroSuave);
        Marcar(txtGiroSnap, !giroSuave);

        textoActual.text = "Activo: " + (movSuave ? "movimiento suave" : "teletransporte")
                           + "  |  " + (giroSuave ? "giro suave" : "giro por pasos");
    }

    static void Marcar(TextMeshProUGUI texto, bool activo)
    {
        texto.color = activo ? colorActivo : Color.white;
        texto.fontStyle = activo ? FontStyles.Bold : FontStyles.Normal;
    }

    // -------------------------------------------------------------- construccion del menu



    void ColocarFrenteAlJugador()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cam == null) return;

        Vector3 adelante = cam.transform.forward;
        adelante.y = 0f;
        if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
        adelante.Normalize();

        Vector3 pos = cam.transform.position + adelante * distancia;
        pos.y = cam.transform.position.y - 0.05f;

        canvasGO.GetComponent<RectTransform>().SetPositionAndRotation(pos, Quaternion.LookRotation(adelante));

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.worldCamera = cam;
    }

    void ConstruirMenu()
    {
        canvasGO = new GameObject("MenuPausaCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

        RectTransform rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800f, 600f);
        rt.localScale = Vector3.one * (anchoMetros / 800f);

        // Fondo
        Image fondo = CrearImagen(rt, "Fondo", new Color(0.04f, 0.06f, 0.11f, 0.95f));
        Estirar(fondo.rectTransform);

        // ----- Panel principal de pausa
        panelPausa = CrearPanel(rt, "PanelPausa");
        CrearTexto(panelPausa.transform, "Titulo", "PAUSA", 64, new Vector2(0f, 230f), new Vector2(700f, 90f), Color.white);
        CrearBoton(panelPausa.transform, "Continuar", new Vector2(0f, 100f), new Vector2(520f, 95f), 42, Reanudar);
        CrearBoton(panelPausa.transform, "Opciones", new Vector2(0f, -20f), new Vector2(520f, 95f), 42, MostrarOpciones);
        CrearBoton(panelPausa.transform, "Menú principal", new Vector2(0f, -140f), new Vector2(520f, 95f), 42, IrAlMenuPrincipal);
        textoEstado = CrearTexto(panelPausa.transform, "Estado", "", 26, new Vector2(0f, -240f), new Vector2(760f, 60f), new Color(1f, 0.85f, 0.3f));

        // ----- Submenu de opciones (selector de movimiento)
        panelOpciones = CrearPanel(rt, "PanelOpciones");
        CrearTexto(panelOpciones.transform, "Titulo", "OPCIONES", 60, new Vector2(0f, 245f), new Vector2(700f, 80f), Color.white);

        CrearTexto(panelOpciones.transform, "EtMov", "Movimiento", 36, new Vector2(0f, 165f), new Vector2(700f, 50f), new Color(0.7f, 0.75f, 0.85f));
        txtMovSuave = CrearBoton(panelOpciones.transform, "Movimiento suave", new Vector2(-210f, 95f), new Vector2(380f, 80f), 32, () => ElegirMovimiento(true));
        txtMovTeleport = CrearBoton(panelOpciones.transform, "Teletransporte", new Vector2(210f, 95f), new Vector2(380f, 80f), 32, () => ElegirMovimiento(false));

        CrearTexto(panelOpciones.transform, "EtGiro", "Giro", 36, new Vector2(0f, 20f), new Vector2(700f, 50f), new Color(0.7f, 0.75f, 0.85f));
        txtGiroSuave = CrearBoton(panelOpciones.transform, "Giro suave", new Vector2(-210f, -50f), new Vector2(380f, 80f), 32, () => ElegirGiro(true));
        txtGiroSnap = CrearBoton(panelOpciones.transform, "Giro por pasos", new Vector2(210f, -50f), new Vector2(380f, 80f), 32, () => ElegirGiro(false));

        textoActual = CrearTexto(panelOpciones.transform, "Actual", "", 28, new Vector2(0f, -135f), new Vector2(760f, 50f), new Color(0.6f, 0.9f, 1f));
        CrearBoton(panelOpciones.transform, "Volver", new Vector2(0f, -225f), new Vector2(320f, 80f), 36, MostrarPausa);

        // Puntero laser propio: rayo recto, punto de mira y gatillo para elegir
        PunteroMenuVR puntero = canvasGO.AddComponent<PunteroMenuVR>();
        puntero.canvas = canvas;

        canvasGO.SetActive(false);
    }

    // -------------------------------------------------------------- ayudas para crear UI

    static GameObject CrearPanel(Transform padre, string nombre)
    {
        GameObject g = new GameObject(nombre, typeof(RectTransform));
        g.transform.SetParent(padre, false);
        Estirar(g.GetComponent<RectTransform>());
        return g;
    }

    static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    static Image CrearImagen(Transform padre, string nombre, Color color)
    {
        GameObject g = new GameObject(nombre);
        g.transform.SetParent(padre, false);
        Image img = g.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI CrearTexto(Transform padre, string nombre, string texto, float tamano, Vector2 pos, Vector2 size, Color color)
    {
        GameObject g = new GameObject(nombre);
        g.transform.SetParent(padre, false);

        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        t.text = texto;
        t.fontSize = tamano;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;

        RectTransform r = t.rectTransform;
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return t;
    }

    static TextMeshProUGUI CrearBoton(Transform padre, string texto, Vector2 pos, Vector2 size, float fuente, UnityAction alHacerClic)
    {
        GameObject g = new GameObject("Boton_" + texto);
        g.transform.SetParent(padre, false);

        Image img = g.AddComponent<Image>();
        img.color = Color.white;   // el color lo maneja el Button

        Button boton = g.AddComponent<Button>();
        boton.targetGraphic = img;

        ColorBlock colores = boton.colors;
        colores.normalColor = colorBoton;
        colores.highlightedColor = colorResaltado;
        colores.pressedColor = colorPresionado;
        colores.selectedColor = colorBoton;
        colores.colorMultiplier = 1f;
        colores.fadeDuration = 0.05f;
        boton.colors = colores;
        boton.onClick.AddListener(alHacerClic);

        RectTransform r = img.rectTransform;
        r.sizeDelta = size;
        r.anchoredPosition = pos;

        return CrearTexto(g.transform, "Texto", texto, fuente, Vector2.zero, size, Color.white);
    }

    // Para ver en la Console que componentes de locomocion tiene el rig (clic derecho en el titulo del script)
    [ContextMenu("Listar locomocion en la Console")]
    void ListarLocomocion()
    {
        AjustesMovimiento.Listar();
    }
}