using System;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// What a card option does when chosen (GDD §25 / CARDS.md). Authored per option; default 0 =
    /// "no effect" so an unfilled field never changes behaviour.
    ///
    /// Split by whether the target system exists yet:
    ///   • LIVE — Hope (HopeLedger) · Resources (ResourceManager) · Worker stats/reassign
    ///     (WorkerManager). CardManager applies these on resolve.
    ///   • DEFERRED — reactor HEAT / CORE, food-spoil rate, Zone B. Those systems are Sprint 5/6;
    ///     the numbers ride along on OnCrisisCardResolved for them to consume, but CardManager does
    ///     not fake them. Same "ข้ามไว้" approach as the legacy CrisisChoiceEffects.
    ///
    /// Rule #5 (CLAUDE.md): every option has a price — an all-zero effect is a bug in the asset,
    /// not a "free" choice.
    /// </summary>
    [Serializable]
    public class CardEffect
    {
        [Header("Hope (HopeLedger — HopeCategory.Card)")]
        public float hopeDelta;         // immediate, one-off
        public float hopePerDay;        // recurring drip (e.g. Decree B: −3/day)
        public int hopePerDayDays;      // for this many days

        [Header("Resources (immediate delta via RaiseResourceDelta)")]
        public float power;             // Energy
        public float water;
        public float food;
        public float iron;
        public float labMat;
        [Tooltip("0 = none; else food becomes food × this (e.g. 0.7 = ration cut)")]
        public float foodMult;

        [Header("Workers (immediate — WorkerManager)")]
        public float fatigueAll;        // + to every alive worker (e.g. −60 forced rest)
        public float hungerAll;         // + to every alive worker (e.g. −20 half-ration)
        public int radiationTargets;    // apply radiation to this many workers (lowest-rad first)
        public float radiationAmount;   // + radiation per targeted worker (e.g. +12)
        public int farmersReturned;     // pull this many (idle first) back to Farm

        [Header("Deferred systems (stored data; Sprint 5/6 reads via OnCrisisCardResolved)")]
        public float heatDelta;         // reactor HEAT −X
        public int coreStallDays;       // CORE% stops for N days
        public float spoilMult;         // FoodStorage spoil × (e.g. 0.3 with Co-60)
        public int stopZoneBDays;       // Zone B paused N days
    }
}
