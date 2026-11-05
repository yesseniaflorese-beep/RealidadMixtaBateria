using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pantalla de resultados flotante en VR. La crea sola el GestorMinijuego, no hay que ponerla en la escena.
public class PantallaResultados : MonoBehaviour
{
    public float distancia = 1.6f;     // a cuantos metros frente a la cara aparece
    public float anchoMetros = 1.4f;   // que tan ancha se ve

    private Sprite spriteEstrella;

    public void Mostrar(float tiempo, int estrellas, float tiempoTres, float tiempoDos)
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("PantallaResultados: no encontre ninguna camara.");
            return;
        }

        // Posicion: frente a la cara, a la altura de los ojos
        Vector3 adelante = cam.transform.forward;
        adelante.y = 0f;
        if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
        adelante.Normalize();

        Vector3 pos = cam.transform.position + adelante * distancia;
        pos.y = cam.transform.position.y - 0.05f;

        // Canvas en el mundo (visible dentro del visor)
        GameObject go = new GameObject("PantallaResultadosCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800f, 520f);
        rt.localScale = Vector3.one * (anchoMetros / 800f);
        rt.SetPositionAndRotation(pos, Quaternion.LookRotation(adelante));

        // Fondo
        Image fondo = CrearImagen(rt, "Fondo", null, new Color(0.05f, 0.07f, 0.12f, 0.94f));
        RectTransform rf = fondo.rectTransform;
        rf.anchorMin = Vector2.zero;
        rf.anchorMax = Vector2.one;
        rf.offsetMin = Vector2.zero;
        rf.offsetMax = Vector2.zero;

        // Textos
        CrearTexto(rt, "Titulo", "¡Llantas listas!", 56, new Vector2(0f, 200f), new Vector2(760f, 80f), Color.white);
        CrearTexto(rt, "Etiqueta", "Tu tiempo", 34, new Vector2(0f, 130f), new Vector2(760f, 50f), new Color(0.7f, 0.75f, 0.85f));
        CrearTexto(rt, "Tiempo", FormatoTiempo(tiempo), 90, new Vector2(0f, 60f), new Vector2(760f, 110f), new Color(0.4f, 0.9f, 1f));

        string mensaje = estrellas == 3 ? "¡Mecánico experto!" : (estrellas == 2 ? "¡Buen trabajo!" : "¡Terminaste! Intenta hacerlo más rápido.");
        CrearTexto(rt, "Mensaje", mensaje, 38, new Vector2(0f, -150f), new Vector2(760f, 60f), Color.white);

        CrearTexto(rt, "Pie", "3 estrellas: menos de " + FormatoTiempo(tiempoTres) + "   |   2 estrellas: menos de " + FormatoTiempo(tiempoDos),
                   22, new Vector2(0f, -225f), new Vector2(780f, 40f), new Color(0.6f, 0.65f, 0.75f));

        // Estrellas (se encienden una por una)
        spriteEstrella = CrearSpriteEstrella();
        Image[] imagenes = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            Color color = i < estrellas ? new Color(1f, 0.82f, 0.1f) : new Color(0.28f, 0.3f, 0.36f);
            imagenes[i] = CrearImagen(rt, "Estrella" + (i + 1), spriteEstrella, color);
            imagenes[i].rectTransform.sizeDelta = new Vector2(120f, 120f);
            imagenes[i].rectTransform.anchoredPosition = new Vector2((i - 1) * 150f, -50f);
            imagenes[i].rectTransform.localScale = Vector3.zero;
        }
        StartCoroutine(AnimarEstrellas(imagenes));
    }

    IEnumerator AnimarEstrellas(Image[] imagenes)
    {
        yield return new WaitForSeconds(0.4f);

        foreach (Image img in imagenes)
        {
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.3f);
                // pequeno "rebote" al aparecer
                img.rectTransform.localScale = Vector3.one * (k * (1f + 0.25f * Mathf.Sin(k * Mathf.PI)));
                yield return null;
            }
            img.rectTransform.localScale = Vector3.one;
            yield return new WaitForSeconds(0.25f);
        }
    }

    static string FormatoTiempo(float segundos)
    {
        int total = Mathf.FloorToInt(segundos);
        return string.Format("{0:00}:{1:00}", total / 60, total % 60);
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

    static void CrearTexto(Transform padre, string nombre, string texto, float tamano, Vector2 pos, Vector2 size, Color color)
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
    }

    // Dibuja una estrella de 5 puntas como imagen, sin necesitar ningun archivo
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