# 03. План реализации в Unity

Документ рассчитан на текущий проект: Unity 6000.0.83f1, 2D URP, Input System и uGUI.

## Что уже есть в репозитории

Перед началом не создавай дубликаты существующих заготовок.

- Сцены: Assets/Scenes/Menu.unity (стартовая сцена приложения) и Assets/Scenes/Main.unity (игровая сцена).
- Скрипт Assets/Scripts/Gameplay/CoverZone.cs — отслеживает вход и выход игрока из trigger-укрытия через UnityEvent<bool>.
- Скрипт Assets/Scripts/Gameplay/WindController.cs — уже содержит цикл ожидание → предупреждение → порыв и настройки интервалов в Inspector.
- Префабы: Assets/Prefabs/Player/Player.prefab, Assets/Prefabs/Pickups/PickupRoot.prefab, Assets/Prefabs/Environment/EnvironmentRoot.prefab.
- UI_HUD.prefab уже содержит Canvas, CanvasScaler и GraphicRaycaster, но пока не имеет готовых элементов HUD.
- UI_StartPanel.prefab пока является простой Image-заготовкой.

Заготовки требуют настройки и развития. Не считать, что у Player, PickupRoot или EnvironmentRoot уже реализовано поведение.

## Целевая структура Assets

Сохранять существующую структуру папок и расширять её по необходимости:

- Assets/Prefabs/Player/Player.prefab
- Assets/Prefabs/Pickups/PickupRoot.prefab
- Assets/Prefabs/Environment/EnvironmentRoot.prefab
- Assets/Prefabs/UI/UI_Menu.prefab
- Assets/Prefabs/UI/UI_HUD.prefab
- Assets/Prefabs/UI/UI_StartPanel.prefab
- Assets/Prefabs/UI/UI_ResultPanel.prefab
- Assets/Prefabs/UI/UI_FlameMeter.prefab
- Assets/Prefabs/UI/UI_Timer.prefab
- Assets/Prefabs/UI/UI_WindWarning.prefab
- Assets/Scripts/Gameplay/
- Assets/Scripts/UI/
- Assets/Scripts/Core/
- Assets/Scenes/Menu.unity
- Assets/Scenes/Main.unity

Новые папки и файлы создавать только когда они действительно нужны. Unity-объекты и визуальные элементы создаются в Editor, не в коде.

## Сцена Menu

Menu — первая сцена приложения. Она содержит только меню и его аудио:

- Menu
  - MenuSystems
    - AudioListener
    - Music AudioSource
  - EventSystem
  - UI_Menu (Canvas)
    - UI_StartPanel
      - Играть
      - Настройки
      - Выход
    - UI_SettingsPanel
      - SFX Slider
      - Music Slider
      - Назад

MainMenuController переключает готовые панели и загружает Main через SceneManager. AudioSettingsController применяет значения ползунков к заранее размещённым AudioSource. Все элементы существуют до запуска игры.

## Сцена Main

Main содержит только игровой уровень и HUD:

- Main
  - Main Camera
  - Global Light 2D
  - Level
    - Background
    - Walls
    - Cover_01
    - Cover_02
    - FuelPoint_01
    - FuelPoint_02
    - FuelPoint_03
    - FuelPoint_04
  - Player
  - GameSystems
    - GameManager
    - WindController
    - Audio
  - EventSystem
  - UI_HUD (Canvas)
    - UI_FlameMeter
    - UI_Timer
    - UI_WindWarning
    - UI_ResultPanel

Все игровые объекты и вложенные prefab instances заранее размещаются через Editor. При загрузке Main GameManager автоматически запускает матч. Retry сбрасывает существующие объекты и начинает новый матч. В Main нет главного меню.

## Префабы и обязанности

### Player.prefab

Содержит SpriteRenderer, Rigidbody2D, Collider2D, PlayerController, заранее созданный визуал защиты и Light2D.

Настраиваемые поля:

- обычная скорость;
- множитель скорости при защите;
- ссылка на визуал защиты;
- ссылка на Light2D;
- максимальный запас огня;
- обычная скорость расхода огня;
- коэффициент замедления;
- ссылка на систему запаса огня или её события.

Визуал защиты — заранее существующий дочерний объект, который включается/выключается. Не создавать защитный спрайт в коде.

### PickupRoot.prefab

Содержит SpriteRenderer, Collider2D с Is Trigger и компонент FuelPickup.

Настраиваемые поля:

- количество восстанавливаемого огня;
- задержка до повторного появления;
- ссылка на визуал;
- ссылка на Collider2D;
- при необходимости — заранее назначенный AudioSource/Animator.

Разместить четыре экземпляра на уровне вручную. При сборе выключать экземпляр и его Collider2D; после cooldown снова включать тот же экземпляр. Не создавать новый pickup.

### EnvironmentRoot.prefab и укрытия

Укрытия расставляются вручную. На каждом укрытии настроить Collider2D с Is Trigger и CoverZone. Существующий CoverZone сообщает состояние через UnityEvent<bool>; подключить его к PlayerController.SetInCover(bool) через Inspector.

