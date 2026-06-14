using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ข้อมูลทั้งหมดที่ใช้ save/load เกม (JSON ผ่าน SaveManager - Phase 2 Day 6)
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public ResourceData resources;
        public PopulationData population;
        public TowerData tower;
        public List<Vector2Int> placedBuildings;
        public List<string> buildingTypes;
        public List<string> unlockedCodexEntries;
        public float gameTime;
        public int aethonRelationship;
        public int keranRelationship;
    }
}
