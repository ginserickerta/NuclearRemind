using System;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// ศูนย์กลาง event ของเกม — ใช้ C# Action เพื่อให้ Manager ต่างๆ สื่อสารกัน
    /// โดยไม่ direct reference ข้าม Manager
    /// </summary>
    public static class EventManager
    {
        /// <summary>เมื่อวางอาคารสำเร็จ ส่ง Cell ที่วาง และข้อมูลอาคาร</summary>
        public static event Action<Cell, BuildingData> OnBuildingPlaced;

        /// <summary>เมื่อผู้เล่นยกเลิกการวางอาคาร (คลิกขวา)</summary>
        public static event Action OnPlacementCancelled;

        /// <summary>เมื่อสถานะเกมเปลี่ยน (Playing/Paused/GameOver/Victory)</summary>
        public static event Action<GameManager.GameState> OnGameStateChanged;

        /// <summary>เรียกเมื่อวางอาคารสำเร็จ</summary>
        public static void RaiseBuildingPlaced(Cell cell, BuildingData data)
        {
            OnBuildingPlaced?.Invoke(cell, data);
        }

        /// <summary>เรียกเมื่อยกเลิกการวางอาคาร</summary>
        public static void RaisePlacementCancelled()
        {
            OnPlacementCancelled?.Invoke();
        }

        /// <summary>เรียกเมื่อสถานะเกมเปลี่ยน</summary>
        public static void RaiseGameStateChanged(GameManager.GameState newState)
        {
            OnGameStateChanged?.Invoke(newState);
        }

        /// <summary>
        /// ล้าง subscriber ทั้งหมดเมื่อเริ่ม play mode ใหม่
        /// ป้องกัน stale subscriber ค้างเมื่อปิด Domain Reload (Enter Play Mode Options)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents()
        {
            OnBuildingPlaced = null;
            OnPlacementCancelled = null;
            OnGameStateChanged = null;
        }
    }
}
