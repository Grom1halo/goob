# 🔥 AdvancedHitscan — лазер с поджигом и затуханием урона по дистанции (Goob-Station)

Набор хитсканных орудий с продвинутой механикой: затухание урона по дистанции и атмосфере (туман/вакуум), а также пробивающий стены рентгеновский луч.

---

## 📁 Структура файлов

```
Goob-Station/
├── Content.Goobstation.Shared/
│   └── AdvancedHitscan/
│       ├── HitscanEvents.cs                  # BeforeHitscanFiredEvent, HitscanDamageModifyEvent, HitscanPenetratingFireEvent
│       ├── DistanceDamageFalloffComponent.cs # Компонент затухания урона по дистанции/атмосфере
│       └── PenetratingHitscanComponent.cs    # Компонент пробивающего луча (X-Ray)
│
├── Content.Goobstation.Server/
│   └── AdvancedHitscan/
│       ├── DistanceDamageFalloffSystem.cs    # АКТИВНАЯ система затухания (подписана на HitscanDamageModifyEvent)
│       ├── PenetratingHitscanSystem.cs       # Система пробивающего луча (X-Ray rifle)
│       ├── HitscanEvents.cs                  # Доп. дубль HitscanBeforeHitEvent (namespace Shared.AdvancedHitscan)
│       ├── HitscanBeforeHitEvent.cs          # Доп. дубль HitscanBeforeHitEvent (namespace Server.AdvancedHitscan)
│       └── DistanceFalloffSystem.cs          # НЕИСПОЛЬЗУЕМАЯ система (см. "Известные проблемы")
│
└── Resources/
    └── Prototypes/_Goobstation/Entities/Objects/Weapons/Guns/
        └── advanced_hitscan.yml              # Прототипы: WeaponHeavyLaserGoob, WeaponXRayRifleGoob
```

Плюс патч `gunsystem_advanced_hitscan.patch` — изменения в `Content.Server/Weapons/Ranged/Systems/GunSystem.cs`, без которых вся фича не работает.

---

## 🔫 Оружие

### Heavy Laser Cannon (`WeaponHeavyLaserGoob`)

> "Devastating energy weapon. Extremely effective at close range but loses power over distance. Useless in fog. Deadly in vacuum."

- Урон: `Heat 35 + Shock 10`, `staminaDamage 15`
- `fireStacks: 2` — поджигает цель
- `DistanceDamageFalloff`:
  - `optimalRange: 5` — полный урон до 5 тайлов
  - `maxRange: 18` — дальше 18 тайлов урон = минимум
  - `minDamageMultiplier: 0.15` — минимум 15% урона
  - `atmosDamageReduction: 0.4` — доп. множитель за каждый тайл тумана/пара
  - `vacuumIgnoresFalloff: true` — в вакууме затухания по дистанции нет

### X-Ray Penetrating Rifle (`WeaponXRayRifleGoob`)

> "Experimental weapon firing concentrated radiation through walls. Blocked only by plasteel and radiation shielding. Does not damage structures."

- Урон: `Radiation 20 + Cellular 15` (через `PenetratingHitscanComponent.AllowedDamageTypes`)
- `PenetratingHitscan`:
  - `penetrationFalloff: 0.25` — −25% урона за каждую пробитую стену
  - `blockedByTags: [Plasteel, RadiationShield]` — что останавливает луч
  - `maxPenetrations: 4`

---

## ⚙️ Механика затухания (`DistanceDamageFalloffSystem`)

Подписана на `HitscanDamageModifyEvent`, который кастует `GunSystem.Shoot()` после попадания, до применения урона:

1. Считает дистанцию выстрел → цель.
2. Если `VacuumIgnoresFalloff` и весь путь — вакуум, дистанционное затухание пропускается.
3. Иначе линейно интерполирует множитель урона между 1.0 (на `OptimalRange`) и `MinDamageMultiplier` (на `MaxRange` и далее).
4. Дополнительно сканирует тайлы по пути луча на `WaterVapor` (туман/пар) и умножает урон на `AtmosDamageReduction ^ (число туманных тайлов)`.
5. Итоговый множитель применяется к `DamageSpecifier` через `args.Damage *= finalMultiplier`.

`PenetratingHitscanSystem` работает иначе: подписан на `BeforeHitscanFiredEvent` (вызывается ДО стандартной хитскан-логики), сам кастует луч через `IntersectRay`, проходит стены (кроме тех, что в `BlockedByTags`) с накопительным затуханием, наносит отфильтрованный урон каждой живой цели на пути.

---

## 🚀 Установка

1. Скопировать `Content.Goobstation.Shared/AdvancedHitscan/` и `Content.Goobstation.Server/AdvancedHitscan/` в проект.
2. Скопировать `Resources/Prototypes/_Goobstation/Entities/Objects/Weapons/Guns/advanced_hitscan.yml`.
3. Применить `gunsystem_advanced_hitscan.patch` к `Content.Server/Weapons/Ranged/Systems/GunSystem.cs` — добавляет:
   - `using Content.Goobstation.Shared.AdvancedHitscan;`
   - вызов `BeforeHitscanFiredEvent` перед стандартной хитскан-логикой (для X-Ray penetration)
   - вызов `HitscanDamageModifyEvent` после хита, перед применением урона (для затухания по дистанции)
4. Собрать проект.
5. `spawn WeaponHeavyLaserGoob` / `spawn WeaponXRayRifleGoob`.

---

## ⚠️ Известные проблемы

- **`DistanceFalloffSystem.cs`** (Server/AdvancedHitscan) — мёртвый код. Подписан на `HitscanBeforeHitEvent`, но это событие никогда не вызывается через `RaiseLocalEvent` ни в одном месте кодовой базы. Реальное затухание урона делает только `DistanceDamageFalloffSystem` (через `HitscanDamageModifyEvent`, который реально кастуется в `GunSystem.cs`).
- **Дублированный тип `HitscanBeforeHitEvent`** объявлен в двух местах с разными namespace:
  - `Content.Goobstation.Server/AdvancedHitscan/HitscanEvents.cs` → `Content.Goobstation.Shared.AdvancedHitscan.HitscanBeforeHitEvent`
  - `Content.Goobstation.Server/AdvancedHitscan/HitscanBeforeHitEvent.cs` → `Content.Goobstation.Server.AdvancedHitscan.HitscanBeforeHitEvent`

  Оба — наследие двух разных попыток реализовать одну и ту же фичу. Код компилируется (разные namespace), но один из вариантов лишний. При доработке стоит удалить `DistanceFalloffSystem.cs` + один из дублей `HitscanBeforeHitEvent`, оставив только рабочую связку `DistanceDamageFalloffSystem` + `HitscanDamageModifyEvent`.

---

## 📝 Зависимости

- **Robust Toolbox** — ECS (`EntitySystem`, `RaiseLocalEvent`, `[ByRefEvent]`), `IMapManager`, `TransformSystem`
- `Content.Server.Atmos` — `AtmosphereSystem`, `GridAtmosphereComponent`, `Gas.WaterVapor`
- `Content.Shared.Damage` — `DamageSpecifier`
- `Content.Shared.Tag` — `TagSystem` (для `PenetratingHitscanSystem`)
- Пространства имён: `Content.Goobstation.Shared.AdvancedHitscan`, `Content.Goobstation.Server.AdvancedHitscan`
