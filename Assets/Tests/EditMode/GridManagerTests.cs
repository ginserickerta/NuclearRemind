using NUnit.Framework;
using UnityEngine;
using NuclearReMind;

namespace NuclearReMind.Tests
{
    /// <summary>
    /// EditMode tests สำหรับ GridManager — ตรวจคณิตศาสตร์ isometric และการเข้าถึง cell
    /// </summary>
    public class GridManagerTests
    {
        private GameObject go;
        private GridManager grid;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("GridManagerTest");
            grid = go.AddComponent<GridManager>();
            grid.columns = 20;
            grid.rows = 12;
            grid.tileWidth = 1f;
            grid.tileHeight = 0.5f;
            grid.originOffset = Vector3.zero;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void IsoToWorld_AtZeroZero_ReturnsOriginOffset()
        {
            grid.originOffset = new Vector3(2f, 3f, 0f);
            Vector3 world = grid.IsoToWorld(0, 0);
            Assert.AreEqual(grid.originOffset, world);
        }

        [Test]
        public void IsoToWorld_KnownCell_ReturnsExpectedPosition()
        {
            // (3,2): x = (3-2)*0.5 = 0.5 ; y = (3+2)*0.25 = 1.25
            Vector3 world = grid.IsoToWorld(3, 2);
            Assert.AreEqual(0.5f, world.x, 1e-4f);
            Assert.AreEqual(1.25f, world.y, 1e-4f);
        }

        [Test]
        public void WorldToIso_IsInverseOfIsoToWorld()
        {
            // round-trip ทุก cell ในขอบเขต ต้องได้พิกัดเดิมกลับมา
            for (int col = 0; col < grid.columns; col++)
            {
                for (int row = 0; row < grid.rows; row++)
                {
                    Vector3 world = grid.IsoToWorld(col, row);
                    Vector2Int back = grid.WorldToIso(world);
                    Assert.AreEqual(new Vector2Int(col, row), back,
                        $"round-trip ล้มเหลวที่ ({col}, {row})");
                }
            }
        }

        [Test]
        public void WorldToIso_RoundTrip_WithOriginOffset()
        {
            grid.originOffset = new Vector3(-4.5f, 2.25f, 0f);
            Vector3 world = grid.IsoToWorld(7, 5);
            Vector2Int back = grid.WorldToIso(world);
            Assert.AreEqual(new Vector2Int(7, 5), back);
        }

        [Test]
        public void IsInBounds_InsideGrid_ReturnsTrue()
        {
            Assert.IsTrue(grid.IsInBounds(0, 0));
            Assert.IsTrue(grid.IsInBounds(19, 11));
            Assert.IsTrue(grid.IsInBounds(5, 4));
        }

        [Test]
        public void IsInBounds_OutsideGrid_ReturnsFalse()
        {
            Assert.IsFalse(grid.IsInBounds(-1, 0));
            Assert.IsFalse(grid.IsInBounds(0, -1));
            Assert.IsFalse(grid.IsInBounds(20, 0));
            Assert.IsFalse(grid.IsInBounds(0, 12));
        }

        [Test]
        public void GetCell_OutOfBounds_ReturnsNull()
        {
            Assert.IsNull(grid.GetCell(-1, -1));
            Assert.IsNull(grid.GetCell(20, 12));
        }

        [Test]
        public void GetCell_InBounds_ReturnsCellWithMatchingCoords()
        {
            grid.InitializeGrid();
            Cell cell = grid.GetCell(5, 4);
            Assert.IsNotNull(cell);
            Assert.AreEqual(5, cell.col);
            Assert.AreEqual(4, cell.row);
            Assert.IsFalse(cell.isOccupied);
            Assert.AreEqual(BuildingType.None, cell.buildingType);
        }
    }
}
