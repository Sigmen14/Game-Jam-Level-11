using UnityEngine;

/// <summary>
/// Supports both the route director placed on GameSystems and pre-placed rotating
/// obstacle instances. No gameplay objects are created at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayChallengeController : MonoBehaviour
{
    public enum ControllerMode
    {
        RouteDirector,
        RotatingObstacle
    }

    private enum RouteState
    {
        Waiting,
        Choosing,
        ShortDangerous,
        LongSafe,
        Completed,
        Finished
    }

    [Header("Mode")]
    [SerializeField] private ControllerMode mode = ControllerMode.RouteDirector;

    [Header("Rotating obstacle")]
    [SerializeField, Range(0f, 85f)] private float rotationAmplitude = 66f;
    [SerializeField, Min(0f)] private float rotationSpeed = 1.5f;
    [SerializeField] private float phaseOffset;
    [SerializeField, Min(0f)] private float collisionDamage = 11f;
    [SerializeField, Min(0f)] private float knockbackImpulse = 4f;
    [SerializeField, Min(0.1f)] private float hitCooldown = 0.85f;

    [Header("Route junction")]
    [SerializeField, Min(0f)] private float decisionStartX = 1f;
    [SerializeField, Min(0f)] private float decisionLockX = 3.5f;
    [SerializeField] private float shortLaneThreshold = 1.35f;
    [SerializeField] private float safeLaneThreshold = -1.35f;

    [Header("Route exits")]
    [SerializeField] private float shortFinishX = 6.55f;
    [SerializeField] private float shortFinishY = 3.05f;
    [SerializeField] private float safeFinishX = 7.75f;
    [SerializeField] private float safeFinishY = -3.05f;
    [SerializeField, Min(0.1f)] private float finishLaneTolerance = 1.05f;

    [Header("Route rewards")]
    [SerializeField, Min(0f)] private float shortRouteFuelReward = 14f;
    [SerializeField, Min(0f)] private float safeRouteFuelReward = 8f;
    [SerializeField, Min(0f)] private float resultMessageDuration = 4f;

    public bool IsRotatingObstacle => mode == ControllerMode.RotatingObstacle;
    public bool IsRouteDirector => mode == ControllerMode.RouteDirector;
    public float CollisionDamage => collisionDamage;
    public float KnockbackImpulse => knockbackImpulse;

    private PlayerController player;
    private Rigidbody2D obstacleBody;
    private FlameMeter flameMeter;
    private HUDView hudView;
    private RouteState routeState;
    private bool isRunning;
    private float baseAngle;
    private float elapsed;
    private float nextHitTime;
    private float resultMessageTime;

    private void Awake()
    {
        obstacleBody = GetComponent<Rigidbody2D>();
        baseAngle = transform.localEulerAngles.z;

        if (IsRotatingObstacle)
            ValidateRotatingObstaclePhysics();

        ResetMotion();
        ResetRoute();
    }

    private void ValidateRotatingObstaclePhysics()
    {
        if (obstacleBody == null)
        {
            Debug.LogError("GameplayChallengeController: rotating obstacle needs a root Rigidbody2D.", this);
        }
        else if (obstacleBody.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogError("GameplayChallengeController: rotating obstacle Rigidbody2D must be Kinematic.", this);
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        bool hasSolidCollider = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled && !colliders[i].isTrigger)
            {
                hasSolidCollider = true;
                break;
            }
        }

        if (!hasSolidCollider)
            Debug.LogError("GameplayChallengeController: rotating obstacle needs an enabled non-trigger Collider2D.", this);
    }

    public void Bind(PlayerController playerController, FlameMeter meter, HUDView hud)
    {
        player = playerController;
        flameMeter = meter;
        hudView = hud;
    }

    public void SetRunning(bool running)
    {
        isRunning = running;

        if (IsRotatingObstacle)
        {
            ResetMotion();
        }
        else
        {
            ResetRoute();
            if (running)
            {
                ShowMessage("ДОЙДИ ДО РАЗВИЛКИ ВПРАВО: ВВЕРХ — КОРОТКИЙ ОПАСНЫЙ ПУТЬ; ВНИЗ — ДЛИННЫЙ БЕЗОПАСНЫЙ");
            }
        }
    }

    private void Update()
    {
        if (!isRunning)
            return;

        if (IsRotatingObstacle)
            return;

        UpdateRoute();
    }

    private void FixedUpdate()
    {
        if (!isRunning || !IsRotatingObstacle || obstacleBody == null)
            return;

        elapsed += Time.fixedDeltaTime;
        float angle = baseAngle + Mathf.Sin(elapsed * rotationSpeed + phaseOffset) * rotationAmplitude;
        obstacleBody.MoveRotation(angle);
    }

    public bool TryConsumeHit()
    {
        if (!IsRotatingObstacle || !isRunning || Time.time < nextHitTime)
            return false;

        nextHitTime = Time.time + hitCooldown;
        return true;
    }

    private void UpdateRoute()
    {
        if (player == null || !player.IsGameplayActive)
            return;

        if (routeState == RouteState.Completed)
        {
            resultMessageTime -= Time.deltaTime;
            if (resultMessageTime <= 0f)
            {
                routeState = RouteState.Finished;
                if (hudView != null)
                    hudView.HideChallengeMessage();
            }
            return;
        }

        if (routeState == RouteState.Finished)
            return;

        Vector2 position = player.transform.position;

        if (routeState == RouteState.Waiting && position.x >= decisionStartX)
        {
            routeState = RouteState.Choosing;
            ShowMessage("РАЗВИЛКА: ВВЕРХ — КОРОТКО И ОПАСНО (+14); ВНИЗ — ДОЛЬШЕ И БЕЗОПАСНЕЕ (+8)");
        }

        if (routeState == RouteState.Choosing)
        {
            if (position.y >= shortLaneThreshold)
            {
                SelectRoute(true);
            }
            else if (position.y <= safeLaneThreshold)
            {
                SelectRoute(false);
            }
            else if (position.x >= decisionLockX)
            {
                // Commit to the nearest lane. Neutral input defaults to the safer
                // lower lane rather than silently forcing the player into hazards.
                SelectRoute(position.y > 0f);
            }
        }

        if (routeState == RouteState.ShortDangerous &&
            position.x >= shortFinishX &&
            Mathf.Abs(position.y - shortFinishY) <= finishLaneTolerance)
        {
            CompleteRoute(true);
        }
        else if (routeState == RouteState.LongSafe &&
                 position.x >= safeFinishX &&
                 Mathf.Abs(position.y - safeFinishY) <= finishLaneTolerance)
        {
            CompleteRoute(false);
        }
    }

    private void SelectRoute(bool tookShortRoute)
    {
        routeState = tookShortRoute ? RouteState.ShortDangerous : RouteState.LongSafe;
        ShowMessage(tookShortRoute
            ? "КОРОТКИЙ ПУТЬ ↑: ДЕРЖИСЬ ВЕРХНЕЙ ЛИНИИ · 3 ВРАЩАЮЩИХСЯ ЗАСОВА · +14 ОГНЯ"
            : "БЕЗОПАСНЫЙ ПУТЬ ↓: ДЕРЖИСЬ НИЖНЕЙ ЛИНИИ · ДОЛЬШЕ, ЗАТО БЕЗ ЗАСОВОВ · +8 ОГНЯ");
    }

    private void CompleteRoute(bool tookShortRoute)
    {
        routeState = RouteState.Completed;
        float reward = tookShortRoute ? shortRouteFuelReward : safeRouteFuelReward;

        if (flameMeter != null)
            flameMeter.AddFuel(reward);

        resultMessageTime = resultMessageDuration;
        ShowMessage(tookShortRoute
            ? string.Format("МАРШРУТ ПРОЙДЕН! БЫСТРО И РИСКОВАННО — +{0:0} ОГНЯ", reward)
            : string.Format("МАРШРУТ ПРОЙДЕН! ДОЛЬШЕ, ЗАТО БЕЗ ОПАСНОСТИ — +{0:0} ОГНЯ", reward));
    }

    private void ShowMessage(string message)
    {
        if (hudView != null)
            hudView.ShowChallengeMessage(message);
    }

    private void ResetMotion()
    {
        elapsed = 0f;
        nextHitTime = 0f;
        transform.localRotation = Quaternion.Euler(0f, 0f, baseAngle);
        if (obstacleBody != null)
            obstacleBody.rotation = baseAngle;
    }

    private void ResetRoute()
    {
        routeState = RouteState.Waiting;
        resultMessageTime = 0f;

        if (hudView == null)
            return;

        if (isRunning)
            ShowMessage("ВЫБЕРИ МАРШРУТ: ИДИ ВПРАВО К РАЗВИЛКЕ · W/↑ — КОРОТКИЙ ОПАСНЫЙ · S/↓ — ДЛИННЫЙ БЕЗОПАСНЫЙ");
        else
            hudView.HideChallengeMessage();
    }

    private void OnValidate()
    {
        decisionLockX = Mathf.Max(decisionStartX + 0.1f, decisionLockX);
        finishLaneTolerance = Mathf.Max(0.1f, finishLaneTolerance);
    }
}
