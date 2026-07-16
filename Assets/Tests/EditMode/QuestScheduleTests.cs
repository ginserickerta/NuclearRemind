using System.Collections.Generic;
using NUnit.Framework;

namespace NuclearReMind.Tests
{
    /// <summary>QuestScheduleSO.FindForDay — pure lookup used by QuestPanelController.</summary>
    public class QuestScheduleTests
    {
        private static QuestScheduleSO.DayQuest Q(int day, string title = "t")
            => new QuestScheduleSO.DayQuest { day = day, title = title, tasks = new List<string> { "task" } };

        [Test]
        public void FindForDay_ReturnsMatchingEntry()
        {
            var list = new List<QuestScheduleSO.DayQuest> { Q(2), Q(5, "five"), Q(10) };
            var found = QuestScheduleSO.FindForDay(list, 5);
            Assert.IsNotNull(found);
            Assert.AreEqual("five", found.title);
        }

        [Test]
        public void FindForDay_DayWithoutQuest_ReturnsNull()
        {
            var list = new List<QuestScheduleSO.DayQuest> { Q(2), Q(5) };
            Assert.IsNull(QuestScheduleSO.FindForDay(list, 4));
        }

        [Test]
        public void FindForDay_NullOrEmptyList_ReturnsNull()
        {
            Assert.IsNull(QuestScheduleSO.FindForDay(null, 2));
            Assert.IsNull(QuestScheduleSO.FindForDay(new List<QuestScheduleSO.DayQuest>(), 2));
        }

        [Test]
        public void FindForDay_SkipsNullItems()
        {
            var list = new List<QuestScheduleSO.DayQuest> { null, Q(3) };
            var found = QuestScheduleSO.FindForDay(list, 3);
            Assert.IsNotNull(found);
            Assert.AreEqual(3, found.day);
        }

        [Test]
        public void FindForDay_DuplicateDays_ReturnsFirst()
        {
            var list = new List<QuestScheduleSO.DayQuest> { Q(7, "first"), Q(7, "second") };
            Assert.AreEqual("first", QuestScheduleSO.FindForDay(list, 7).title);
        }
    }
}
