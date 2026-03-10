using System;
using System.Collections.Generic;

namespace Content.Goobstation.Client.DoomArcade;

public sealed class DoomArcadeGame
{
    public const int MapW = 16;
    public const int MapH = 16;

    public readonly int[,] Map = new int[MapW, MapH]
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

    public float PlayerX = 2.5f;
    public float PlayerY = 2.5f;
    public float PlayerAngle = 0f;
    public int PlayerHp = 100;
    public int PlayerAmmo = 50;
    public int Score = 0;
    public bool GameOver = false;
    public bool Shooting = false;
    public float ShootTimer = 0f;

    public List<Enemy> Enemies = new();

    public const float MoveSpeed = 3.0f;
    public const float RotSpeed = 2.5f;
    public const float Fov = MathF.PI / 3f;
    public const float ShootCooldown = 0.3f;
    public const float ShootRange = 10f;

    public DoomArcadeGame()
    {
        SpawnEnemies();
    }

    private void SpawnEnemies()
    {
        Enemies.Clear();
        Enemies.Add(new Enemy(7.5f, 7.5f, 30));
        Enemies.Add(new Enemy(10.5f, 3.5f, 20));
        Enemies.Add(new Enemy(4.5f, 10.5f, 40));
        Enemies.Add(new Enemy(12.5f, 12.5f, 25));
        Enemies.Add(new Enemy(6.5f, 13.5f, 35));
    }

    public void Update(float dt, InputState input)
    {
        if (GameOver) return;

        if (input.TurnLeft) PlayerAngle -= RotSpeed * dt;
        if (input.TurnRight) PlayerAngle += RotSpeed * dt;

        float dx = MathF.Cos(PlayerAngle);
        float dy = MathF.Sin(PlayerAngle);

        float moveX = 0;
        float moveY = 0;

        if (input.Forward) { moveX += dx * MoveSpeed * dt; moveY += dy * MoveSpeed * dt; }
        if (input.Backward) { moveX -= dx * MoveSpeed * dt; moveY -= dy * MoveSpeed * dt; }
        if (input.StrafeLeft) { moveX += dy * MoveSpeed * dt; moveY -= dx * MoveSpeed * dt; }
        if (input.StrafeRight) { moveX -= dy * MoveSpeed * dt; moveY += dx * MoveSpeed * dt; }

        float newX = PlayerX + moveX;
        float newY = PlayerY + moveY;
        float margin = 0.2f;

        if (Map[(int) (newX + margin * MathF.Sign(moveX)), (int) PlayerY] == 0)
            PlayerX = newX;
        if (Map[(int) PlayerX, (int) (newY + margin * MathF.Sign(moveY))] == 0)
            PlayerY = newY;

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
                    PlayerHp -= 5;
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

                if (Map[(int) enx, (int) enemy.Y] == 0) enemy.X = enx;
                if (Map[(int) enemy.X, (int) eny] == 0) enemy.Y = eny;
            }
        }

        if (Enemies.TrueForAll(e => !e.Alive))
        {
            Score += 500;
            SpawnEnemies();
        }
    }

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

    public void Restart()
    {
        PlayerX = 2.5f;
        PlayerY = 2.5f;
        PlayerAngle = 0f;
        PlayerHp = 100;
        PlayerAmmo = 50;
        Score = 0;
        GameOver = false;
        Shooting = false;
        ShootTimer = 0;
        SpawnEnemies();
    }
}

public sealed class Enemy
{
    public float X;
    public float Y;
    public int Hp;
    public bool Alive = true;
    public float AttackTimer = 1.0f;

    public Enemy(float x, float y, int hp)
    {
        X = x;
        Y = y;
        Hp = hp;
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
