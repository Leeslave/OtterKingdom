using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class MineMazeSetup
{
    [MenuItem("Tools/Mine Maze/Update Floors 2 and 3")]
    public static void UpdateFloors()
    {
        var root = AssetDatabase.LoadAssetAtPath<MineMazeConfig>($"{DataFolder}/MineMazeConfig.asset");
        if (root == null) throw new InvalidOperationException("Build the Mine Maze scene first.");
        ConfigureAdditionalFloors(root, root.Rework);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureAdditionalFloors(MineMazeConfig root, MineReworkConfig rework)
    {
        if (rework.Layers.Count < 3) throw new InvalidOperationException("Three mine layers are required.");
        var floors = new MineMazeConfig[2];
        for (int i = 0; i < 2; i++)
        {
            int number = i + 2;
            var floor = Asset<MineMazeConfig>($"{DataFolder}/MineMazeFloor{number}.asset");
            floors[i] = floor;
            var so = new SerializedObject(floor);
            so.FindProperty("_rework").objectReferenceValue = rework;
            so.FindProperty("_layer").objectReferenceValue = rework.Layers[number - 1];
            so.FindProperty("_floorName").stringValue = $"지하 {number}층";
            var rows = GenerateFloor(17 + i * 2, 15 + i * 2, 20261018 + number, 8 + i * 2, 4);
            MazeMap.Parse(rows); // Validate before persisting any layout.
            var map = so.FindProperty("_map");
            map.arraySize = rows.Length;
            for (int r = 0; r < rows.Length; r++) map.GetArrayElementAtIndex(r).stringValue = rows[r];
            so.FindProperty("_rockRespawnSeconds").floatValue = 60;
            so.FindProperty("_testKingdomLevel").intValue = 13;
            so.FindProperty("_gateKingdomLevel").intValue = number == 2 ? 13 : 16;
            so.FindProperty("_gatePickaxeTier").intValue = number;
            so.FindProperty("_gateHits").intValue = number == 2 ? 45 : 60;
            var loot = so.FindProperty("_chestLoot");
            loot.arraySize = 2;
            string[] ids = number == 2 ? new[] { "ore_wave_iron", "ore_moon_silver" }
                : new[] { "ore_coral_crystal", "ore_moon_silver" };
            for (int n = 0; n < 2; n++)
            {
                var ore = rework.FindOre(ids[n]);
                if (ore == null) throw new InvalidOperationException($"Missing ore: {ids[n]}");
                var entry = loot.GetArrayElementAtIndex(n);
                entry.FindPropertyRelative("_ore").objectReferenceValue = ore;
                entry.FindPropertyRelative("_min").intValue = n == 0 ? 5 : 2;
                entry.FindPropertyRelative("_max").intValue = n == 0 ? 8 : 4;
                entry.FindPropertyRelative("_chance").floatValue = 1;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(floor);
        }
        var rootSo = new SerializedObject(root);
        rootSo.FindProperty("_testKingdomLevel").intValue = 13;
        var list = rootSo.FindProperty("_additionalFloors");
        list.arraySize = 2;
        for (int i = 0; i < 2; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = floors[i];
        rootSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(root);
    }

    // Fixed seeds generate authored asset data once; layouts never reroll during play.
    private static string[] GenerateFloor(int width, int height, int seed, int rocks, int chests)
    {
        var rng = new System.Random(seed);
        var grid = new char[width, height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) grid[x, y] = '#';
        var stack = new Stack<Vector2Int>();
        var start = new Vector2Int(1, height - 2);
        stack.Push(start); grid[start.x, start.y] = '.';
        var steps = new[] { Vector2Int.up * 2, Vector2Int.down * 2, Vector2Int.left * 2, Vector2Int.right * 2 };
        while (stack.Count > 0)
        {
            var cell = stack.Peek();
            var choices = new List<Vector2Int>();
            foreach (var step in steps)
            {
                var next = cell + step;
                if (next.x > 0 && next.y > 0 && next.x < width - 1 && next.y < height - 1 && grid[next.x, next.y] == '#') choices.Add(next);
            }
            if (choices.Count == 0) { stack.Pop(); continue; }
            var chosen = choices[rng.Next(choices.Count)];
            grid[(cell.x + chosen.x) / 2, (cell.y + chosen.y) / 2] = '.';
            grid[chosen.x, chosen.y] = '.';
            stack.Push(chosen);
        }
        // A few loops make revisits less linear; never touch the outer wall.
        for (int i = 0; i < 8; i++)
        {
            int x = 1 + rng.Next(width - 2), y = 1 + rng.Next(height - 2);
            if (grid[x, y] == '#' && ((grid[x - 1, y] == '.' && grid[x + 1, y] == '.') || (grid[x, y - 1] == '.' && grid[x, y + 1] == '.')))
                grid[x, y] = '.';
        }
        grid[start.x, start.y] = 'S'; grid[width - 2, 1] = 'E';
        var cells = new List<Vector2Int>();
        for (int y = 1; y < height - 1; y++) for (int x = 1; x < width - 1; x++)
            if (grid[x, y] == '.') cells.Add(new Vector2Int(x, y));
        for (int i = cells.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var c = cells[i]; cells[i] = cells[j]; cells[j] = c; }
        for (int i = 0; i < rocks + chests; i++) grid[cells[i].x, cells[i].y] = i < rocks ? 'R' : 'T';
        var rows = new string[height];
        for (int y = 0; y < height; y++) { var row = new char[width]; for (int x = 0; x < width; x++) row[x] = grid[x, y]; rows[y] = new string(row); }
        return rows;
    }
}
