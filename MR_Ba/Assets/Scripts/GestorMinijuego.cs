using UnityEngine;

public class GestorMinijuego : MonoBehaviour
{
    [Tooltip("Si esta marcado, cuenta solo todos los birlos de la escena (16)")]
    public bool contarBirlosAutomatico = true;
    public int totalBirlos = 16;

    [Header("Estrellas (segundos)")]
    public float tiempoTresEstrellas = 360f;   // 3 estrellas si terminas en menos de esto (6:00)
    public float tiempoDosEstrellas = 540f;    // 2 estrellas si terminas en menos de esto (9:00). Mas tiempo = 1 estrella

    [Header("Tiempo")]
    [Tooltip("Si esta marcado, el tiempo empieza cuando agarras la llave por primera vez")]
    public bool iniciarAlAgarrarLlave = true;

    private int apretados;
    private float tiempo;
    private bool corriendo;
    private bool terminado;
    private int estrellas;

    // Datos que lee el HUD
    public static GestorMinijuego Instancia { get; private set; }
    public float Tiempo { get { return tiempo; } }
    public bool Empezo { get { return corriendo || terminado; } }
    public bool Terminado { get { return terminado; } }
    public int BirlosApretados { get { return apretados; } }
    public int TotalBirlos { get { return totalBirlos; } }
    public float MejorTiempo { get { return PlayerPrefs.GetFloat("mejor_tiempo", 0f); } }

    void Awake()
    {
        Instancia = this;
    }

    void Start()
    {
        if (contarBirlosAutomatico)
            totalBirlos = FindObjectsByType<Birlo>().Length;

        if (!iniciarAlAgarrarLlave) corriendo = true;
    }

    void OnEnable()
    {
        Birlo.OnApretado += BirloApretado;
        LlaveVR.OnLlaveAgarrada += Iniciar;
    }

    void OnDisable()
    {
        Birlo.OnApretado -= BirloApretado;
        LlaveVR.OnLlaveAgarrada -= Iniciar;
    }

    void Iniciar()
    {
        if (!terminado && !corriendo)
        {
            corriendo = true;
            Debug.Log("[JUEGO] Empieza el tiempo");
        }
    }

    void Update()
    {
        if (corriendo && !terminado) tiempo += Time.deltaTime;
    }

    void BirloApretado()
    {
        apretados++;
        if (apretados >= totalBirlos) Terminar();
    }

    void Terminar()
    {
        terminado = true;
        corriendo = false;

        estrellas = tiempo <= tiempoTresEstrellas ? 3 : (tiempo <= tiempoDosEstrellas ? 2 : 1);
        Debug.Log("[JUEGO] Listo en " + tiempo.ToString("F1") + " s - " + estrellas + " estrellas");

        // Record: se guarda el mejor tiempo
        float mejor = PlayerPrefs.GetFloat("mejor_tiempo", 0f);
        if (mejor <= 0f || tiempo < mejor)
        {
            PlayerPrefs.SetFloat("mejor_tiempo", tiempo);
            PlayerPrefs.Save();
        }

        PantallaResultados pantalla = FindAnyObjectByType<PantallaResultados>();
        if (pantalla == null) pantalla = new GameObject("PantallaResultados").AddComponent<PantallaResultados>();
        pantalla.Mostrar(tiempo, estrellas, tiempoTresEstrellas, tiempoDosEstrellas);
    }

    // Solo se ve en la ventana Game del monitor (no dentro del visor)
    void OnGUI()
    {
        string texto = "Tiempo: " + tiempo.ToString("F1") + "  Birlos apretados: " + apretados + "/" + totalBirlos;
        if (terminado) texto += "  |  Terminado: " + estrellas + " estrellas";
        GUI.Label(new Rect(10f, 10f, 600f, 30f), texto);
    }
}