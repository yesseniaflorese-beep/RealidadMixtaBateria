using System.Reflection;
using UnityEngine;

// Guarda y aplica las opciones de movimiento (se usa desde el menu de pausa y, mas adelante, desde el menu principal).
// Las opciones se guardan con PlayerPrefs, asi se conservan al cambiar de escena.
public static class AjustesMovimiento
{
    private const string ClaveMovimiento = "ajuste_movimiento_suave";
    private const string ClaveGiro = "ajuste_giro_suave";

    // true = movimiento suave (joystick) | false = teletransporte
    public static bool MovimientoSuave
    {
        get { return PlayerPrefs.GetInt(ClaveMovimiento, 1) == 1; }
        set { PlayerPrefs.SetInt(ClaveMovimiento, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // true = giro suave | false = giro por pasos (snap)
    public static bool GiroSuave
    {
        get { return PlayerPrefs.GetInt(ClaveGiro, 0) == 1; }
        set { PlayerPrefs.SetInt(ClaveGiro, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void Aplicar()
    {
        bool movSuave = MovimientoSuave;
        bool giroSuave = GiroSuave;

        int managers = 0, snap = 0, giroCont = 0, movimiento = 0, teleport = 0, rayosTp = 0, areas = 0;
        object capaTeleport = null;

        MonoBehaviour[] todos = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

        foreach (MonoBehaviour m in todos)
        {
            string tipo = m.GetType().Name;

            // 1) Controlador de Starter Assets: cada mano se configura distinto
            if (tipo == "ControllerInputActionManager" || tipo == "ActionBasedControllerManager")
            {
                string ruta = Ruta(m.transform).ToLower();
                bool izquierda = ruta.Contains("left");
                bool derecha = ruta.Contains("right");

                // Movimiento suave: SOLO la mano izquierda mueve. La derecha queda libre para girar.
                // Teletransporte: las dos manos quedan sin mover (adelante = teletransportar, a los lados = girar).
                bool estaManoMueve = movSuave && !derecha;

                bool a = PonerBool(m, "smoothMotionEnabled", estaManoMueve);
                bool b = PonerBool(m, "smoothTurnEnabled", giroSuave);
                if (a || b) managers++;

                Debug.Log("[MOVIMIENTO] " + Ruta(m.transform) + " -> mover: " + estaManoMueve
                          + ", giro suave: " + giroSuave + (izquierda ? " (mano izquierda)" : derecha ? " (mano derecha)" : ""));
            }
            // 2) Proveedores de movimiento y giro
            else if (tipo == "SnapTurnProvider") { m.enabled = !giroSuave; snap++; }
            else if (tipo == "ContinuousTurnProvider") { m.enabled = giroSuave; giroCont++; }
            else if (tipo == "TeleportationProvider") { m.enabled = !movSuave; teleport++; }
            else if (tipo.EndsWith("MoveProvider") && !tipo.StartsWith("Grab")) { m.enabled = movSuave; movimiento++; }
            // 3) El rayo de teletransporte: apagado en movimiento suave (para que no salga la linea sin hacer nada)
            else if (tipo == "XRRayInteractor" && m.gameObject.name.ToLower().Contains("teleport"))
            {
                m.enabled = !movSuave;
                rayosTp++;
                if (capaTeleport == null) capaTeleport = LeerPropiedad(m, "interactionLayers");
            }
        }

        // 4) Las areas de teletransporte (el piso) deben usar la misma capa de interaccion que el rayo,
        //    si no, el rayo las ignora y se ve la linea pero no pasa nada.
        if (capaTeleport != null)
        {
            foreach (MonoBehaviour m in todos)
            {
                string tipo = m.GetType().Name;
                if (tipo == "TeleportationArea" || tipo == "TeleportationAnchor")
                {
                    EscribirPropiedad(m, "interactionLayers", capaTeleport);
                    areas++;
                }
            }
        }
        else
        {
            foreach (MonoBehaviour m in todos)
            {
                string tipo = m.GetType().Name;
                if (tipo == "TeleportationArea" || tipo == "TeleportationAnchor") areas++;
            }
        }

        Debug.Log("[MOVIMIENTO] Aplicado: " + (movSuave ? "movimiento suave" : "teletransporte")
                  + " + " + (giroSuave ? "giro suave" : "giro por pasos")
                  + " | encontrados -> controladores: " + managers + ", giro snap: " + snap
                  + ", giro suave: " + giroCont + ", mover: " + movimiento
                  + ", teletransporte: " + teleport + ", rayos de teletransporte: " + rayosTp
                  + ", areas de teletransporte: " + areas);

        if (!movSuave && teleport == 0)
            Debug.LogWarning("[MOVIMIENTO] Pediste teletransporte, pero no encontre ningun TeleportationProvider.");
        if (!movSuave && rayosTp == 0)
            Debug.LogWarning("[MOVIMIENTO] No encontre el rayo de teletransporte (un objeto 'Teleport Interactor' con XRRayInteractor).");
        if (!movSuave && areas == 0)
            Debug.LogWarning("[MOVIMIENTO] No hay ningun Teleportation Area en la escena: agrega uno al piso (Floor).");
        if (snap == 0 && giroCont == 0 && managers == 0)
            Debug.LogWarning("[MOVIMIENTO] No encontre ningun componente de giro. Revisa que el XR Origin tenga locomocion.");
    }

    // Para ver en la Console que componentes de locomocion tiene tu rig
    public static void Listar()
    {
        foreach (MonoBehaviour m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            string tipo = m.GetType().Name;
            if (tipo.Contains("Provider") || tipo.Contains("Manager") || tipo.Contains("Teleport") || tipo.Contains("Locomotion"))
                Debug.Log("[LOCOMOCION] " + tipo + " en '" + Ruta(m.transform) + "' (activo: " + m.enabled + ")");
        }
    }

    private static string Ruta(Transform t)
    {
        string ruta = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            ruta = t.name + "/" + ruta;
        }
        return ruta;
    }

    private static bool PonerBool(object objeto, string nombre, bool valor)
    {
        PropertyInfo prop = objeto.GetType().GetProperty(
            nombre, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
        {
            prop.SetValue(objeto, valor);
            return true;
        }
        return false;
    }

    private static object LeerPropiedad(object objeto, string nombre)
    {
        PropertyInfo prop = objeto.GetType().GetProperty(
            nombre, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return (prop != null && prop.CanRead) ? prop.GetValue(objeto) : null;
    }

    private static void EscribirPropiedad(object objeto, string nombre, object valor)
    {
        PropertyInfo prop = objeto.GetType().GetProperty(
            nombre, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (prop != null && prop.CanWrite && valor != null && prop.PropertyType == valor.GetType())
            prop.SetValue(objeto, valor);
    }
}