В MVP укрытие полностью защищает от дополнительного урона ветра. Не добавлять сложную физику препятствий или динамическое перемещение укрытий.

### UI_HUD.prefab

Сохранить UI_HUD как корневой Canvas с CanvasScaler и GraphicRaycaster. Внутрь вручную добавить вложенные префабы UI_FlameMeter, UI_Timer и UI_WindWarning.

У всех элементов заранее выставить anchors, размеры, шрифты, цвета, состояния и ссылки. В рантайме обновлять только значения существующих компонентов.

### UI_StartPanel.prefab и UI_ResultPanel.prefab

Самостоятельные префабы, размещённые внутри Canvas заранее.

UI_StartPanel содержит заголовок, цель, подсказки управления и три вертикальные кнопки. Он размещается только в Menu через UI_Menu. UI_SettingsPanel также размещается в Menu. UI_ResultPanel находится в Main внутри UI_HUD и содержит результат и Retry.

Кнопки и обработчики заранее связать с методами GameManager через Inspector. При завершении игры переключать уже существующий ResultPanel, не создавать новый.

## Скриптовые роли

Не создавать монолитный скрипт, который одновременно управляет игроком, интерфейсом, ветром и топливом.

- **GameManager** — состояния Ready / Playing / Won / Lost, автоматический запуск при загрузке Main, таймер раунда на 180 секунд, завершение и Retry.
- **PlayerController** — Move и Protect; обычная скорость и скорость при защите; состояние нахождения в укрытии.
- **FlameMeter** — запас огня от 0 до 100, пассивный расход, дополнительная потеря от ветра, пополнение топливом, событие изменения значения.
- **WindController** — уже существует; управляет предупреждением и активной фазой ветра.
- **CoverZone** — уже существует; передаёт вход/выход игрока из зоны укрытия.
- **FuelPickup** — сбор и cooldown заранее размещённого экземпляра.
- **HUDView** — обновление существующих Slider / Image / Text и переключение заранее размещённых UI-панелей.

Все ссылки получать из serialized fields / Inspector. Не использовать глобальный поиск объектов как нормальный путь работы системы.

## Основной игровой цикл

1. GameManager запускает матч, включает управление игроком и WindController.
2. Игрок видит доступную точку топлива в пределах текущего радиуса света.
3. Игрок выбирает короткий путь напрямую или безопасный маршрут через укрытие.
4. WindController включает предупреждение, затем порыв. Игрок решает: войти в укрытие или удерживать Space и двигаться медленно.
5. При касании FuelPickup запас огня восстанавливается, радиус света увеличивается.
6. Точка топлива выключается и после cooldown снова активируется как тот же объект.
7. Цикл повторяется до 180 секунд или до нулевого запаса огня.

Важное ощущение: игрок всегда должен понимать, почему теряет огонь и какой выбор поможет выжить.

## Стартовый баланс

Значения являются гипотезами и должны настраиваться в Inspector:

- максимальный запас огня: 100;
- начальный запас: 75;
- обычный расход: 0.35 в секунду;
- топливо: +25;
- интервал между порывами: 12 секунд;
- предупреждение: 1.2 секунды;
- длительность порыва: 2 секунды;
- потеря от ветра без защиты: 8 в секунду;
- потеря от ветра под защитой: 1.5 в секунду;
- скорость под защитой: 45% от обычной.

Во время нахождения в укрытии дополнительный урон от ветра равен нулю. Базовый расход огня продолжается. Если тестировщик часто проигрывает без возможности повлиять на результат, сначала ослабить ветер или уменьшить пассивный расход.

## Связь систем

1. Кнопка Играть в Menu загружает Main; GameManager автоматически переводит игру в Playing.
2. PlayerController читает Move и Protect из существующего Input System.
3. FlameMeter уменьшает запас огня и принимает пополнение от FuelPickup.
4. WindController сообщает о предупреждении и активном порыве. PlayerController учитывает Protect и InCover.
5. FlameMeter передаёт новое значение HUDView. HUDView обновляет полосу и радиус заранее назначенного Light2D.
6. При нуле огня GameManager завершает игру как Lost. При достижении 180 секунд — Won.
7. ResultPanel уже существует в сцене и только включается.
8. Retry сбрасывает значения и позиции существующих объектов, включает заранее созданные pickup instances и сразу начинает новый матч. Новые объекты не создаются.

## Input System

Использовать существующий Input System package и Input Actions asset.

- Move — Vector2, WASD / стрелки.
- Protect — Button, удержание Space.
- Start / Retry — кнопки UI.

Если действия Protect ещё нет, добавить его в Input Actions asset через Editor. Не создавать Input Actions Asset в рантайме.

## Первая тестовая версия

До визуальной полировки проверить:

- движение игрока;
- удержание защиты и замедление;
- вход в укрытие;
- четыре заранее размещённые точки топлива;
- расход и пополнение огня;
- предупреждение и порыв ветра;
- победу и поражение;
- Retry без создания новых объектов.

После этого настраивать ощущение скорости, длину порыва, расход огня и радиус света. Только затем добавлять звук, экранные эффекты и дополнительные детали.
