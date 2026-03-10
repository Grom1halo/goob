using System;
using System.Collections.Generic;

namespace Content.Goobstation.Client.DoomArcade;

// ── Типы пикапов ────────────────────────────────────────────────────────────

public enum PickupType { Medkit, Ammo }

public sealed class Pickup
{
    public float X;
    public float Y;
    public PickupType Type;
    public bool Collected;

    public Pickup(float x, float y, PickupType type)
    {
        X = x; Y = y; Type = type;
    }
}

// ── Основной класс игры ─────────────────────────────────────────────────────

public sealed class DoomArcadeGame
{
    // Размеры карты берём из загруженных данных
    public int MapW { get; private set; } = 16;
    public int MapH { get; private set; } = 16;
    public int[,] Map { get; private set; } = new int[16, 16];

    // Стартовые координаты (из YAML)
    private float _startX = 2.5f;
    private float _startY = 2.5f;
    private float _startAngle = 0f;

    public float PlayerX;
    public float PlayerY;
    public float PlayerAngle;
    public int PlayerHp = 100;
    public int PlayerAmmo = 50;
    public int Score = 0;
    public bool GameOver = false;
    public bool Shooting = false;
    public float ShootTimer = 0f;

    public List<Enemy> Enemies = new();
    public List<Pickup> Pickups = new();

    // ── Константы ────────────────────────────────────────────────────────────
    public const float MoveSpeed = 3.0f;
    public const float RotSpeed = 2.5f;
    public const float Fov = MathF.PI / 3f;
    public const float ShootCooldown = 0.3f;
    public const float ShootRange = 10f;

    // Интервал спавна новых врагов (секунды)
    private const float SpawnInterval = 8f;
    private const int MaxEnemies = 8;
    private const float PickupRadius = 0.6f;
    private const float MedkitHeal = 30f;
    private const int AmmoPickupCount = 15;

    private float _spawnTimer = SpawnInterval;
    private int _wave = 1;
    public int Wave => _wave;

    private readonly Random _rng = new();

    // ── Загрузка карты ───────────────────────────────────────────────────────

    /// <summary>
    /// Загружает карту из распарсенных данных YAML.
    /// Вызывается из DoomArcadeControl после чтения файла через IResourceCache.
    /// </summary>
    public void LoadMap(DoomArcadeMapData mapData)
    {
        MapW = mapData.Width;
        MapH = mapData.Height;
        Map = mapData.Tiles;
        _startX = mapData.PlayerStartX;
        _startY = mapData.PlayerStartY;
        _startAngle = mapData.PlayerStartAngle;

        PlayerX = _startX;
        PlayerY = _startY;
        PlayerAngle = _startAngle;
    }

    public DoomArcadeGame()
    {
        // Fallback: статичная карта из кода, если файл не загружен
        Map = _defaultMap;
        PlayerX = _startX;
        PlayerY = _startY;
        PlayerAngle = _startAngle;
        SpawnInitialEnemies();
    }

    // ── Спавн ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Возвращает список всех пустых клеток карты (не занятых стеной,
    /// не занятых игроком, не занятых живым врагом).
    /// </summary>
    private List<(float x, float y)> GetFreePositions(float minDistFromPlayer = 4f)
    {
        var result = new List<(float, float)>();
        for (int cx = 1; cx < MapW - 1; cx++)
        {
            for (int cy = 1; cy < MapH - 1; cy++)
            {
                if (Map[cx, cy] != 0) continue;

                float wx = cx + 0.5f;
                float wy = cy + 0.5f;

                float dx = wx - PlayerX;
                float dy = wy - PlayerY;
                if (MathF.Sqrt(dx * dx + dy * dy) < minDistFromPlayer) continue;

                // Не спавним прямо на живого врага
                bool occupied = false;
                foreach (var e in Enemies)
                {
                    if (!e.Alive) continue;
                    float ex = wx - e.X;
                    float ey = wy - e.Y;
                    if (MathF.Sqrt(ex * ex + ey * ey) < 1f) { occupied = true; break; }
                }
                if (!occupied)
                    result.Add((wx, wy));
            }
        }
        return result;
    }

    private void SpawnInitialEnemies()
    {
        Enemies.Clear();
        Pickups.Clear();
        _wave = 1;
        _spawnTimer = SpawnInterval;

        var free = GetFreePositions(4f);
        Shuffle(free);

        int count = Math.Min(5, free.Count);
        for (int i = 0; i < count; i++)
        {
            int hp = EnemyHpForWave(_wave);
            Enemies.Add(new Enemy(free[i].x, free[i].y, hp, EnemyDamageForWave(_wave)));
        }
    }

