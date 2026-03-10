using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Goobstation.Client.DoomArcade;

public sealed class DoomArcadeControl : Control
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;

    public readonly DoomArcadeGame Game = new();
    private InputState _input;

    // ── Текстуры ─────────────────────────────────────────────────────────────
    private Texture? _enemyTexture;
    private Texture? _floorTexture;
    private Texture? _ceilingTexture;
    private Texture? _medkitTexture;
    private Texture? _ammoTexture;
    // wall textures by wall type (index = Map value 1..4)
    private readonly Texture?[] _wallTextures = new Texture?[5];

    // Fallback-цвета (используются если текстура не загружена)
    private static readonly Color[] WallColors =
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

    // Текстурные столбцы стен (переиспользуем буфер)
    // Для текстурированных стен храним U-координату и wallType на каждый столбец
    private struct WallSlice
    {
        public float Dist;
        public float U;       // 0..1 горизонтальная позиция на стене
        public int WallType;
        public bool Side;
    }

    public DoomArcadeControl()
    {
        IoCManager.InjectDependencies(this);
        LoadTextures();

        // Загружаем карту из YAML
        try
        {
            var path = new ResPath("/Maps/DoomArcade/map_01.yml");
            var mapYaml = _resourceManager.ContentFileReadAllText(path);
            var mapData = DoomArcadeMapLoader.Parse(mapYaml);
            Game.LoadMap(mapData);
        }
        catch
        {
            // Fallback: используется карта по умолчанию из DoomArcadeGame
        }

        CanKeyboardFocus = true;
        KeyboardFocusOnClick = true;
        MouseFilter = MouseFilterMode.Stop;
    }

    private void LoadTextures()
    {
        _enemyTexture = TryLoad("/Textures/DoomArcade/vulp_enemy.png");
        _floorTexture = TryLoad("/Textures/DoomArcade/floor.png");
        _ceilingTexture = TryLoad("/Textures/DoomArcade/ceiling.png");
        _medkitTexture = TryLoad("/Textures/DoomArcade/medkit.png");
        _ammoTexture = TryLoad("/Textures/DoomArcade/ammo.png");

        _wallTextures[1] = TryLoad("/Textures/DoomArcade/wall_1.png");
        _wallTextures[2] = TryLoad("/Textures/DoomArcade/wall_2.png");
        _wallTextures[3] = TryLoad("/Textures/DoomArcade/wall_3.png");
        _wallTextures[4] = TryLoad("/Textures/DoomArcade/wall_4.png");
    }

    private Texture? TryLoad(string path)
    {
        try { return _resourceCache.GetResource<TextureResource>(path).Texture; }
        catch { return null; }
    }

    // ── Обновление кадра ─────────────────────────────────────────────────────

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        Game.Update(args.DeltaSeconds, _input);

        var parent = Parent;
        while (parent != null)
        {
            if (parent is DoomArcadeMenu menu) { menu.UpdateLabels(); break; }
            parent = parent.Parent;
        }
    }

    // ── Отрисовка ────────────────────────────────────────────────────────────

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        int screenW = (int) box.Width;
        int screenH = (int) box.Height;
        if (screenW <= 0 || screenH <= 0) return;

        // Потолок / пол
        DrawFloorCeiling(handle, screenW, screenH);

        // Raycasting + сбор слайсов стен
        float fov = DoomArcadeGame.Fov;
        float halfFov = fov / 2f;
        var slices = new WallSlice[screenW];
        var depthBuf = new float[screenW];

        for (int x = 0; x < screenW; x++)
        {
            float rayAngle = Game.PlayerAngle - halfFov + fov * x / screenW;
            float cos = MathF.Cos(rayAngle);
            float sin = MathF.Sin(rayAngle);

            float dist = CastRay(Game.PlayerX, Game.PlayerY, cos, sin,
                out int wallType, out bool side, out float wallU);
            float corrDist = dist * MathF.Cos(rayAngle - Game.PlayerAngle);
            if (corrDist < 0.01f) corrDist = 0.01f;

            depthBuf[x] = corrDist;
            slices[x] = new WallSlice { Dist = corrDist, U = wallU, WallType = wallType, Side = side };

            DrawWallSlice(handle, x, screenW, screenH, ref slices[x]);
        }

        DrawPickups(handle, screenW, screenH, depthBuf);
        DrawEnemies(handle, screenW, screenH, depthBuf);
        DrawGun(handle, screenW, screenH);
        DrawCrosshair(handle, screenW, screenH);

        if (Game.GameOver)
            DrawGameOver(handle, screenW, screenH);

        DrawMinimap(handle);
    }

    // ── Пол и потолок (текстурированные или цветные) ─────────────────────────

    private void DrawFloorCeiling(DrawingHandleScreen handle, int screenW, int screenH)
    {
        if (_ceilingTexture != null)
        {
            // Тайлим текстуру потолка
            DrawTiled(handle, _ceilingTexture, 0, 0, screenW, screenH / 2, 0.6f);
        }
        else
        {
            handle.DrawRect(new UIBox2(0, 0, screenW, screenH / 2), CeilingColor);
        }

        if (_floorTexture != null)
        {
            DrawTiled(handle, _floorTexture, 0, screenH / 2, screenW, screenH, 0.8f);
        }
        else
        {
            handle.DrawRect(new UIBox2(0, screenH / 2, screenW, screenH), FloorColor);
        }
    }

    /// <summary>Простое тайлирование текстуры через повторяющиеся блоки.</summary>
    private void DrawTiled(DrawingHandleScreen handle, Texture tex,
        int x0, int y0, int x1, int y1, float brightness)
    {
        int tw = tex.Width;
        int th = tex.Height;
        var tint = new Color(brightness, brightness, brightness, 1f);

        for (int ty = y0; ty < y1; ty += th)
        {
            for (int tx = x0; tx < x1; tx += tw)
            {
                int rx = Math.Min(tw, x1 - tx);
                int ry = Math.Min(th, y1 - ty);
                handle.DrawTextureRectRegion(tex,
                    new UIBox2(tx, ty, tx + rx, ty + ry),
                    new UIBox2(0, 0, rx, ry),
                    tint);
            }
        }
    }

    // ── Отрисовка вертикального слайса стены ─────────────────────────────────

    private void DrawWallSlice(DrawingHandleScreen handle, int x, int screenW, int screenH,
        ref WallSlice slice)
    {
        float wallHeight = screenH / slice.Dist;
        float halfWall = wallHeight / 2f;
        float centerY = screenH / 2f;
        float top = Math.Max(0, centerY - halfWall);
        float bottom = Math.Min(screenH, centerY + halfWall);

        float darkening = 1f / (1f + slice.Dist * 0.15f);
        if (slice.Side) darkening *= 0.7f;

        int wi = slice.WallType;
        Texture? wallTex = (wi >= 1 && wi <= 4) ? _wallTextures[wi] : null;

        if (wallTex != null)
        {
            int texW = wallTex.Width;
            int texH = wallTex.Height;
            int texX = (int) (slice.U * texW) % texW;
            if (texX < 0) texX += texW;

            var tint = new Color(darkening, darkening, darkening, 1f);

            handle.DrawTextureRectRegion(wallTex,
                new UIBox2(x, top, x + 1, bottom),
                new UIBox2(texX, 0, texX + 1, texH),
                tint);
        }
        else
        {
            // Fallback — цвет
            int colorIdx = wi >= 0 && wi < WallColors.Length ? wi : 1;
            Color wc = WallColors[colorIdx];
            wc = new Color(wc.R * darkening, wc.G * darkening, wc.B * darkening, 1f);
            handle.DrawRect(new UIBox2(x, top, x + 1, bottom), wc);
        }
    }

    // ── Raycast с вычислением U-координаты ───────────────────────────────────

    private float CastRay(float startX, float startY, float dirX, float dirY,
        out int wallType, out bool side, out float wallU)
    {
        wallType = 1; side = false; wallU = 0f;

        int mapX = (int) startX;
        int mapY = (int) startY;

        float deltaDistX = dirX == 0 ? float.MaxValue : MathF.Abs(1f / dirX);
        float deltaDistY = dirY == 0 ? float.MaxValue : MathF.Abs(1f / dirY);

        float sideDistX, sideDistY;
        int stepX, stepY;

        if (dirX < 0) { stepX = -1; sideDistX = (startX - mapX) * deltaDistX; }
        else { stepX = 1; sideDistX = (mapX + 1f - startX) * deltaDistX; }
        if (dirY < 0) { stepY = -1; sideDistY = (startY - mapY) * deltaDistY; }
        else { stepY = 1; sideDistY = (mapY + 1f - startY) * deltaDistY; }

        for (int i = 0; i < 64; i++)
        {
            if (sideDistX < sideDistY) { sideDistX += deltaDistX; mapX += stepX; side = false; }
            else { sideDistY += deltaDistY; mapY += stepY; side = true; }

            if (mapX < 0 || mapX >= Game.MapW || mapY < 0 || mapY >= Game.MapH)
                return 64f;

            if (Game.Map[mapX, mapY] > 0)
            {
                wallType = Game.Map[mapX, mapY];
                float dist = side ? sideDistY - deltaDistY : sideDistX - deltaDistX;

                // U-координата на стене (0..1)
                float wallHitX;
                if (!side) wallHitX = startY + dist * dirY;
                else wallHitX = startX + dist * dirX;
                wallU = wallHitX - MathF.Floor(wallHitX);

                return dist;
            }
        }

        return 64f;
    }

    // ── Пикапы ───────────────────────────────────────────────────────────────

    private void DrawPickups(DrawingHandleScreen handle, int screenW, int screenH,
        float[] depthBuffer)
    {
        float fov = DoomArcadeGame.Fov;

        foreach (var pickup in Game.Pickups)
        {
            if (pickup.Collected) continue;

            float dx = pickup.X - Game.PlayerX;
            float dy = pickup.Y - Game.PlayerY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < 0.3f) continue;

            float angle = MathF.Atan2(dy, dx) - Game.PlayerAngle;
            while (angle > MathF.PI) angle -= 2f * MathF.PI;
            while (angle < -MathF.PI) angle += 2f * MathF.PI;
            if (MathF.Abs(angle) > fov / 2f + 0.1f) continue;

            float screenX = (0.5f + angle / fov) * screenW;
            float sprHeight = screenH / dist * 0.5f; // пикапы вдвое меньше врагов
            float sprWidth = sprHeight;
            float centerY = screenH / 2f;

            float left = screenX - sprWidth / 2f;
            float right = screenX + sprWidth / 2f;
            // Пикап лежит на полу
            float bottom = centerY + screenH / (2f * dist);
            float top = bottom - sprHeight;

            int centerCol = (int) Math.Clamp(screenX, 0, screenW - 1);
            if (depthBuffer[centerCol] < dist) continue;

            float darkening = 1f / (1f + dist * 0.1f);
            var tint = new Color(darkening, darkening, darkening, 1f);

            Texture? tex = pickup.Type == PickupType.Medkit ? _medkitTexture : _ammoTexture;

            if (tex != null)
            {
                handle.DrawTextureRect(tex, new UIBox2(left, top, right, bottom), tint);
            }
            else
            {
                // Fallback
                Color fc = pickup.Type == PickupType.Medkit
                    ? new Color(0.2f * darkening, 0.9f * darkening, 0.2f * darkening, 1f)
                    : new Color(0.9f * darkening, 0.9f * darkening, 0.1f * darkening, 1f);
                handle.DrawRect(new UIBox2(left, top, right, bottom), fc);
            }
        }
    }

    // ── Враги ────────────────────────────────────────────────────────────────

    private void DrawEnemies(DrawingHandleScreen handle, int screenW, int screenH,
        float[] depthBuffer)
    {
        var sorted = new List<(Enemy enemy, float dist)>();
        foreach (var enemy in Game.Enemies)
        {
            if (!enemy.Alive) continue;
            float dx = enemy.X - Game.PlayerX;
            float dy = enemy.Y - Game.PlayerY;
            sorted.Add((enemy, MathF.Sqrt(dx * dx + dy * dy)));
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

            int centerColumn = (int) Math.Clamp(spriteScreenX, 0, screenW - 1);
            if (depthBuffer[centerColumn] < dist) continue;

            float darkening = 1f / (1f + dist * 0.12f);

            if (_enemyTexture != null)
            {
                var tint = new Color(darkening, darkening, darkening, 1f);
                handle.DrawTextureRect(_enemyTexture, new UIBox2(left, top, right, bottom), tint);
            }
            else
            {
                Color bodyColor = new(1f * darkening, 0.2f * darkening, 0.2f * darkening, 1f);
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

    // ── HUD ──────────────────────────────────────────────────────────────────

    private void DrawGun(DrawingHandleScreen handle, int screenW, int screenH)
    {
        float gunW = screenW * 0.15f;
        float gunH = screenH * 0.25f;
        float gunX = screenW / 2f - gunW / 2f;
        float gunY = screenH - gunH;

        handle.DrawRect(new UIBox2(gunX + gunW * 0.35f, gunY - gunH * 0.3f,
            gunX + gunW * 0.65f, gunY + gunH * 0.15f), GunColor);
        handle.DrawRect(new UIBox2(gunX, gunY + gunH * 0.15f,
            gunX + gunW, gunY + gunH), GunColor);

        if (Game.Shooting)
        {
            float flashSize = gunW * 0.8f;
            handle.DrawRect(new UIBox2(
                screenW / 2f - flashSize / 2f, gunY - gunH * 0.6f,
                screenW / 2f + flashSize / 2f, gunY - gunH * 0.2f), MuzzleFlashColor);
        }
    }

    private void DrawCrosshair(DrawingHandleScreen handle, int screenW, int screenH)
    {
        float cx = screenW / 2f, cy = screenH / 2f;
        handle.DrawRect(new UIBox2(cx - 8, cy - 1, cx + 8, cy + 1), CrosshairColor);
        handle.DrawRect(new UIBox2(cx - 1, cy - 8, cx + 1, cy + 8), CrosshairColor);
    }

    private void DrawGameOver(DrawingHandleScreen handle, int screenW, int screenH)
    {
        handle.DrawRect(new UIBox2(0, 0, screenW, screenH),
            new Color(0.5f, 0f, 0f, 0.6f));
        float centerX = screenW / 2f, centerY = screenH / 2f;
        float boxW = screenW * 0.5f, boxH = screenH * 0.1f;
        handle.DrawRect(new UIBox2(
            centerX - boxW / 2f, centerY - boxH,
            centerX + boxW / 2f, centerY + boxH),
            new Color(0f, 0f, 0f, 0.8f));
    }

    // ── Миникарта ────────────────────────────────────────────────────────────

    private void DrawMinimap(DrawingHandleScreen handle)
    {
        const float cellSize = 4f;
        const float offsetX = 5f;
        const float offsetY = 5f;

        for (int x = 0; x < Game.MapW; x++)
            for (int y = 0; y < Game.MapH; y++)
            {
                if (Game.Map[x, y] > 0)
                    handle.DrawRect(new UIBox2(
                        offsetX + x * cellSize, offsetY + y * cellSize,
                        offsetX + (x + 1) * cellSize, offsetY + (y + 1) * cellSize),
                        new Color(0.5f, 0.5f, 0.5f, 0.5f));
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

        // Пикапы на миникарте
        foreach (var pickup in Game.Pickups)
        {
            if (pickup.Collected) continue;
            float px2 = offsetX + pickup.X * cellSize;
            float py2 = offsetY + pickup.Y * cellSize;
            Color pc = pickup.Type == PickupType.Medkit
                ? new Color(0f, 1f, 0.3f, 0.9f)
                : new Color(1f, 1f, 0f, 0.9f);
            handle.DrawRect(new UIBox2(px2 - 1.5f, py2 - 1.5f, px2 + 1.5f, py2 + 1.5f), pc);
        }
    }

    // ── Ввод ─────────────────────────────────────────────────────────────────

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (Game.GameOver) { Game.Restart(); args.Handle(); return; }
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
        if (func == EngineKeyFunctions.MoveUp) _input.Forward = down;
        else if (func == EngineKeyFunctions.MoveDown) _input.Backward = down;
        else if (func == EngineKeyFunctions.MoveLeft) _input.StrafeLeft = down;
        else if (func == EngineKeyFunctions.MoveRight) _input.StrafeRight = down;
        else if (func == EngineKeyFunctions.TextCursorLeft) _input.TurnLeft = down;
        else if (func == EngineKeyFunctions.TextCursorRight) _input.TurnRight = down;
        else if (func == EngineKeyFunctions.TextCursorUp) _input.Shoot = down;
    }
}
