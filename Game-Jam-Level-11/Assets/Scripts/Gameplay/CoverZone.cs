using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Reports when the player enters or leaves this 2D trigger.
/// Connect CoverStateChanged to PlayerController.SetInCover(bool) in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class CoverZone : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private UnityEvent<bool> coverStateChanged = new UnityEvent<bool>();

    // Each player is tracked separately, and each of its colliders is counted only once.
    private readonly Dictionary<Transform, HashSet<Collider2D>> _playerColliders =
        new Dictionary<Transform, HashSet<Collider2D>>();

    /// <summary>Persistent UnityEvent used by the Editor setup to connect the scene player.</summary>
    public UnityEvent<bool> CoverStateChanged => coverStateChanged;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayerRoot(other, out Transform playerRoot))
        {
            return;
        }

        if (!_playerColliders.TryGetValue(playerRoot, out HashSet<Collider2D> colliders))
        {
            colliders = new HashSet<Collider2D>();
            _playerColliders.Add(playerRoot, colliders);
        }

        // Ignore duplicate enter callbacks for the same collider.
        if (!colliders.Add(other))
        {
            return;
        }

        if (colliders.Count == 1)
        {
            coverStateChanged.Invoke(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!TryGetPlayerRoot(other, out Transform playerRoot) ||
            !_playerColliders.TryGetValue(playerRoot, out HashSet<Collider2D> colliders))
        {
            return;
        }

        if (!colliders.Remove(other))
        {
            return;
        }

        if (colliders.Count == 0)
        {
            _playerColliders.Remove(playerRoot);
            coverStateChanged.Invoke(false);
        }
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
}
