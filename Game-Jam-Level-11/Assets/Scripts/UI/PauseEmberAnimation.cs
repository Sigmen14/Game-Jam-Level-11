using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a pre-sliced ember sprite sheet on an existing UI Image.
/// Uses unscaled time so the embers keep moving while the game is paused.
/// </summary>
[DisallowMultipleComponent]
public sealed class PauseEmberAnimation : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 12f;

    private int _frameIndex;
    private float _elapsed;

    private void OnEnable()
    {
        _frameIndex = 0;
        _elapsed = 0f;
        ApplyFrame();
    }

    private void Update()
    {
        if (targetImage == null || frames == null || frames.Length == 0)
            return;

        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        _elapsed += Time.unscaledDeltaTime;

        if (_elapsed < frameDuration)
            return;

        int framesToAdvance = Mathf.FloorToInt(_elapsed / frameDuration);
        _elapsed -= framesToAdvance * frameDuration;
        _frameIndex = (_frameIndex + framesToAdvance) % frames.Length;
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (targetImage != null && frames != null && frames.Length > 0)
            targetImage.sprite = frames[_frameIndex];
    }

    private void OnValidate()
    {
        framesPerSecond = Mathf.Max(1f, framesPerSecond);
    }
}