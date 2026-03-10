using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.DoomArcade;

public sealed class DoomArcadeControl : Control
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public readonly DoomArcadeGame Game = new();
    private InputState _input;
    private Texture? _enemyTexture;

    private static readonly Color[] WallColors = new[]
    {
        Color.Black,
        Color.Gray,
        Color.DarkRed,
        Color.DarkGreen,
        Color.DarkBlue,
    };

    private static readonly Color CeilingColor = new(0.1f, 0.1f, 0.15f);
    private static readonly Color FloorColor = new(0.25f, 0.15f, 0.1f);
    private static readonly Color GunColor = new(0.8f, 0.8f, 0.2f);
    private static readonly Color MuzzleFlashColor = new(1f, 1f, 0.5f);
    private static readonly Color CrosshairColor = new(0f, 1f, 0f, 0.8f);

    public DoomArcadeControl()
    {
        IoCManager.InjectDependencies(this);

        try
        {
            _enemyTexture = _resourceCache
                .GetResource<TextureResource>("/Textures/DoomArcade/vulp_enemy.png")
                .Texture;
        }
        catch
        {
            _enemyTexture = null;
        }

        CanKeyboardFocus = true;
        KeyboardFocusOnClick = true;
        MouseFilter = MouseFilterMode.Stop;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        Game.Update(args.DeltaSeconds, _input);

        var parent = Parent;
        while (parent != null)
        {
            if (parent is DoomArcadeMenu menu)
            {
                menu.UpdateLabels();
                break;
            }
            parent = parent.Parent;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        int screenW = (int) box.Width;
        int screenH = (int) box.Height;

        if (screenW <= 0 || screenH <= 0) return;

        // Потолок
        handle.DrawRect(new UIBox2(0, 0, screenW, screenH / 2), CeilingColor);

        // Пол
        handle.DrawRect(new UIBox2(0, screenH / 2, screenW, screenH), FloorColor);

        // Raycasting
        float fov = DoomArcadeGame.Fov;
        float halfFov = fov / 2f;
        int numRays = screenW;
        float[] depthBuffer = new float[numRays];

        for (int x = 0; x < numRays; x++)
        {
            float rayAngle = Game.PlayerAngle - halfFov + fov * x / numRays;
            float cos = MathF.Cos(rayAngle);
            float sin = MathF.Sin(rayAngle);

            float dist = CastRay(Game.PlayerX, Game.PlayerY, cos, sin, out int wallType, out bool side);
            float correctedDist = dist * MathF.Cos(rayAngle - Game.PlayerAngle);
            depthBuffer[x] = correctedDist;

            if (correctedDist <= 0.01f) correctedDist = 0.01f;

            float wallHeight = screenH / correctedDist;
            float halfWall = wallHeight / 2f;
            float centerY = screenH / 2f;

            float top = centerY - halfWall;
            float bottom = centerY + halfWall;
            if (top < 0) top = 0;
            if (bottom > screenH) bottom = screenH;

            int colorIdx = wallType;
            if (colorIdx < 0 || colorIdx >= WallColors.Length) colorIdx = 1;
            Color wallColor = WallColors[colorIdx];

            if (side)
            {
                wallColor = new Color(
                    wallColor.R * 0.7f,
                    wallColor.G * 0.7f,
                    wallColor.B * 0.7f,
                    wallColor.A);
            }

            float darkening = 1f / (1f + correctedDist * 0.15f);
            wallColor = new Color(
                wallColor.R * darkening,
                wallColor.G * darkening,
                wallColor.B * darkening, 1f);

            handle.DrawRect(new UIBox2(x, top, x + 1, bottom), wallColor);
        }

        DrawEnemies(handle, screenW, screenH, depthBuffer);
        DrawGun(handle, screenW, screenH);
        DrawCrosshair(handle, screenW, screenH);

        if (Game.GameOver)
            DrawGameOver(handle, screenW, screenH);

        DrawMinimap(handle);
    }

    private float CastRay(float startX, float startY, float dirX, float dirY,
        out int wallType, out bool side)
    {
        wallType = 1;
        side = false;

        int mapX = (int) startX;
        int mapY = (int) startY;

        float deltaDistX = dirX == 0 ? float.MaxValue : MathF.Abs(1f / dirX);
        float deltaDistY = dirY == 0 ? float.MaxValue : MathF.Abs(1f / dirY);

        float sideDistX, sideDistY;
        int stepX, stepY;

        if (dirX < 0)
        {
            stepX = -1;
            sideDistX = (startX - mapX) * deltaDistX;
        }
        else
        {
            stepX = 1;
            sideDistX = (mapX + 1f - startX) * deltaDistX;
        }

        if (dirY < 0)
        {
            stepY = -1;
            sideDistY = (startY - mapY) * deltaDistY;
        }
        else
        {
            stepY = 1;
            sideDistY = (mapY + 1f - startY) * deltaDistY;
        }

        for (int i = 0; i < 64; i++)
        {
            if (sideDistX < sideDistY)
            {
                sideDistX += deltaDistX;
                mapX += stepX;
                side = false;
            }
            else
            {
                sideDistY += deltaDistY;
                mapY += stepY;
                side = true;
            }

            if (mapX < 0 || mapX >= DoomArcadeGame.MapW ||
                mapY < 0 || mapY >= DoomArcadeGame.MapH)
                return 64f;

            if (Game.Map[mapX, mapY] > 0)
            {
                wallType = Game.Map[mapX, mapY];
                return side ? sideDistY - deltaDistY : sideDistX - deltaDistX;
            }
        }

        return 64f;
    }
    
    private void DrawEnemies(DrawingHandleScreen handle, int screenW, int screenH,
        float[] depthBuffer)
    {
        var sorted = new List<(Enemy enemy, float dist)>();
        foreach (var enemy in Game.Enemies)
        {
            if (!enemy.Alive) continue;
            float dx = enemy.X - Game.PlayerX;
            float dy = enemy.Y - Game.PlayerY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            sorted.Add((enemy, dist));
        }
        sorted.Sort((a, b) => b.dist.CompareTo(a.dist));

        float fov = DoomArcadeGame.Fov;

        foreach (var (enemy, dist) in sorted)
        {
            if (dist < 0.3f) continue;

            float dx = enemy.X - Game.PlayerX;
            float dy = enemy.Y - Game.PlayerY;
            float angle = MathF.Atan2(dy, dx) - Game.PlayerAngle;

            while (angle > MathF.PI) angle -= 2f * MathF.PI;
            while (angle < -MathF.PI) angle += 2f * MathF.PI;

            if (MathF.Abs(angle) > fov / 2f + 0.1f) continue;

            float spriteScreenX = (0.5f + angle / fov) * screenW;
            float spriteHeight = screenH / dist;
            float spriteWidth = spriteHeight * 0.75f;
            float centerY = screenH / 2f;

            float left = spriteScreenX - spriteWidth / 2f;
            float right = spriteScreenX + spriteWidth / 2f;
            float top = centerY - spriteHeight / 2f;
            float bottom = centerY + spriteHeight / 2f;

            // Проверяем depth buffer в центре спрайта
            int centerColumn = (int) Math.Clamp(spriteScreenX, 0, screenW - 1);
            if (depthBuffer[centerColumn] < dist) continue;

            float darkening = 1f / (1f + dist * 0.12f);

            if (_enemyTexture != null)
            {
                // Рисуем текстуру вульпы
                var tint = new Color(darkening, darkening, darkening, 1f);
                handle.DrawTextureRect(_enemyTexture,
                    new UIBox2(left, top, right, bottom),
                    tint);
            }
            else
            {
                // Фоллбэк — красный прямоугольник с глазами (если текстура не загрузилась)int centerColumn = (int)Math.Clamp(spriteScreenX, 0, screenW - 1);
                Color bodyColor = new(
                    1f * darkening,
                    0.2f * darkening,
                    0.2f * darkening, 1f);

                int startX = (int) left;
                int endX = (int) right;

                for (int sx = startX; sx < endX; sx++)
                {
                    if (sx < 0 || sx >= screenW) continue;
                    if (depthBuffer[sx] < dist) continue;

                    float bodyTop = centerY - spriteHeight * 0.4f;
                    float bodyBottom = centerY + spriteHeight * 0.4f;

                    handle.DrawRect(new UIBox2(sx, bodyTop, sx + 1, bodyBottom), bodyColor);

                    if (sx == (int) (spriteScreenX - spriteWidth * 0.15f) ||
                        sx == (int) (spriteScreenX + spriteWidth * 0.15f))
                    {
                        float eyeY = centerY - spriteHeight * 0.25f;
                        float eyeSize = MathF.Max(2f, spriteWidth * 0.1f);
                        handle.DrawRect(new UIBox2(sx, eyeY, sx + eyeSize, eyeY + eyeSize), Color.White);
                    }
                }
            }
        }
    }

    private void DrawGun(DrawingHandleScreen handle, int screenW, int screenH)
    {
        float gunW = screenW * 0.15f;
        float gunH = screenH * 0.25f;
        float gunX = screenW / 2f - gunW / 2f;
        float gunY = screenH - gunH;

        handle.DrawRect(new UIBox2(
            gunX + gunW * 0.35f,
            gunY - gunH * 0.3f,
            gunX + gunW * 0.65f,
            gunY + gunH * 0.15f), GunColor);

        handle.DrawRect(new UIBox2(
            gunX,
            gunY + gunH * 0.15f,
            gunX + gunW,
            gunY + gunH), GunColor);

        if (Game.Shooting)
        {
            float flashSize = gunW * 0.8f;
            handle.DrawRect(new UIBox2(
                screenW / 2f - flashSize / 2f,
                gunY - gunH * 0.6f,
                screenW / 2f + flashSize / 2f,
                gunY - gunH * 0.2f), MuzzleFlashColor);
        }
    }

    private void DrawCrosshair(DrawingHandleScreen handle, int screenW, int screenH)
    {
        float cx = screenW / 2f;
        float cy = screenH / 2f;
        handle.DrawRect(new UIBox2(cx - 8, cy - 1, cx + 8, cy + 1), CrosshairColor);
        handle.DrawRect(new UIBox2(cx - 1, cy - 8, cx + 1, cy + 8), CrosshairColor);
    }

    private void DrawGameOver(DrawingHandleScreen handle, int screenW, int screenH)
    {
        handle.DrawRect(new UIBox2(0, 0, screenW, screenH),
            new Color(0.5f, 0f, 0f, 0.6f));

        float centerX = screenW / 2f;
        float centerY = screenH / 2f;
        float boxW = screenW * 0.5f;
        float boxH = screenH * 0.1f;

        handle.DrawRect(new UIBox2(
            centerX - boxW / 2f, centerY - boxH,
            centerX + boxW / 2f, centerY + boxH),
            new Color(0f, 0f, 0f, 0.8f));
    }

    private void DrawMinimap(DrawingHandleScreen handle)
    {
        float cellSize = 4f;
        float offsetX = 5f;
        float offsetY = 5f;

        for (int x = 0; x < DoomArcadeGame.MapW; x++)
        {
            for (int y = 0; y < DoomArcadeGame.MapH; y++)
            {
                if (Game.Map[x, y] > 0)
                {
                    handle.DrawRect(new UIBox2(
                        offsetX + x * cellSize, offsetY + y * cellSize,
                        offsetX + (x + 1) * cellSize, offsetY + (y + 1) * cellSize),
                        new Color(0.5f, 0.5f, 0.5f, 0.5f));
                }
            }
        }

        float px = offsetX + Game.PlayerX * cellSize;
        float py = offsetY + Game.PlayerY * cellSize;
        handle.DrawRect(new UIBox2(px - 2, py - 2, px + 2, py + 2),
            new Color(0f, 1f, 0f, 0.8f));

        foreach (var enemy in Game.Enemies)
        {
            if (!enemy.Alive) continue;
            float ex = offsetX + enemy.X * cellSize;
            float ey = offsetY + enemy.Y * cellSize;
            handle.DrawRect(new UIBox2(ex - 1.5f, ey - 1.5f, ex + 1.5f, ey + 1.5f),
                new Color(1f, 0f, 0f, 0.8f));
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (Game.GameOver)
        {
            Game.Restart();
            args.Handle();
            return;
        }

        HandleKey(args.Function, true);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        HandleKey(args.Function, false);
        args.Handle();
    }
    

    private void HandleKey(BoundKeyFunction func, bool down)
    {
        if (func == EngineKeyFunctions.MoveUp)
            _input.Forward = down;
        else if (func == EngineKeyFunctions.MoveDown)
            _input.Backward = down;
        else if (func == EngineKeyFunctions.MoveLeft)
            _input.StrafeLeft = down;
        else if (func == EngineKeyFunctions.MoveRight)
            _input.StrafeRight = down;
        else if (func == EngineKeyFunctions.TextCursorLeft)
            _input.TurnLeft = down;
        else if (func == EngineKeyFunctions.TextCursorRight)
            _input.TurnRight = down;
        else if (func == EngineKeyFunctions.TextCursorUp)
            _input.Shoot = down;
    }
}
