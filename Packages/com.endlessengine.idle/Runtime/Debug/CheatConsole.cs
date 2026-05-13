#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Economy;
using EndlessEngine.Flow;
using EndlessEngine.Generator;

namespace EndlessEngine.DevTools
{
    /// <summary>
    /// In-game cheat console for QA and balance testing.
    /// Toggle with the grave key (~) or backtick (`).
    /// Development builds only — stripped entirely from release builds.
    ///
    /// Commands:
    ///   addgold [amount]      — add gold (e.g. addgold 1e30)
    ///   setgold [amount]      — set gold to exact value
    ///   prestige              — trigger prestige immediately
    ///   setwave [n]           — set wave number
    ///   setstreak [n]         — set daily streak count
    ///   setspeed [x]          — set TickEngine time scale (e.g. setspeed 10)
    ///   clearsave             — wipe all PlayerPrefs save data
    ///   help                  — list all commands
    ///
    /// Bootstrap wiring: Add CheatConsole component to the Bootstrap GameObject.
    /// Inject dependencies via Initialize() after all systems are ready.
    /// </summary>
    public class CheatConsole : MonoBehaviour
    {
        private bool   _visible;
        private string _input = string.Empty;
        private readonly List<string> _log   = new List<string>(64);
        private readonly List<string> _history = new List<string>(32);
        private Vector2 _scrollPos;

        private EconomyService   _economy;
        private TickEngine       _tickEngine;
        private GeneratorSystem  _generators;

        private const int MaxLog = 100;

        private static GUIStyle _boxStyle;
        private static GUIStyle _inputStyle;

        // ── Initialization ────────────────────────────────────────────────────────

        public void Initialize(EconomyService economy, TickEngine tickEngine, GeneratorSystem generators)
        {
            _economy    = economy;
            _tickEngine = tickEngine;
            _generators = generators;
            Log("Cheat console ready. Type 'help' for commands.");
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.BackQuote))
            {
                _visible = !_visible;
                if (_visible) _input = string.Empty;
            }

            if (!_visible) return;

            if (Event.current != null && Event.current.isKey && Event.current.keyCode == KeyCode.Return)
                SubmitCommand(_input);
        }

        private void OnGUI()
        {
            if (!_visible) return;

            float w = Screen.width * 0.6f;
            float h = Screen.height * 0.4f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - h - 20f;

            GUI.Box(new Rect(x, y, w, h), "Cheat Console [~ to close]");

            float logH = h - 60f;
            _scrollPos = GUI.BeginScrollView(
                new Rect(x + 4, y + 20, w - 8, logH),
                _scrollPos,
                new Rect(0, 0, w - 24, _log.Count * 18));

            for (int i = 0; i < _log.Count; i++)
                GUI.Label(new Rect(4, i * 18, w - 30, 18), _log[i]);

            GUI.EndScrollView();

            GUI.SetNextControlName("CheatInput");
            _input = GUI.TextField(new Rect(x + 4, y + h - 36, w - 70, 24), _input);
            GUI.FocusControl("CheatInput");

            if (GUI.Button(new Rect(x + w - 62, y + h - 36, 58, 24), "Execute"))
                SubmitCommand(_input);
        }

        // ── Command Processing ────────────────────────────────────────────────────

        private void SubmitCommand(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            string trimmed = raw.Trim();
            _history.Insert(0, trimmed);
            if (_history.Count > 32) _history.RemoveAt(_history.Count - 1);
            _input = string.Empty;

            Log($"> {trimmed}");

            var parts = trimmed.Split(' ');
            string cmd = parts[0].ToLowerInvariant();

            try
            {
                switch (cmd)
                {
                    case "addgold":  CmdAddGold(parts);  break;
                    case "setgold":  CmdSetGold(parts);  break;
                    case "prestige": CmdPrestige();       break;
                    case "setwave":  CmdSetWave(parts);  break;
                    case "setstreak": CmdSetStreak(parts); break;
                    case "setspeed": CmdSetSpeed(parts); break;
                    case "clearsave": CmdClearSave();    break;
                    case "help":     CmdHelp();          break;
                    default: Log($"Unknown command '{cmd}'. Type 'help'."); break;
                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────────

        private void CmdAddGold(string[] parts)
        {
            if (parts.Length < 2) { Log("Usage: addgold [amount]"); return; }
            double amount = ParseNumber(parts[1]);
            _economy?.AddResources(amount);
            Log($"Added {amount:G4} gold. Balance: {_economy?.CurrentResources:G4}");
        }

        private void CmdSetGold(string[] parts)
        {
            if (parts.Length < 2) { Log("Usage: setgold [amount]"); return; }
            double target = ParseNumber(parts[1]);
            if (_economy == null) { Log("EconomyService not injected."); return; }
            double current = _economy.CurrentResources;
            double delta = target - current;
            if (delta > 0) _economy.AddResources(delta);
            else if (delta < 0) _economy.DeductResources(-delta);
            Log($"Gold set to {_economy.CurrentResources:G4}");
        }

        private void CmdPrestige()
        {
            var mgr = FindFirstObjectByType<Prestige.PrestigeStateManager>();
            if (mgr == null) { Log("PrestigeStateManager not found."); return; }
            bool ok = mgr.TryPrestige();
            Log(ok ? "Prestige triggered." : "Prestige conditions not met (CanPrestige=false).");
        }

        private void CmdSetWave(string[] parts)
        {
            if (parts.Length < 2) { Log("Usage: setwave [n]"); return; }
            if (!int.TryParse(parts[1], out int n)) { Log("Invalid wave number."); return; }
            var mgr = FindFirstObjectByType<Prestige.PrestigeStateManager>();
            mgr?.SetCurrentWave(n);
            Log($"Wave set to {n}.");
        }

        private void CmdSetStreak(string[] parts)
        {
            if (parts.Length < 2) { Log("Usage: setstreak [n]"); return; }
            if (!int.TryParse(parts[1], out int n)) { Log("Invalid streak count."); return; }
            var svc = FindFirstObjectByType<Modules.SessionService>();
            if (svc == null) { Log("SessionService not found."); return; }
            svc.InjectForTesting(n, DateTime.UtcNow);
            Log($"Streak set to {n}.");
        }

        private void CmdSetSpeed(string[] parts)
        {
            if (parts.Length < 2) { Log("Usage: setspeed [x]"); return; }
            if (!float.TryParse(parts[1], out float x)) { Log("Invalid speed."); return; }
            if (_tickEngine != null) _tickEngine.TimeScale = x;
            Log($"TimeScale set to {x}×.");
        }

        private void CmdClearSave()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Log("PlayerPrefs cleared. Restart the game to start fresh.");
        }

        private void CmdHelp()
        {
            Log("Commands:");
            Log("  addgold [amount]   — add gold (supports 1e30 notation)");
            Log("  setgold [amount]   — set gold balance");
            Log("  prestige           — trigger prestige");
            Log("  setwave [n]        — set wave number");
            Log("  setstreak [n]      — set daily streak count");
            Log("  setspeed [x]       — set time scale multiplier");
            Log("  clearsave          — wipe all PlayerPrefs");
            Log("  help               — show this list");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void Log(string msg)
        {
            _log.Add(msg);
            if (_log.Count > MaxLog) _log.RemoveAt(0);
            _scrollPos.y = float.MaxValue;
            UnityEngine.Debug.Log($"[CheatConsole] {msg}");
        }

        private static double ParseNumber(string s)
        {
            if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
                return d;
            throw new FormatException($"Cannot parse '{s}' as a number.");
        }
    }
}
#endif
