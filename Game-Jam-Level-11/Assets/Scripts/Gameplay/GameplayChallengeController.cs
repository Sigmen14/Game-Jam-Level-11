using UnityEngine;

/// <summary>Directs the upper-risk / lower-safe route choice and grants its fuel reward.</summary>
[DisallowMultipleComponent]
public sealed class GameplayChallengeController : MonoBehaviour
{
    private enum RouteState { Waiting, Choosing, ShortDangerous, LongSafe, Completed, Finished }

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

    private PlayerController player;
    private FlameMeter flameMeter;
    private HUDView hudView;
    private RouteState routeState;
    private bool isRunning;
    private float resultMessageTime;

    public void Bind(PlayerController playerController, FlameMeter meter, HUDView hud)
    {
        player = playerController;
        flameMeter = meter;
        hudView = hud;
    }

    public void SetRunning(bool running)
    {
        isRunning = running;
        ResetRoute();
    }

    private void Update()
    {
        if (isRunning)
            UpdateRoute();
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
            ShowMessage("РАЗВИЛКА: ВВЕРХ — КОРОТКО И РИСКОВАННО (+14); ВНИЗ — ДОЛЬШЕ И БЕЗОПАСНЕЕ (+8)");
        }

        if (routeState == RouteState.Choosing)
        {
            if (position.y >= shortLaneThreshold)
                SelectRoute(true);
            else if (position.y <= safeLaneThreshold)
                SelectRoute(false);
            else if (position.x >= decisionLockX)
                SelectRoute(position.y > 0f);
        }

        if (routeState == RouteState.ShortDangerous &&
            position.x >= shortFinishX &&
            Mathf.Abs(position.y - shortFinishY) <= finishLaneTolerance)
            CompleteRoute(true);
        else if (routeState == RouteState.LongSafe &&
                 position.x >= safeFinishX &&
                 Mathf.Abs(position.y - safeFinishY) <= finishLaneTolerance)
            CompleteRoute(false);
    }

    private void SelectRoute(bool tookShortRoute)
    {
        routeState = tookShortRoute ? RouteState.ShortDangerous : RouteState.LongSafe;
        ShowMessage(tookShortRoute
            ? "КОРОТКИЙ ПУТЬ ↑: БЫСТРЕЕ, НО ОПАСНЕЕ · +14 ОГНЯ"
            : "БЕЗОПАСНЫЙ ПУТЬ ↓: ДОЛЬШЕ, ЗАТО СПОКОЙНЕЕ · +8 ОГНЯ");
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

    private void ResetRoute()
    {
        routeState = RouteState.Waiting;
        resultMessageTime = 0f;
        if (hudView == null)
            return;

        if (isRunning)
            ShowMessage("ВЫБЕРИ МАРШРУТ: ИДИ ВПРАВО К РАЗВИЛКЕ · W/↑ — КОРОТКИЙ РИСКОВАННЫЙ · S/↓ — ДЛИННЫЙ БЕЗОПАСНЫЙ");
        else
            hudView.HideChallengeMessage();
    }

    private void OnValidate()
    {
        decisionLockX = Mathf.Max(decisionStartX + 0.1f, decisionLockX);
        finishLaneTolerance = Mathf.Max(0.1f, finishLaneTolerance);
    }
}
