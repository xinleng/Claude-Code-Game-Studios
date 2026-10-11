using System.Collections;
using Dynasty.Bootstrap;
using Dynasty.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dynasty.Tests.Bootstraps
{
    /// <summary>
    /// Story BCF-005 AC-9 PlayMode smoke (ADR-0010 PlayMode scope): MainArena holds exactly one
    /// Bootstrapper; loading the scene completes boot; one frame runs with no exceptions or errors.
    /// Scene-wiring only — no gameplay assertions.
    /// </summary>
    public class BootstrapSceneSmokeTest
    {
        private const string SceneName = "MainArena";

        [UnityTest]
        public IEnumerator test_bootstrap_main_arena_boots_and_runs_one_frame_cleanly()
        {
            // Act
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            // Assert
            Bootstrapper[] boots = Object.FindObjectsByType<Bootstrapper>(FindObjectsSortMode.None);
            CombatFrame[] frames = Object.FindObjectsByType<CombatFrame>(FindObjectsSortMode.None);
            Assert.That(boots.Length, Is.EqualTo(1));
            Assert.That(boots[0].HasBooted, Is.True);
            Assert.That(frames.Length, Is.EqualTo(1));
            Assert.That(frames[0].IsInitialized, Is.True);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
