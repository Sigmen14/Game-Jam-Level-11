# 05. Автоматизация первичной настройки проекта

Unity Editor недоступен в среде подготовки изменений, поэтому сцены и Prefab создаются специальной Editor-командой при первом запуске проекта на машине разработчика.

## Запуск

1. Получить ветку `feature/project-setup` и открыть папку `Game-Jam-Level-11` в Unity **6000.0.83f1**.
2. Дождаться завершения импорта и компиляции скрипта.
3. В меню Unity выбрать **Game Jam → Project Setup → Create Missing Setup Assets**.

Команду можно запускать повторно: существующие Prefab и `Main.unity` не перезаписываются.

## Что создаётся

- `Assets/Prefabs/Player/PF_Player.prefab` — SpriteRenderer, Rigidbody2D, CircleCollider2D, выключенный GuardVisual и Light2D.
- `Assets/Prefabs/Gameplay/PF_FuelPickup.prefab` — SpriteRenderer и trigger CircleCollider2D.
- `Assets/Prefabs/Gameplay/PF_Cover.prefab` — SpriteRenderer и BoxCollider2D.
- `Assets/Prefabs/UI/UI_HUD.prefab` — индикатор пламени, таймер и скрытый текст предупреждения.
- `Assets/Prefabs/UI/UI_StartPanel.prefab` — заголовок, подсказки и кнопка Start.
- `Assets/Prefabs/UI/UI_ResultPanel.prefab` — заголовок результата, время и кнопка Retry.
- `Assets/Scenes/Main.unity` — создаётся копированием `SampleScene.unity`; исходная сцена не меняется.
- `Main.unity` добавляется в Build Settings, остальные сцены сохраняются.

В Main размещается стартовая иерархия Main / Level / Player / GameSystems / UI, экземпляры трёх UI-панелей и EventSystem с InputSystemUIInputModule. GameManager и WindController пока являются пустыми объектами-органайзерами, без игровых скриптов.

## Важные ограничения

- Это Editor-only утилита в `Assets/Editor`; она не попадает в игровой runtime.
- GameObject и компоненты создаются только во время явного запуска команды в Unity Editor.
- В существующие Prefab и существующую `Main.unity` утилита не вносит изменений.
- Спрайты-заглушки не назначены: визуальные ассеты добавляются отдельно.
- Кнопки Start/Retry пока не связаны с GameManager — это только UI-заготовки, не игровая система.
- Unity Editor здесь не запускался; созданные Unity-ассеты и сцену нужно проверить после выполнения команды в редакторе.
