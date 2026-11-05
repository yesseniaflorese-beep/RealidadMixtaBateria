using System;
using UnityEngine;

public class TerminalLogic : MonoBehaviour
{
    public static event Action<int> OnConnectionsChanged;
    public static event Action<float> OnPenalty;      // lo usa el panel de cables
    public static event Action OnAllConnected;
    public static int connectedCorrect;
    public static bool Completed;

    public Polarity polarity;
    public ParticleSystem sparks;
    public float penalty = 3f;
    public int totalNeeded = 4;

    bool correct;

    void Awake()
    {
        connectedCorrect = 0;
        Completed = false;
    }

    // Devuelve true si la conexión es correcta
    public bool TryConnect(ClampPolarity clamp)
    {
        if (Completed) return false;

        if (clamp.polarity != polarity)
        {
            if (sparks) sparks.Play();
            Debug.Log($"Polaridad incorrecta: -{penalty} s");
            OnPenalty?.Invoke(penalty);
            GameEvents.Penalty(penalty);              // resta tiempo en el manager global
            return false;
        }

        correct = true;
        connectedCorrect++;
        Debug.Log($"Conexión correcta ({connectedCorrect}/{totalNeeded})");
        OnConnectionsChanged?.Invoke(connectedCorrect);

        if (connectedCorrect >= totalNeeded)
        {
            Completed = true;
            OnAllConnected?.Invoke();
            GameEvents.Completed("Battery");          // avisa al manager global
        }
        return true;
    }

    public void Disconnect()
    {
        if (!correct || Completed || !GameFlowManager.Playing) return;
        correct = false;
        connectedCorrect--;
        OnConnectionsChanged?.Invoke(connectedCorrect);
    }
}