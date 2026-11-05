using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD en VR: tiempo, estrellas, birlos puestos, estado de cada rueda y una pista de lo que sigue.
// Es un panel en el mundo que te sigue con calma (no queda pegado a la cara) y se oculta al pausar.
// Se arma solo por codigo: basta con poner este script en un objeto vacio de la escena.
public class HudJuego : MonoBehaviour
{
    [Header("Donde se ve (relativo a tu mirada)")]
    public float distancia = 1.25f;     // metros hacia adelante
    public float lateral = -0.55f;      // negativo = a la izquierda, positivo = a la derecha
    public float altura = -0.25f;       // negativo = mas abajo que los ojos
    public float anchoMetros = 0.36f;   // tamano del panel
    public float suavizado = 4f;        // mas alto = te sigue mas rapido

    private const float AnchoPx = 480f;
    private const float AltoPx = 660f;

    // Colores de cada estado del birlo
    private static readonly Color colorEnRueda = new Color(0.38f, 0.43f, 0.52f);   // todavia en la llanta vieja
    private static readonly Color colorSuelto = new Color(0.35f, 0.75f, 1f);       // aflojado, fuera de la llanta
    private static readonly Color colorColocado = new Color(1f, 0.60f, 0.10f);     // en el hueco, falta apretarlo
    private static readonly Color colorApretado = new Color(0.30f, 1f, 0.40f);     // listo
    private static readonly Color colorSuave = new Color(0.65f, 0.70f, 0.80f);
    private static readonly Color estrellaApagada = new Color(0.25f, 0.28f, 0.34f);
    private static readonly Color estrellaEncendida = new Color(1f, 0.82f, 0.10f);

    private Transform camara;
    private Canvas canvas;
    private RectTransform raiz;
    private TextMeshProUGUI txtTiempo, txtRecord, txtFaltan, txtBirlos, txtPista;
    private readonly Image[] estrellas = new Image[3];
    private RectTransform rellenoBarra;
    private readonly List<Image[]> segmentos = new List<Image[]>();
    private readonly List<Birlo[]> birlosPorRueda = new List<Birlo[]>();
    private readonly List<Birlo> todosLosBirlos = new List<Birlo>();
    private float proximaActualizacion;
    private bool colocado;

    void Start()
    {
        Construir();
        BuscarRuedas();
    }

    // ------------------------------------------------------------ construccion

