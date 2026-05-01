using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Multi-session economy simulator for game designers.
    /// Open: Tools → Endless Engine → Economy Simulator
    ///
    /// Simulates a player's progression across N sessions, modeling:
    ///   - Gold accumulation per session (idle + waves + offline)
    ///   - Prestige timing (when the player hits the prestige gate)
    ///   - Prestige multiplier growth curve
    ///   - Generator upgrade timing (cost vs gold rate)
    ///
    /// All simulation is purely mathematical — no Unity scene state required.
    /// </summary>
    public class EconomySimulatorWindow : EditorWindow
    {
        // ── Menu item ─────────────────────────────────────────────────────────────

        [MenuItem("Tools/Endless Engine/Economy Simulator")]
        public static void Open() => GetWindow<EconomySimulatorWindow>("Economy Simulator");

        // ── Config references ─────────────────────────────────────────────────────

        private EconomyConfigSO         _economy;
        private PrestigeConfigSO        _prestige;
        private GeneratorDatabaseSO     _genDb;
        private WaveConfigSO            _wave;
        private RunConfigSO             _run;

        // ── Simulation parameters ─────────────────────────────────────────────────

        private int   _sessions           = 30;
        private float _sessionMinutes     = 30f;
        private float _offlineHoursBetween = 4f;
        private int   _genTargetCopies    = 10;
        private bool  _autoPrestige       = true;

        // ── Results ───────────────────────────────────────────────────────────────

        private List<SessionSnapshot> _snapshots = new();
        private Vector2               _scroll;
        private bool                  _dirty = true;

        // ── GUI ───────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Economy Simulator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawConfigSection();
            EditorGUILayout.Space(8);
            DrawParameterSection();
            EditorGUILayout.Space(8);

            if (_dirty || GUILayout.Button("Run Simulation"))
            {
                RunSimulation();
                _dirty = false;
            }

            EditorGUILayout.Space(8);
            DrawResults();

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigSection()
        {
            GUILayout.Label("Config Assets", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _economy  = (EconomyConfigSO)        EditorGUILayout.ObjectField("Economy Config",  _economy,  typeof(EconomyConfigSO),        false);
            _prestige = (PrestigeConfigSO)        EditorGUILayout.ObjectField("Prestige Config", _prestige, typeof(PrestigeConfigSO),        false);
            _genDb    = (GeneratorDatabaseSO)     EditorGUILayout.ObjectField("Generator DB",    _genDb,    typeof(GeneratorDatabaseSO),     false);
            _wave     = (WaveConfigSO)            EditorGUILayout.ObjectField("Wave Config",     _wave,     typeof(WaveConfigSO),            false);
            _run      = (RunConfigSO)             EditorGUILayout.ObjectField("Run Config",      _run,      typeof(RunConfigSO),             false);
            if (EditorGUI.EndChangeCheck()) _dirty = true;
        }

        private void DrawParameterSection()
        {
            GUILayout.Label("Simulation Parameters", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _sessions           = EditorGUILayout.IntSlider("Sessions to simulate",   _sessions,           1, 100);
            _sessionMinutes     = EditorGUILayout.Slider("Session length (min)",       _sessionMinutes,     5f, 120f);
            _offlineHoursBetween = EditorGUILayout.Slider("Offline between sessions (hr)", _offlineHoursBetween, 0f, 24f);
            _genTargetCopies    = EditorGUILayout.IntSlider("Gen copies purchased/session", _genTargetCopies, 0, 50);
            _autoPrestige       = EditorGUILayout.Toggle("Auto-prestige when eligible", _autoPrestige);
            if (EditorGUI.EndChangeCheck()) _dirty = true;
        }

        private void DrawResults()
        {
            if (_snapshots.Count == 0) return;

            GUILayout.Label("Session-by-Session Results", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Table header
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Session", GUILayout.Width(55));
                GUILayout.Label("Prestige", GUILayout.Width(55));
                GUILayout.Label("Gold Earned", GUILayout.Width(110));
                GUILayout.Label("Total Gold", GUILayout.Width(110));
                GUILayout.Label("Perm Mult", GUILayout.Width(75));
                GUILayout.Label("Prestiged?", GUILayout.Width(70));
            }

            for (int i = 0; i < _snapshots.Count; i++)
            {
                var s = _snapshots[i];
                var bg = i % 2 == 0 ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.18f, 0.18f, 0.18f);
                var rect = EditorGUILayout.BeginHorizontal();
                EditorGUI.DrawRect(rect, bg);

                GUILayout.Label($"{i + 1}", GUILayout.Width(55));
                GUILayout.Label($"{s.PrestigeCount}", GUILayout.Width(55));
                GUILayout.Label(FormatGold(s.GoldEarned), GUILayout.Width(110));
                GUILayout.Label(FormatGold(s.TotalGoldLifetime), GUILayout.Width(110));
                GUILayout.Label($"×{s.PermanentMultiplier:F2}", GUILayout.Width(75));
                GUILayout.Label(s.PrestigedThisSession ? "✓" : "", GUILayout.Width(70));

                EditorGUILayout.EndHorizontal();
            }
        }

        // ── Simulation engine ─────────────────────────────────────────────────────

        private void RunSimulation()
        {
            _snapshots.Clear();

            double gold        = 0;
            double totalGold   = 0;
            int    prestige    = 0;
            float  baseYield   = _economy?.IdleYieldRateBase ?? 10f;
            float  baseMult    = _prestige?.BaseMultiplierPerPrestige ?? 1.5f;
            float  maxMult     = _prestige?.MaxPermanentMultiplier ?? 100f;
            int    minWave     = _prestige?.MinWaveForPrestige ?? 20;
            int    maxPrestige = _prestige?.MaxPrestigeCount ?? 0;
            float  runSecs     = (_run?.RunDurationSeconds ?? 120f) + _sessionMinutes * 60f;
            int    waveInt     = _wave?.UpgradeSelectionWaveInterval ?? 5;
            float  waveDur     = (_wave?.WaveDurationSeconds ?? 8f) + (_wave?.WaveTransitionDelaySeconds ?? 2f);
            int    wavesPerSess = Mathf.Max(1, Mathf.FloorToInt(runSecs / waveDur));

            for (int s = 0; s < _sessions; s++)
            {
                float permMult = (float)Math.Min(maxMult, Math.Pow(baseMult, prestige));
                float genYield = 0f;
                if (_genDb?.Generators != null)
                    foreach (var g in _genDb.Generators)
                        if (g != null) genYield += g.BaseYieldPerSecond * _genTargetCopies * permMult;

                double sessionYield = (baseYield * permMult + genYield) * runSecs;

                // Offline gold
                float capHrs   = _economy?.OfflineCapHours ?? 8f;
                float offHrs   = Math.Min(_offlineHoursBetween, capHrs);
                double offline = baseYield * permMult * offHrs * 3600.0;

                // Wave gold
                double waveGold = 0;
                if (_economy != null && _wave != null)
                {
                    for (int w = 1; w <= wavesPerSess; w++)
                    {
                        double drop = _economy.BaseGoldDropPerEnemy *
                                      Math.Pow(_economy.GoldDropScalingExponent, w - 1);
                        int enemies = Mathf.Min(_wave.HardCapEnemiesOnScreen,
                                         Mathf.FloorToInt(_wave.BaseEnemyCountPerWave *
                                             (float)Math.Pow(_wave.EnemyCountScalingFactor, w - 1)));
                        waveGold += drop * enemies * permMult;
                    }
                }

                double earned = sessionYield + offline + waveGold;
                gold      += earned;
                totalGold += earned;

                bool prestigedNow = false;
                if (_autoPrestige && wavesPerSess >= minWave &&
                    (maxPrestige == 0 || prestige < maxPrestige))
                {
                    prestige++;
                    gold = 0;
                    prestigedNow = true;
                }

                _snapshots.Add(new SessionSnapshot
                {
                    GoldEarned           = earned,
                    TotalGoldLifetime    = totalGold,
                    PrestigeCount        = prestige,
                    PermanentMultiplier  = (float)Math.Min(maxMult, Math.Pow(baseMult, prestige)),
                    PrestigedThisSession = prestigedNow,
                });
            }
        }

        private static string FormatGold(double v)
        {
            if (v >= 1e12) return $"{v / 1e12:F2}T";
            if (v >= 1e9)  return $"{v / 1e9:F2}B";
            if (v >= 1e6)  return $"{v / 1e6:F2}M";
            if (v >= 1e3)  return $"{v / 1e3:F1}K";
            return $"{v:F0}";
        }

        // ── Data ──────────────────────────────────────────────────────────────────

        private struct SessionSnapshot
        {
            public double GoldEarned;
            public double TotalGoldLifetime;
            public int    PrestigeCount;
            public float  PermanentMultiplier;
            public bool   PrestigedThisSession;
        }
    }
}
