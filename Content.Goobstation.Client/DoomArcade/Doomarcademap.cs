using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Content.Goobstation.Client.DoomArcade;

/// <summary>
/// Данные карты, загруженные из YAML-файла.
/// </summary>
public sealed class DoomArcadeMapData
{
    public string Name = "Unknown";
    public int Width;
    public int Height;
    public int[,] Tiles = new int[0, 0];
    public float PlayerStartX;
    public float PlayerStartY;
    public float PlayerStartAngle; // в радианах
}

/// <summary>
/// Загружает карту из YAML-файла ресурсов.
/// Поддерживает формат map_01.yml (см. Resources/Maps/DoomArcade/).
/// </summary>
public static class DoomArcadeMapLoader
{
    /// <summary>
    /// Загружает карту из строки YAML-содержимого.
    /// Вызывается из DoomArcadeGame после чтения файла через IResourceCache.
    /// </summary>
    public static DoomArcadeMapData Parse(string yaml)
    {
        var data = new DoomArcadeMapData();

        // --- name ---
        var nameMatch = Regex.Match(yaml, @"^name:\s*""([^""]+)""", RegexOptions.Multiline);
        if (nameMatch.Success)
            data.Name = nameMatch.Groups[1].Value;

        // --- width / height ---
        var wMatch = Regex.Match(yaml, @"^width:\s*(\d+)", RegexOptions.Multiline);
        var hMatch = Regex.Match(yaml, @"^height:\s*(\d+)", RegexOptions.Multiline);
        data.Width = wMatch.Success ? int.Parse(wMatch.Groups[1].Value) : 16;
        data.Height = hMatch.Success ? int.Parse(hMatch.Groups[1].Value) : 16;

        // --- player_start ---
        var psMatch = Regex.Match(yaml,
            @"player_start:\s*\n\s*x:\s*([\d.]+)\s*\n\s*y:\s*([\d.]+)\s*\n\s*angle:\s*([\d.]+)",
            RegexOptions.Multiline);
        if (psMatch.Success)
        {
            data.PlayerStartX = float.Parse(psMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            data.PlayerStartY = float.Parse(psMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            var deg = float.Parse(psMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
            data.PlayerStartAngle = deg * MathF.PI / 180f;
        }
        else
        {
            data.PlayerStartX = 2.5f;
            data.PlayerStartY = 2.5f;
            data.PlayerStartAngle = 0f;
        }

        // --- tiles ---
        // Ищем блок tiles: и парсим строки вида "  - [1, 0, 0, ...]"
        var tilesStart = yaml.IndexOf("\ntiles:", StringComparison.Ordinal);
        if (tilesStart < 0)
            tilesStart = yaml.IndexOf("tiles:", StringComparison.Ordinal);

        data.Tiles = new int[data.Width, data.Height];

        if (tilesStart >= 0)
        {
            var rowPattern = new Regex(@"^\s*-\s*\[([^\]]+)\]", RegexOptions.Multiline);
            var rows = new List<int[]>();

            foreach (Match rowMatch in rowPattern.Matches(yaml, tilesStart))
            {
                var parts = rowMatch.Groups[1].Value.Split(',');
                var row = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    row[i] = int.TryParse(parts[i].Trim(), out var v) ? v : 0;
                rows.Add(row);
            }

            // tiles[x, y]: y = строка (row index), x = колонка
            for (int y = 0; y < Math.Min(rows.Count, data.Height); y++)
            {
                var row = rows[y];
                for (int x = 0; x < Math.Min(row.Length, data.Width); x++)
                    data.Tiles[x, y] = row[x];
            }
        }

        return data;
    }
}
