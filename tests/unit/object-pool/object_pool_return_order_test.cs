using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dynasty.GameData;
using Dynasty.Gameplay;
using Dynasty.Tests.Helpers;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dynasty.Tests.ObjectPools
{
    /// <summary>
    /// Story OP-004 tests: ReturnToPool's binding order (flush → deregister → deactivate → push), the
    /// current-spawn AttackerId on the flush, the enemy still active at flush time, exactly-once calls, and
    /// a static scan for <c>SetActive(false)</c> / <c>Destroy(</c> outside the allow-listed files.
    /// </summary>
    public class ObjectPoolReturnOrderTest
    {
        /// <summary>Source files allowed to contain the scanned patterns (AC-5 allow-list; review new hits, never widen blindly).</summary>
        private static readonly Dictionary<string, string[]> AllowList = new Dictionary<string, string[]>
        {
            // The pool: ReturnToPool (enemy deactivation), pre-warm (instance + container), teardown (container Destroy).
            { "Gameplay/Pooling/ObjectPool.cs", new[] { "SetActive(false)", "Destroy(" } },
            // Generated Input System wrapper: Dispose() destroys its own InputActionAsset, not a pooled object.
            { "Input/PlayerControls.cs", new[] { "Destroy(" } },
        };

        private readonly List<Object> _cleanup = new List<Object>();
        private EventRecorder _recorder;
        private RecordingEnemyRegistration _registration;
        private RecordingParryFlush _flush;
        private ObjectPool _pool;

        [SetUp]
        public void SetUp()
        {
            _recorder = new EventRecorder();
            _registration = new RecordingEnemyRegistration(_recorder);
            _flush = new RecordingParryFlush(_recorder);
            var prefabs = new Dictionary<EnemyType, EnemyController>();
            for (int i = 0; i < EnemyTypes.Count; i++)
            {
                var go = new GameObject("Prefab_" + (EnemyType)i);
                go.SetActive(false);
                prefabs[(EnemyType)i] = go.AddComponent<EnemyController>();
                _cleanup.Add(go);
            }

            _pool = new ObjectPool(prefabs, TestPoolSpawnData.Create(_cleanup), TestPoolSizing.Small().With(EnemyType.Levy, 1).With(EnemyType.Pikeman, 4),
                new RecordingDesignerErrorLog(), new System.Random(TestPoolSpawnData.Seed), _registration, _flush);
            _pool.Prewarm();
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            foreach (Object o in _cleanup)
            {
                Object.DestroyImmediate(o);
            }

            _cleanup.Clear();
        }

        // ---------------------------------------------------------------- AC-1 / AC-3 / AC-4

        [Test]
        public void test_object_pool_return_flushes_then_deregisters_then_deactivates_then_pushes()
        {
            // Arrange
            EnemyController enemy = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            bool activeAtFlush = false;
            int freeAtFlush = -1;
            _flush.OnFlush = _ =>
            {
                activeAtFlush = enemy.gameObject.activeSelf;
                freeAtFlush = _pool.FreeCount(EnemyType.Levy);
            };
            _recorder.Reset();

            // Act
            _pool.ReturnToPool(enemy);

            // Assert
            Assert.That(_recorder.Log, Is.EqualTo(new[] { "FlushEventsFor", "Deregister" }));
            Assert.That(activeAtFlush, Is.True, "flush runs while the enemy is still active (not via OnDisable)");
            Assert.That(_registration.ActiveAtDeregister, Is.EqualTo(new[] { true }), "deregister precedes SetActive(false)");
            Assert.That(freeAtFlush, Is.EqualTo(0), "push happens last");
            Assert.That(enemy.gameObject.activeSelf, Is.False);
            Assert.That(_pool.FreeCount(EnemyType.Levy), Is.EqualTo(1));
            Assert.That(_flush.Flushed.Count, Is.EqualTo(1));
            Assert.That(_registration.Deregistered.Count, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- AC-2

        [Test]
        public void test_object_pool_flush_uses_current_spawn_attacker_id()
        {
            // Arrange: two lives of the same slot
            EnemyController enemy = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            int firstLife = enemy.AttackerId;
            _pool.ReturnToPool(enemy);
            EnemyController again = _pool.Spawn(EnemyType.Levy, Vector3.zero);
            int secondLife = again.AttackerId;

            // Act
            _pool.ReturnToPool(again);

            // Assert
            Assert.That(again, Is.SameAs(enemy));
            Assert.That(_flush.Flushed, Is.EqualTo(new[] { firstLife, secondLife }));
            Assert.That(secondLife, Is.Not.EqualTo(firstLife));
        }

        // ---------------------------------------------------------------- AC-5

        [Test]
        public void test_object_pool_no_set_active_false_or_destroy_outside_allow_list()
        {
            // Arrange
            string srcRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "src"));
            Assume.That(Directory.Exists(srcRoot), "source tree not found at " + srcRoot);
            // Word-boundary match: OnDestroy()/DestroyImmediate( are not calls to Object.Destroy.
            var patterns = new Dictionary<string, Regex>
            {
                { "SetActive(false)", new Regex(@"SetActive\(\s*false\s*\)") },
                { "Destroy(", new Regex(@"(?<![A-Za-z0-9_])Destroy\(") },
            };

            // Act
            var violations = new List<string>();
            foreach (string file in Directory.GetFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
            {
                string relative = file.Substring(srcRoot.Length + 1).Replace('\\', '/');
                AllowList.TryGetValue(relative, out string[] allowed);
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    foreach (var pattern in patterns)
                    {
                        if (pattern.Value.IsMatch(code) && (allowed == null || !allowed.Contains(pattern.Key)))
                        {
                            violations.Add(relative + ":" + (i + 1) + "  " + lines[i].Trim());
                        }
                    }
                }
            }

            // Assert
            Assert.That(violations, Is.Empty, "forbidden enemy deactivation/destroy outside ObjectPool:\n" + string.Join("\n", violations));
        }

        private static string StripComment(string line)
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment >= 0 ? line.Substring(0, comment) : line;
        }
    }
}
