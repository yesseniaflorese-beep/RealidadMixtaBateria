using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action<float> OnPenalty;
    public static event Action<string> OnMinigameCompleted;

    public static void Penalty(float seconds)
    {
        Debug.Log($"Penalización: +{seconds} s");
        OnPenalty?.Invoke(seconds);
    }

    public static void Completed(string minigameId)
    {
        Debug.Log($"Minijuego completado: {minigameId}");
        OnMinigameCompleted?.Invoke(minigameId);
    }
}