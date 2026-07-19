using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>One hand-painted stone cell. Empty <see cref="tileName"/> = the cell was erased.</summary>
    [Serializable]
    public struct StonePaveOverride
    {
        public Vector2Int cell;
        public string tileName;   // asset name in Assets/Sprites/Art/Tiles/Stone (e.g. "stone_full_0")

        public StonePaveOverride(Vector2Int cell, string tileName)
        {
            this.cell = cell;
            this.tileName = tileName;
        }

        public bool IsErase => string.IsNullOrEmpty(tileName);
    }

    /// <summary>
    /// Hand-painted additions/removals on the StonePave tilemap (docs/GROUND_PLAN.md).
    ///
    /// Same pattern as OreDepositManager.tileColorOverrides: the automatic pass
    /// (StoneGroundSetup) lays down plazas/paths/patches, then re-applies this list LAST, so
    /// manual edits made with the Tile Color Painter survive re-running the setup.
    ///
    /// Lives on the "StonePave" GameObject. Visual layer only — nothing reads this at runtime,
    /// it is not in SaveData, and gameplay never queries it.
    /// </summary>
    public class StonePaveOverrides : MonoBehaviour
    {
        [Tooltip("ช่องที่ทาเอง — ทาทับหลังระบบอัตโนมัติเสมอ (tileName ว่าง = ลบหินช่องนั้น)")]
        public List<StonePaveOverride> overrides = new List<StonePaveOverride>();
    }
}
