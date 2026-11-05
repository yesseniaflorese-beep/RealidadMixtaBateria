using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu principal en VR: "Elegir nivel" y "Salir".
// Se arma solo por codigo: basta con poner este script en un objeto vacio de la escena MenuPrincipal.
public class MenuPrincipal : MonoBehaviour
{
    [System.Serializable]
    public class Nivel
    {
        public string nombre;   // lo que se lee en el boton
        public string escena;   // nombre EXACTO de la escena (debe estar en Build Profiles)
    }

    [Header("Titulo")]
    public string tituloJuego = "TALLER VR";

    [Header("Niveles (maximo 4)")]
    public Nivel[] niveles = new Nivel[]
    {
        new Nivel { nombre = "Cambio de ruedas", escena = "CarroVr" },
        new Nivel { nombre = "Batería y cables", escena = "" }
    };

    [Header("Donde aparece el menu")]
    public float distancia = 1.6f;
    public float anchoMetros = 1.2f;

    private GameObject canvasGO;
    private GameObject panelPrincipal;
    private GameObject panelNiveles;
    private TextMeshProUGUI textoEstado;

    private static readonly Color colorBoton = new Color(0.14f, 0.20f, 0.32f, 1f);
    private static readonly Color colorResaltado = new Color(0.22f, 0.38f, 0.62f, 1f);
    private static readonly Color colorPresionado = new Color(0.10f, 0.60f, 0.90f, 1f);

    void Start()
    {
        // Por si se llega desde un nivel que estaba en pausa
        Time.timeScale = 1f;
        AudioListener.pause = false;

        ConstruirMenu();
        StartCoroutine(ColocarCuandoHayCamara());
    }

    // ---------------------------------------------------------------- acciones

    void MostrarPrincipal()
    {
        panelPrincipal.SetActive(true);
        panelNiveles.SetActive(false);
        MostrarEstado("");
    }

    void MostrarNiveles()
    {
        panelPrincipal.SetActive(false);
        panelNiveles.SetActive(true);
        MostrarEstado("");
    }

    void CargarNivel(Nivel nivel)
    {
        if (string.IsNullOrEmpty(nivel.escena) || !Application.CanStreamedLevelBeLoaded(nivel.escena))
        {
            MostrarEstado("No se encontró la escena \"" + nivel.escena + "\". Agrégala en Build Profiles.");
            Debug.LogWarning("[MENU] La escena '" + nivel.escena + "' no existe o no esta en Build Profiles.");
            return;
        }

        SceneManager.LoadScene(nivel.escena);
    }

    void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void MostrarEstado(string mensaje)
    {
        if (textoEstado != null) textoEstado.text = mensaje;
    }

    // ---------------------------------------------------------------- construccion


    // En VR la camara tarda un instante en recibir la posicion de tu cabeza: se espera antes de colocar el menu
    IEnumerator ColocarCuandoHayCamara()
    {
        yield return new WaitForSecondsRealtime(0.6f);

        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cam == null) yield break;

        Vector3 adelante = cam.transform.forward;
        adelante.y = 0f;
        if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
        adelante.Normalize();

        Vector3 pos = cam.transform.position + adelante * distancia;
        pos.y = cam.transform.position.y - 0.05f;

        canvasGO.GetComponent<RectTransform>().SetPositionAndRotation(pos, Quaternion.LookRotation(adelante));
        canvasGO.GetComponent<Canvas>().worldCamera = cam;
    }

    void ConstruirMenu()
    {
        canvasGO = new GameObject("MenuPrincipalCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

        RectTransform rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800f, 640f);
        rt.localScale = Vector3.one * (anchoMetros / 800f);
        rt.position = new Vector3(0f, 1.5f, 2f);   // posicion provisional, se corrige al tener camara

        Image fondo = CrearImagen(rt, "Fondo", new Color(0.04f, 0.06f, 0.11f, 0.95f));
        Estirar(fondo.rectTransform);

        // ----- Panel principal
        panelPrincipal = CrearPanel(rt, "PanelPrincipal");
        CrearTexto(panelPrincipal.transform, "Titulo", tituloJuego, 76, new Vector2(0f, 190f), new Vector2(740f, 110f), Color.white);
        CrearBoton(panelPrincipal.transform, "Elegir nivel", new Vector2(0f, 30f), new Vector2(560f, 105f), 46, MostrarNiveles);
        CrearBoton(panelPrincipal.transform, "Salir", new Vector2(0f, -110f), new Vector2(560f, 105f), 46, Salir);

        // ----- Panel de niveles
        panelNiveles = CrearPanel(rt, "PanelNiveles");
        CrearTexto(panelNiveles.transform, "Titulo", "ELEGIR NIVEL", 62, new Vector2(0f, 255f), new Vector2(740f, 90f), Color.white);

        int cantidad = Mathf.Min(niveles.Length, 4);
        for (int i = 0; i < cantidad; i++)
        {
            Nivel nivel = niveles[i];
            string texto = string.IsNullOrEmpty(nivel.nombre) ? "Nivel " + (i + 1) : nivel.nombre;
            CrearBoton(panelNiveles.transform, texto, new Vector2(0f, 150f - i * 95f), new Vector2(560f, 80f), 38, () => CargarNivel(nivel));
        }

        CrearBoton(panelNiveles.transform, "Volver", new Vector2(0f, -230f), new Vector2(320f, 70f), 36, MostrarPrincipal);
        textoEstado = CrearTexto(panelNiveles.transform, "Estado", "", 22, new Vector2(0f, -290f), new Vector2(780f, 40f), new Color(1f, 0.85f, 0.3f));

        // Puntero laser propio: rayo recto, punto de mira y gatillo para elegir
        PunteroMenuVR puntero = canvasGO.AddComponent<PunteroMenuVR>();
        puntero.canvas = canvas;

        MostrarPrincipal();
    }

    // ---------------------------------------------------------------- ayudas para crear UI

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
}