    void Construir()
    {
        GameObject go = new GameObject("HudJuegoCanvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

        raiz = go.GetComponent<RectTransform>();
        raiz.sizeDelta = new Vector2(AnchoPx, AltoPx);
        raiz.localScale = Vector3.one * (anchoMetros / AnchoPx);

        Image fondo = CrearImagen(raiz, "Fondo", null, new Color(0.04f, 0.06f, 0.11f, 0.80f));
        Estirar(fondo.rectTransform);

        // Tiempo
        CrearTexto(raiz, "EtTiempo", "TIEMPO", 26, new Vector2(-120f, 305f), new Vector2(200f, 34f), colorSuave, TextAlignmentOptions.Left);
        txtRecord = CrearTexto(raiz, "Record", "", 22, new Vector2(110f, 305f), new Vector2(230f, 34f), colorSuave, TextAlignmentOptions.Right);
        txtTiempo = CrearTexto(raiz, "Tiempo", "00:00", 84, new Vector2(0f, 250f), new Vector2(440f, 100f), Color.white, TextAlignmentOptions.Center);

        // Estrellas que llevas ganadas segun tu ritmo
        Sprite sprite = CrearSpriteEstrella();
        for (int i = 0; i < 3; i++)
        {
            estrellas[i] = CrearImagen(raiz, "Estrella" + (i + 1), sprite, estrellaEncendida);
            estrellas[i].rectTransform.sizeDelta = new Vector2(52f, 52f);
            estrellas[i].rectTransform.anchoredPosition = new Vector2((i - 1) * 64f, 175f);
        }
        txtFaltan = CrearTexto(raiz, "Faltan", "", 24, new Vector2(0f, 130f), new Vector2(450f, 34f), colorSuave, TextAlignmentOptions.Center);

        // Birlos apretados + barra de progreso
        txtBirlos = CrearTexto(raiz, "Birlos", "Birlos 0/0", 44, new Vector2(0f, 85f), new Vector2(450f, 56f), Color.white, TextAlignmentOptions.Center);

        Image barraFondo = CrearImagen(raiz, "BarraFondo", null, new Color(0.18f, 0.22f, 0.30f, 1f));
        barraFondo.rectTransform.sizeDelta = new Vector2(400f, 24f);
        barraFondo.rectTransform.anchoredPosition = new Vector2(0f, 45f);

        Image relleno = CrearImagen(barraFondo.transform, "Relleno", null, colorApretado);
        rellenoBarra = relleno.rectTransform;
        rellenoBarra.anchorMin = Vector2.zero;
        rellenoBarra.anchorMax = new Vector2(0f, 1f);
        rellenoBarra.offsetMin = Vector2.zero;
        rellenoBarra.offsetMax = Vector2.zero;

        // Leyenda de colores
        string[] nombres = { "En llanta", "Suelto", "Colocado", "Apretado" };
        Color[] colores = { colorEnRueda, colorSuelto, colorColocado, colorApretado };
        for (int i = 0; i < 4; i++)
        {
            float cx = -165f + i * 110f;
            Image cuadro = CrearImagen(raiz, "Leyenda" + i, null, colores[i]);
            cuadro.rectTransform.sizeDelta = new Vector2(18f, 18f);
            cuadro.rectTransform.anchoredPosition = new Vector2(cx - 38f, -225f);
            CrearTexto(raiz, "LeyendaTxt" + i, nombres[i], 18, new Vector2(cx + 16f, -225f), new Vector2(80f, 24f), colorSuave, TextAlignmentOptions.Left);
        }

        // Pista de lo que sigue
        txtPista = CrearTexto(raiz, "Pista", "", 28, new Vector2(0f, -285f), new Vector2(440f, 80f), new Color(1f, 0.9f, 0.4f), TextAlignmentOptions.Center);
    }

    // Una fila por rueda, con un cuadrito por cada birlo (cambia de color segun su estado)
    void BuscarRuedas()
    {
        EstacionRueda[] estaciones = FindObjectsByType<EstacionRueda>();
        System.Array.Sort(estaciones, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        int filas = Mathf.Min(estaciones.Length, 4);
        for (int r = 0; r < filas; r++)
        {
            Birlo[] birlos = estaciones[r].GetComponentsInChildren<Birlo>(true);
            birlosPorRueda.Add(birlos);
            todosLosBirlos.AddRange(birlos);

            float y = -10f - r * 55f;
            CrearTexto(raiz, "EtRueda" + (r + 1), "Rueda " + (r + 1), 30, new Vector2(-150f, y), new Vector2(140f, 40f), Color.white, TextAlignmentOptions.Left);

            float paso = Mathf.Min(62f, 250f / Mathf.Max(1, birlos.Length));
            Image[] segs = new Image[birlos.Length];
            for (int i = 0; i < birlos.Length; i++)
            {
                segs[i] = CrearImagen(raiz, "Seg", null, colorEnRueda);
                segs[i].rectTransform.sizeDelta = new Vector2(paso - 8f, 30f);
                segs[i].rectTransform.anchoredPosition = new Vector2(-10f + i * paso, y);
            }
            segmentos.Add(segs);
        }
    }

    // ------------------------------------------------------------ actualizacion

    void Update()
    {
        if (canvas == null) return;

        // Se oculta mientras el juego esta en pausa
        bool pausa = Time.timeScale == 0f;
        if (canvas.gameObject.activeSelf == pausa)
            canvas.gameObject.SetActive(!pausa);
        if (pausa) return;

        GestorMinijuego g = GestorMinijuego.Instancia;
        ActualizarTiempo(g);

        if (Time.unscaledTime >= proximaActualizacion)
        {
            proximaActualizacion = Time.unscaledTime + 0.2f;
            ActualizarProgreso(g);
        }
    }

    void ActualizarTiempo(GestorMinijuego g)
    {
        float t = g != null ? g.Tiempo : 0f;
        txtTiempo.text = Formato(t);

        if (g == null)
        {
            txtRecord.text = "";
            return;
        }

        bool tres = t <= g.tiempoTresEstrellas;
        bool dos = t <= g.tiempoDosEstrellas;

        // El tiempo cambia de color segun cuantas estrellas llevas "aseguradas"
        txtTiempo.color = tres ? new Color(0.4f, 0.95f, 1f) : (dos ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.3f));

        estrellas[0].color = estrellaEncendida;
        estrellas[1].color = dos ? estrellaEncendida : estrellaApagada;
        estrellas[2].color = tres ? estrellaEncendida : estrellaApagada;

        if (g.Terminado) txtFaltan.text = "¡Completado!";
        else if (tres) txtFaltan.text = "Quedan " + Formato(g.tiempoTresEstrellas - t) + " para 3 estrellas";
        else if (dos) txtFaltan.text = "Quedan " + Formato(g.tiempoDosEstrellas - t) + " para 2 estrellas";
        else txtFaltan.text = "Termina para ganar tu estrella";

        float mejor = g.MejorTiempo;
        txtRecord.text = mejor > 0f ? "Récord " + Formato(mejor) : "Sin récord";
    }

    void ActualizarProgreso(GestorMinijuego g)
    {
        int nEnRueda = 0, nSuelto = 0, nColocado = 0, nApretado = 0;

        foreach (Birlo b in todosLosBirlos)
        {
            if (b == null) continue;
            switch (b.estado)
            {
                case Birlo.Estado.EnRueda: nEnRueda++; break;
                case Birlo.Estado.Suelto: nSuelto++; break;
                case Birlo.Estado.Colocado: nColocado++; break;
                case Birlo.Estado.Apretado: nApretado++; break;
            }
        }

        int apretados = g != null ? g.BirlosApretados : nApretado;
        int total = g != null ? g.TotalBirlos : todosLosBirlos.Count;

        txtBirlos.text = "Birlos " + apretados + "/" + total;
        float avance = total > 0 ? Mathf.Clamp01((float)apretados / total) : 0f;
        rellenoBarra.anchorMax = new Vector2(avance, 1f);

        // Un cuadrito por birlo, de cada rueda
        for (int r = 0; r < segmentos.Count; r++)
        {
            for (int i = 0; i < segmentos[r].Length; i++)
            {
                Birlo b = birlosPorRueda[r][i];
                if (b == null) continue;
                segmentos[r][i].color = ColorDeEstado(b.estado);
            }
        }

        bool empezo = g != null && g.Empezo;
        bool terminado = g != null && g.Terminado;
        txtPista.text = CalcularPista(empezo, terminado, nEnRueda, nSuelto, nColocado);
        txtPista.color = terminado ? colorApretado : new Color(1f, 0.9f, 0.4f);
    }

    static Color ColorDeEstado(Birlo.Estado estado)
    {
        switch (estado)
        {
            case Birlo.Estado.Suelto: return colorSuelto;
            case Birlo.Estado.Colocado: return colorColocado;
            case Birlo.Estado.Apretado: return colorApretado;
            default: return colorEnRueda;
        }
    }

    static string CalcularPista(bool empezo, bool terminado, int nEnRueda, int nSuelto, int nColocado)
    {
        if (terminado) return "¡Todas las llantas listas!";
        if (!empezo) return "Agarra la llave con el grip para empezar.   Y = pausa";
        if (nColocado > 0) return "Aprieta los birlos naranjas con el gatillo.";
        if (nSuelto > 0) return "Monta la llanta nueva y coloca los birlos sueltos en los huecos.";
        if (nEnRueda > 0) return "Afloja los birlos con la punta de la llave y el gatillo.";
        return "";
    }

    // El panel te sigue con calma: no queda pegado a la cara, asi no marea y no estorba
    void LateUpdate()
    {
        if (canvas == null || !canvas.gameObject.activeSelf) return;

        if (camara == null)
        {
            Camera c = Camera.main;
            if (c == null) c = FindAnyObjectByType<Camera>();
            if (c != null) camara = c.transform;
            if (camara == null) return;
        }

        Vector3 adelante = camara.forward;
        adelante.y = 0f;
        if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
        adelante.Normalize();
        Vector3 derecha = Vector3.Cross(Vector3.up, adelante);

        Vector3 objetivo = camara.position + adelante * distancia + derecha * lateral + Vector3.up * altura;

        float k = 1f - Mathf.Exp(-suavizado * Time.unscaledDeltaTime);
        if (!colocado)
        {
            raiz.position = objetivo;
            colocado = true;
            k = 1f;
        }
        else
        {
            raiz.position = Vector3.Lerp(raiz.position, objetivo, k);
        }

        // De frente a tu cara
        Vector3 haciaAfuera = raiz.position - camara.position;
        if (haciaAfuera.sqrMagnitude > 0.0001f)
        {
            Quaternion rotObjetivo = Quaternion.LookRotation(haciaAfuera, Vector3.up);
            raiz.rotation = k >= 1f ? rotObjetivo : Quaternion.Slerp(raiz.rotation, rotObjetivo, k);
        }
    }

    // ------------------------------------------------------------ ayudas

    static string Formato(float segundos)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(segundos));
        return string.Format("{0:00}:{1:00}", total / 60, total % 60);
    }

    static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    static Image CrearImagen(Transform padre, string nombre, Sprite sprite, Color color)
    {
        GameObject g = new GameObject(nombre);
        g.transform.SetParent(padre, false);
        Image img = g.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI CrearTexto(Transform padre, string nombre, string texto, float tamano, Vector2 pos, Vector2 size, Color color, TextAlignmentOptions alineacion)
    {
        GameObject g = new GameObject(nombre);
        g.transform.SetParent(padre, false);

        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        t.text = texto;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = color;
        t.raycastTarget = false;

        RectTransform r = t.rectTransform;
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return t;
    }

    // Estrella de 5 puntas dibujada por codigo (no necesita ningun archivo)
    static Sprite CrearSpriteEstrella()
    {
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2[] poligono = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float angulo = Mathf.PI / 2f + i * Mathf.PI / 5f;
            float radio = (i % 2 == 0) ? n * 0.48f : n * 0.19f;
            poligono[i] = new Vector2(n / 2f + Mathf.Cos(angulo) * radio, n / 2f + Mathf.Sin(angulo) * radio);
        }

        Color[] pixeles = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                bool dentro = PuntoDentro(poligono, new Vector2(x + 0.5f, y + 0.5f));
                pixeles[y * n + x] = dentro ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }

        tex.SetPixels(pixeles);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool PuntoDentro(Vector2[] poly, Vector2 p)
    {
        bool dentro = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                dentro = !dentro;
        }
        return dentro;
    }
}
