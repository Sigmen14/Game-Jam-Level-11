# Three gameplay challenges — Last Ember

## Strong Grey Wind
The existing randomized WindController gives a 3-second telegraph and includes the cover/Space instruction before the gust. During a gust the player receives a directional push scaled by the announced strength. WASD can counter-steer; Space or an existing CoverZone cancels the push. GameManager binds the same scene WindController to PlayerController and FlameMeter, so movement and fuel loss respond to the same event.

## Timed cinder vents
Three pre-placed PF_CinderVent prefab instances form the direct central lane toward the right-side fuel. Each cycles through a safe cooldown, an amber warning, then a red active phase with a solid collider and contact flame damage. Phase offsets are staggered to make the player read and time each crossing. Timing, damage and colors are editable on the prefab.

## Route choice
At the centre-right fork, the existing HUD warning card changes to a route prompt. The short central route crosses the timed vents; the longer upper detour avoids them and passes through existing upper cover zones. Leaving the fork hides the hint; wind warnings take priority.

## Local Unity checklist
1. Open Assets/Scenes/Main.unity in the project's configured Unity version and enter Play Mode.
2. Start a match and confirm the timer begins at 03:00, WASD movement still works and four fuel pickups restore flame.
3. Wait for the wind warning; it should show roughly 3 seconds before the gust and tell the player to seek cover or hold Space.
4. During the gust, steer against it and observe physical drift. Hold Space to cancel drift while keeping the established slower protected movement; repeat inside the existing cover zones.
5. Approach the central fork and read the route prompt. Cross the short route by timing three vent cooldown windows; active vents should block movement and drain flame on contact.
6. Repeat using the longer upper detour through cover and confirm the vents are avoided.
7. Pause/resume, retry and return to menu. Confirm hazards, wind and prompts reset without changing the 180-second timer, fuel or story flow.

## Test status
Source and serialized links were reviewed statically. Unity Editor was not available in this GitHub editing session: C# compilation, prefab/scene import and Play Mode were **not run**.
