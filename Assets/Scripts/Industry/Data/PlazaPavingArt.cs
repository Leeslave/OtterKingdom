using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlazaPavingArt : ScriptableObject
{
    public const string CenterProjectId = "town_paving_plaza_center";
    public const string RoadsProjectId = "town_paving_roads";

    [SerializeField] private List<PavingPiece> _center = new List<PavingPiece>();
    [SerializeField] private List<PavingPiece> _roads = new List<PavingPiece>();

    public IReadOnlyList<PavingPiece> Center => _center;

    public IReadOnlyList<PavingPiece> GetPieces(string projectId) => projectId switch
    {
        CenterProjectId => _center,
        RoadsProjectId => _roads,
        _ => Array.Empty<PavingPiece>(),
    };
}
