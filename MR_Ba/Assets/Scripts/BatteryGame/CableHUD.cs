using System.Collections;
using TMPro;
using UnityEngine;

public class CableHUD : MonoBehaviour
{
    public TMP_Text counterText, messageText;

    void OnEnable()
    {
        TerminalLogic.OnConnectionsChanged += UpdateCounter;
        TerminalLogic.OnPenalty += ShowPenalty;
        TerminalLogic.OnAllConnected += ShowWin;
    }

    void OnDisable()
    {
        TerminalLogic.OnConnectionsChanged -= UpdateCounter;
        TerminalLogic.OnPenalty -= ShowPenalty;
        TerminalLogic.OnAllConnected -= ShowWin;
    }

    void Start()
    {
        UpdateCounter(0);
        messageText.text = "";
    }

    void UpdateCounter(int n) => counterText.text = $"Conexiones: {n} / 4";
    void ShowPenalty(float s) => Show($"¡Polaridad incorrecta!  -{s} s", Color.red, 2f);
    void ShowWin() => Show("¡Batería conectada!", Color.green, 0f);

    void Show(string text, Color color, float seconds)
    {
        StopAllCoroutines();
        messageText.text = text;
        messageText.color = color;
        if (seconds > 0f) StartCoroutine(Hide(seconds));
    }

    IEnumerator Hide(float s)
    {
        yield return new WaitForSeconds(s);
        messageText.text = "";
    }
}