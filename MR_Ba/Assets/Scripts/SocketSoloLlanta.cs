using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Es un XR Socket Interactor normal, pero solo acepta objetos que tengan el script LlantaNueva.
// Se pone en SocketLlanta EN LUGAR de "XR Socket Interactor".
public class SocketSoloLlanta : XRSocketInteractor
{
    private bool EsLlanta(object interactable)
    {
        Component c = interactable as Component;
        return c != null && c.GetComponentInParent<LlantaNueva>() != null;
    }

    public override bool CanHover(IXRHoverInteractable interactable)
    {
        return base.CanHover(interactable) && EsLlanta(interactable);
    }

    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        return base.CanSelect(interactable) && EsLlanta(interactable);
    }
}
