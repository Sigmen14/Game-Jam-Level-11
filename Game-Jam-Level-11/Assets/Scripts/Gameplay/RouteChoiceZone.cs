using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class RouteChoiceZone : MonoBehaviour
{
    [SerializeField] private HUDView hudView;
    private PlayerController trackedPlayer;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;
        trackedPlayer = player;
        RefreshHint();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null) trackedPlayer = player;
        RefreshHint();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != trackedPlayer) return;
        trackedPlayer = null;
        RefreshHint();
    }

    private void RefreshHint()
    {
        if (hudView != null)
            hudView.SetRouteChoiceHint(trackedPlayer != null && trackedPlayer.IsGameplayActive);
    }

    private void OnDisable()
    {
        trackedPlayer = null;
        if (hudView != null) hudView.SetRouteChoiceHint(false);
    }
}