    /// <summary>
    /// Периодически вызывается из Update() — добавляет врагов и пикапы.
    /// </summary>
    private void SpawnWave()
    {
        _wave++;
        var free = GetFreePositions(5f);
        if (free.Count == 0) return;
        Shuffle(free);

        int enemyCount = Math.Min(3 + _wave / 2, MaxEnemies - AliveEnemyCount());
        enemyCount = Math.Max(0, enemyCount);

        int used = 0;
        for (int i = 0; i < enemyCount && used < free.Count; i++, used++)
        {
            int hp = EnemyHpForWave(_wave);
            Enemies.Add(new Enemy(free[used].x, free[used].y, hp, EnemyDamageForWave(_wave)));
        }

        // Вместе с волной спавним 1–2 пикапа
        int pickupCount = 1 + _rng.Next(0, 2);
        for (int i = 0; i < pickupCount && used < free.Count; i++, used++)
        {
            var type = _rng.Next(0, 2) == 0 ? PickupType.Medkit : PickupType.Ammo;
            Pickups.Add(new Pickup(free[used].x, free[used].y, type));
        }
    }

    private int AliveEnemyCount()
    {
        int c = 0;
        foreach (var e in Enemies) if (e.Alive) c++;
        return c;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ── Обновление ───────────────────────────────────────────────────────────

    public void Update(float dt, InputState input)
    {
        if (GameOver) return;

        // Поворот
        if (input.TurnLeft) PlayerAngle -= RotSpeed * dt;
        if (input.TurnRight) PlayerAngle += RotSpeed * dt;

        // Движение
        float dx = MathF.Cos(PlayerAngle);
        float dy = MathF.Sin(PlayerAngle);
        float moveX = 0, moveY = 0;

        if (input.Forward) { moveX += dx * MoveSpeed * dt; moveY += dy * MoveSpeed * dt; }
        if (input.Backward) { moveX -= dx * MoveSpeed * dt; moveY -= dy * MoveSpeed * dt; }
        if (input.StrafeLeft) { moveX += dy * MoveSpeed * dt; moveY -= dx * MoveSpeed * dt; }
        if (input.StrafeRight) { moveX -= dy * MoveSpeed * dt; moveY += dx * MoveSpeed * dt; }

        float newX = PlayerX + moveX;
        float newY = PlayerY + moveY;
        const float margin = 0.2f;

        int bx = (int) (newX + margin * MathF.Sign(moveX != 0 ? moveX : 1));
        int by = (int) (newY + margin * MathF.Sign(moveY != 0 ? moveY : 1));
        if (bx >= 0 && bx < MapW && (int) PlayerY >= 0 && (int) PlayerY < MapH &&
            Map[bx, (int) PlayerY] == 0)
            PlayerX = newX;
        if ((int) PlayerX >= 0 && (int) PlayerX < MapW && by >= 0 && by < MapH &&
            Map[(int) PlayerX, by] == 0)
            PlayerY = newY;

        // Стрельба
        if (ShootTimer > 0) ShootTimer -= dt;

        if (input.Shoot && ShootTimer <= 0 && PlayerAmmo > 0)
        {
            Shooting = true;
            ShootTimer = ShootCooldown;
            PlayerAmmo--;
            TryHitEnemy();
        }
        else if (ShootTimer <= ShootCooldown * 0.5f)
        {
            Shooting = false;
        }

        // ИИ врагов
        foreach (var enemy in Enemies)
        {
            if (!enemy.Alive) continue;

            float ex = PlayerX - enemy.X;
            float ey = PlayerY - enemy.Y;
            float dist = MathF.Sqrt(ex * ex + ey * ey);

            if (dist < 1.5f)
            {
                enemy.AttackTimer -= dt;
                if (enemy.AttackTimer <= 0)
                {
                    PlayerHp -= enemy.Damage;
                    enemy.AttackTimer = 1.0f;
                    if (PlayerHp <= 0)
                    {
                        PlayerHp = 0;
                        GameOver = true;
                    }
                }
            }
            else if (dist < 8f)
            {
                float speed = 1.5f * dt;
                float nx = ex / dist;
                float ny = ey / dist;
                float enx = enemy.X + nx * speed;
                float eny = enemy.Y + ny * speed;

                int mex = (int) enx;
                int mey = (int) eny;
                if (mex >= 0 && mex < MapW && (int) enemy.Y >= 0 && (int) enemy.Y < MapH &&
                    Map[mex, (int) enemy.Y] == 0)
                    enemy.X = enx;
                if ((int) enemy.X >= 0 && (int) enemy.X < MapW && mey >= 0 && mey < MapH &&
                    Map[(int) enemy.X, mey] == 0)
                    enemy.Y = eny;
            }
        }

        // Подбор пикапов
        for (int i = Pickups.Count - 1; i >= 0; i--)
        {
            var p = Pickups[i];
            if (p.Collected) continue;
            float pdx = p.X - PlayerX;
            float pdy = p.Y - PlayerY;
            if (MathF.Sqrt(pdx * pdx + pdy * pdy) < PickupRadius)
            {
                p.Collected = true;
                if (p.Type == PickupType.Medkit)
                    PlayerHp = Math.Min(100, PlayerHp + (int) MedkitHeal);
                else
                    PlayerAmmo += AmmoPickupCount;
            }
        }

        // Тimer спавна новой волны
        _spawnTimer -= dt;
        if (_spawnTimer <= 0)
        {
            _spawnTimer = SpawnInterval;
            SpawnWave();
        }

        // Если все враги мертвы — мгновенная новая волна + бонус
        if (AliveEnemyCount() == 0)
        {
            Score += 500;
            _spawnTimer = 0; // следующий Update() вызовет SpawnWave
        }
    }

    // ── Стрельба ─────────────────────────────────────────────────────────────

    private void TryHitEnemy()
    {
        float dx = MathF.Cos(PlayerAngle);
        float dy = MathF.Sin(PlayerAngle);

        Enemy? closest = null;
        float closestDist = ShootRange;

        foreach (var enemy in Enemies)
        {
            if (!enemy.Alive) continue;
            float ex = enemy.X - PlayerX;
            float ey = enemy.Y - PlayerY;
            float dist = MathF.Sqrt(ex * ex + ey * ey);
            if (dist > ShootRange) continue;

            float dot = (ex * dx + ey * dy) / dist;
            if (dot < 0.95f) continue;

            if (!IsWallBetween(PlayerX, PlayerY, enemy.X, enemy.Y) && dist < closestDist)
            {
                closest = enemy;
                closestDist = dist;
            }
        }

        if (closest != null)
        {
            closest.Hp -= 15;
            if (closest.Hp <= 0)
            {
                closest.Alive = false;
                Score += 100;
            }
        }
    }

    private bool IsWallBetween(float x1, float y1, float x2, float y2)
    {
        float ddx = x2 - x1;
        float ddy = y2 - y1;
        float dist = MathF.Sqrt(ddx * ddx + ddy * ddy);
        float steps = dist * 4;
        for (int i = 0; i < (int) steps; i++)
        {
            float t = i / steps;
            int cx = (int) (x1 + ddx * t);
            int cy = (int) (y1 + ddy * t);
            if (cx >= 0 && cx < MapW && cy >= 0 && cy < MapH && Map[cx, cy] > 0)
                return true;
        }
        return false;
    }

    // ── Формулы масштабирования по волне ─────────────────────────────────────

    /// <summary>
    /// HP врага на заданной волне. Волна 1 → 25–40, каждая следующая +15 базы +10 разброса.
    /// </summary>
    private int EnemyHpForWave(int wave)
    {
        int baseHp = 25 + (wave - 1) * 15;          // +15 за волну
        int spread = 10 + (wave - 1) * 5;           // разброс тоже растёт
        return baseHp + _rng.Next(0, spread);
    }

    /// <summary>
    /// Урон врага в секунду на заданной волне. Волна 1 → 5, каждые 2 волны +2, макс 25.
    /// </summary>
    private int EnemyDamageForWave(int wave)
    {
        return Math.Min(5 + (wave - 1) / 2 * 2, 25);
    }

    // ── Рестарт ──────────────────────────────────────────────────────────────

    public void Restart()
    {
        PlayerX = _startX;
        PlayerY = _startY;
        PlayerAngle = _startAngle;
        PlayerHp = 100;
        PlayerAmmo = 50;
        Score = 0;
        GameOver = false;
        Shooting = false;
        ShootTimer = 0;
        Enemies.Clear();
        Pickups.Clear();
        SpawnInitialEnemies();
    }

    // ── Fallback карта (если YAML не загрузился) ─────────────────────────────

    private static readonly int[,] _defaultMap = new int[16, 16]
    {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,2,2,0,0,0,0,0,3,3,0,0,0,1},
        {1,0,0,2,0,0,0,0,0,0,0,3,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,4,4,4,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,4,0,4,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,4,0,4,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,3,0,0,0,0,0,0,0,2,0,0,0,1},
        {1,0,0,3,3,0,0,0,0,0,2,2,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
    };
}

// ── Вспомогательные типы ─────────────────────────────────────────────────────

public sealed class Enemy
{
    public float X;
    public float Y;
    public int Hp;
    public int Damage;
    public bool Alive = true;
    public float AttackTimer = 1.0f;

    public Enemy(float x, float y, int hp, int damage = 5)
    {
        X = x; Y = y; Hp = hp; Damage = damage;
    }
}

public struct InputState
{
    public bool Forward;
    public bool Backward;
    public bool StrafeLeft;
    public bool StrafeRight;
    public bool TurnLeft;
    public bool TurnRight;
    public bool Shoot;
}
