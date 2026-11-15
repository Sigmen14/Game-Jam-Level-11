using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tracks overlapping player colliders and reports cover entry/exit.
/// Connect CoverStateChanged to PlayerController.SetInCover(bool) in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class CoverZone : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private UnityEvent<bool> coverStateChanged = new UnityEvent<bool>();

    private readonly Dictionary<Transform, HashSet<Collider2D>> playerColliders =
        new Dictionary<Transform, HashSet<Collider2D>>();

    public UnityEvent<bool> CoverStateChanged => coverStateChanged;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayerRoot(other, out Transform playerRoot))
            return;

        if (!playerColliders.TryGetValue(playerRoot, out HashSet<Collider2D> colliders))
        {
            colliders = new HashSet<Collider2D>();
            playerColliders.Add(playerRoot, colliders);
        }

        if (!colliders.Add(other))
            return;

        if (colliders.Count == 1)
            coverStateChanged.Invoke(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!TryGetPlayerRoot(other, out Transform playerRoot) ||
            !playerColliders.TryGetValue(playerRoot, out HashSet<Collider2D> colliders))
            return;

        if (!colliders.Remove(other))
            return;

        if (colliders.Count == 0)
        {
            playerColliders.Remove(playerRoot);
            coverStateChanged.Invoke(false);
        }
    }

    /// <summary>Clears stale trigger state when the match is restarted or returned to menu.</summary>
    public void ResetZone()
    {
        bool wasOccupied = playerColliders.Count > 0;
        playerColliders.Clear();

        if (wasOccupied)
            coverStateChanged.Invoke(false);
    }

    private bool TryGetPlayerRoot(Collider2D other, out Transform playerRoot)
    {
        Transform candidate = other.transform;

        while (candidate != null)
        {
            if (candidate.CompareTag(playerTag))
            {
                playerRoot = candidate;
                return true;
            }

            candidate = candidate.parent;
        }

        playerRoot = null;
        return false;
    }

    private void OnDisable()
    {
        ResetZone();
    }
}
