using System;
using UnityEngine;

namespace Dynasty.GameData
{
    /// <summary>
    /// Warlord Phase 2 ground slam — the only non-parriable attack in MVP. Read only through
    /// <see cref="EnemyDataSO.TryGetSlam"/>; present only on the Warlord Phase 2 asset.
    /// </summary>
    /// <example><code>if (data.TryGetSlam(out SlamData slam) &amp;&amp; !slam.Parriable) ShowDodgeTelegraph(slam.Radius);</code></example>
    [Serializable]
    public struct SlamData
    {
        [SerializeField] private int _damage;
        [SerializeField] private float _radius;
        [SerializeField] private int _telegraphDurationMs;
        [SerializeField] private TelegraphShape _telegraphShape;
        [SerializeField] private bool _parriable;

        internal SlamData(int damage, float radius, int telegraphDurationMs, TelegraphShape telegraphShape, bool parriable)
        {
            _damage = damage;
            _radius = radius;
            _telegraphDurationMs = telegraphDurationMs;
            _telegraphShape = telegraphShape;
            _parriable = parriable;
        }

        /// <summary>Slam damage (int).</summary>
        public int Damage => _damage;

        /// <summary>Slam radius in world units; the player must leave it within the telegraph window.</summary>
        public float Radius => _radius;

        /// <summary>Slam telegraph duration in milliseconds.</summary>
        public int TelegraphDurationMs => _telegraphDurationMs;

        /// <summary>Slam telegraph shape (filled circle).</summary>
        public TelegraphShape TelegraphShape => _telegraphShape;

        /// <summary>False in MVP: the Parry System must not open a window for the slam (enemy-definition AC-06).</summary>
        public bool Parriable => _parriable;
    }
}
