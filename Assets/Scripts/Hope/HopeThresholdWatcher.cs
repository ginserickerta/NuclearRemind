using System;
using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Hope threshold events (GDD §18) with hysteresis. Pure logic, no Unity scene dependency.
    ///
    ///   hope &lt; 60 → bark pool "hope_low" (BarkManager wires in Sprint 5)
    ///   hope &lt; 40 → StrikeEvent  — 10% of workers stop for 2 days
    ///   hope &lt; 25 → ExodusEvent — population −15% permanent
    ///   hope &lt;= 0 → Game Over (HopeZero)
    ///
    /// Hysteresis: each flag re-arms only after hope climbs back above threshold + 8,
    /// so hovering around a threshold cannot re-fire the event every day.
    /// </summary>
    public class HopeThresholdWatcher
    {
        private readonly GameConfigSO _cfg;
        private bool _barkFired, _strikeFired, _exodusFired, _gameOverFired;

        /// <summary>Bark pool id to enqueue ("hope_low") — Sprint 5 BarkManager subscribes.</summary>
        public event Action<string> OnBarkLow;
        /// <summary>Strike ratio (0.10) — subscriber stops that share of workers for cfg.strikeDays.</summary>
        public event Action<float> OnStrike;
        /// <summary>Exodus ratio (0.15) — subscriber removes that share of population permanently.</summary>
        public event Action<float> OnExodus;
        public event Action OnGameOver;

        public HopeThresholdWatcher(GameConfigSO cfg) => _cfg = cfg;

        /// <summary>Run after every ledger commit with the new hope value.</summary>
        public void Evaluate(float hope)
        {
            // re-arm (hysteresis) — reset flag once hope is clear of threshold + margin
            if (_barkFired && hope > _cfg.hopeBarkLow + _cfg.hysteresisMargin) _barkFired = false;
            if (_strikeFired && hope > _cfg.hopeStrike + _cfg.hysteresisMargin) _strikeFired = false;
            if (_exodusFired && hope > _cfg.hopeExodus + _cfg.hysteresisMargin) _exodusFired = false;

            if (!_barkFired && hope < _cfg.hopeBarkLow)
            {
                _barkFired = true;
                OnBarkLow?.Invoke("hope_low");
            }

            if (!_strikeFired && hope < _cfg.hopeStrike)
            {
                _strikeFired = true;
                OnStrike?.Invoke(_cfg.strikeRatio);
            }

            if (!_exodusFired && hope < _cfg.hopeExodus)
            {
                _exodusFired = true;
                OnExodus?.Invoke(_cfg.exodusRatio);
            }

            if (!_gameOverFired && hope <= _cfg.hopeMin)
            {
                _gameOverFired = true;
                OnGameOver?.Invoke();
            }
        }
    }
}
