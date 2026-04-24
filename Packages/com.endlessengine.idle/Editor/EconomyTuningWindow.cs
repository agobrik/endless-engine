using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Economy Tuning Panel — Idle Game Toolset
    /// Open: Tools → Endless Engine → Economy Tuning
    ///
    /// Tabs:
    ///   Gold Curve      — idle yield/s over generator copies, prestige overlay
    ///   Gen Costs       — per-generator cost scaling charts + comparison table
    ///   Prestige        — permanent multiplier curve + threshold table
    ///   Wave Economy    — gold drops per wave, enemy count scaling
    ///   Simulation      — full cross-system snapshot: player state → projected income
    ///   Config Editor   — inline edit all economy SO fields and save
    /// </summary>
    public class EconomyTuningWindow : EditorWindow
    {
        // ── Loaded assets ─────────────────────────────────────────────────────────
        private EconomyConfigSO         _economy;
        private PrestigeConfigSO        _prestige;
        private GeneratorDatabaseSO     _genDb;
        private WaveConfigSO            _wave;
        private RunConfigSO             _run;
        private PlayerBaseStatConfigSO  _playerStats;
        private ConversionDatabaseSO    _conversionDb;
        private SoftCapConfigSO         _softCap;

        // ── UI skeleton ───────────────────────────────────────────────────────────
        private Label         _statusBar;
        private VisualElement _tabBar;
        private VisualElement _content;
        private int           _tab = 0;

        private const int TAB_GOLD       = 0;
        private const int TAB_COST       = 1;
        private const int TAB_PRESTIGE   = 2;
        private const int TAB_WAVE       = 3;
        private const int TAB_SIM        = 4;
        private const int TAB_CFG        = 5;
        private const int TAB_CONVERSION = 6;
        private const int TAB_SOFTCAP    = 7;

        // ── Chart elements (reset per tab rebuild) ────────────────────────────────
        private VisualElement _goldChart;
        private VisualElement _prestigeChart;

        // ── Shared controls ───────────────────────────────────────────────────────
        private int  _genCopies      = 50;   // Gold curve x-axis max
        private int  _costCopies     = 15;   // Gen cost bar count
        private int  _prestigeMax    = 25;   // Prestige preview count

        // ── Simulation state ─────────────────────────────────────────────────────
        private int   _simWave        = 15;
        private int   _simPrestige    = 2;
        private int   _simGenCopies   = 10;   // copies of each gen
        private float _simOfflineHrs  = 4f;
        private bool  _simActiveRun   = false;

        // ── Labels refreshed by sim ───────────────────────────────────────────────
        private Label         _simIdleLabel, _simRunLabel, _simOfflineLabel, _simGoldDropLabel, _simTotalRunLabel;
        private VisualElement _simReadinessContainer;

        [MenuItem("Tools/Endless Engine/Economy Tuning")]
        public static void Open()
        {
            var w = GetWindow<EconomyTuningWindow>("Economy Tuning");
            w.minSize = new Vector2(900, 600);
        }

        private void OnEnable()  { AutoLoad(); BuildUI(); }

        // ════════════════════════════════════════════════════════════════════════
        // Auto-load
        // ════════════════════════════════════════════════════════════════════════

        private void AutoLoad()
        {
            _economy      = TryLoad<EconomyConfigSO>();
            _prestige     = TryLoad<PrestigeConfigSO>();
            _genDb        = TryLoad<GeneratorDatabaseSO>();
            _wave         = TryLoad<WaveConfigSO>();
            _run          = TryLoad<RunConfigSO>();
            _playerStats  = TryLoad<PlayerBaseStatConfigSO>();
            _conversionDb = TryLoad<ConversionDatabaseSO>();
            _softCap      = TryLoad<SoftCapConfigSO>();
        }

        private static T TryLoad<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 0) return null;
            // Prefer Assets/Configs/ over wizard-generated copies
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (p.StartsWith("Assets/Configs/"))
                    return AssetDatabase.LoadAssetAtPath<T>(p);
            }
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ════════════════════════════════════════════════════════════════════════
        // UI skeleton
        // ════════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            BuildToolbar();
            BuildTabBar();
            _content = new VisualElement();
            _content.style.flexGrow  = 1;
            _content.style.overflow  = Overflow.Hidden;
            rootVisualElement.Add(_content);
            ShowTab(_tab);
        }

        private void BuildToolbar()
        {
            var bar = Row(32, new Color(0.18f, 0.18f, 0.18f));
            bar.style.borderBottomWidth = 1;
            bar.style.borderBottomColor = C(0.10f);
            bar.style.paddingLeft       = 8;
            bar.style.paddingRight      = 8;

            bar.Add(TBtn("Economy",      () => Pick<EconomyConfigSO>(a       => { _economy     = a; Reload(); })));
            bar.Add(TBtn("Prestige",     () => Pick<PrestigeConfigSO>(a      => { _prestige    = a; Reload(); })));
            bar.Add(TBtn("Gen DB",       () => Pick<GeneratorDatabaseSO>(a   => { _genDb       = a; Reload(); })));
            bar.Add(TBtn("Wave",         () => Pick<WaveConfigSO>(a          => { _wave        = a; Reload(); })));
            bar.Add(TBtn("Run",          () => Pick<RunConfigSO>(a           => { _run         = a; Reload(); })));
            bar.Add(TBtn("Player Stats",  () => Pick<PlayerBaseStatConfigSO>(a  => { _playerStats  = a; Reload(); })));
            bar.Add(TBtn("Conversion DB", () => Pick<ConversionDatabaseSO>(a   => { _conversionDb = a; Reload(); })));
            bar.Add(TBtn("Soft Cap",      () => Pick<SoftCapConfigSO>(a        => { _softCap      = a; Reload(); })));

            var sp = new VisualElement(); sp.style.flexGrow = 1; bar.Add(sp);

            _statusBar = new Label(StatusText());
            _statusBar.style.fontSize = 9;
            _statusBar.style.color    = C(0.50f);
            _statusBar.style.unityFontStyleAndWeight = FontStyle.Italic;
            bar.Add(_statusBar);

            rootVisualElement.Add(bar);
        }

        private void BuildTabBar()
        {
            _tabBar = Row(30, new Color(0.15f, 0.15f, 0.15f));
            _tabBar.style.borderBottomWidth = 1;
            _tabBar.style.borderBottomColor = C(0.10f);

            string[] names = { "Gold Curve", "Gen Costs", "Prestige", "Wave Economy", "Simulation", "Config Editor", "Conversion", "Soft Cap" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var b = new Button(() => ShowTab(idx)) { text = names[i], name = $"tab{i}" };
                b.style.height  = 30;
                b.style.paddingLeft = b.style.paddingRight = 18;
                b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
                b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 0;
                b.style.borderTopWidth = b.style.borderLeftWidth =
                b.style.borderRightWidth = b.style.borderBottomWidth = 0;
                b.style.marginRight = 0;
                _tabBar.Add(b);
            }
            rootVisualElement.Add(_tabBar);
        }

        private void ShowTab(int idx)
        {
            _tab = idx;
            for (int i = 0; i < _tabBar.childCount; i++)
            {
                if (_tabBar[i] is not Button b) continue;
                bool on = i == idx;
                b.style.backgroundColor = on ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.15f, 0.15f, 0.15f);
                b.style.color = on ? new Color(0.95f, 0.85f, 0.35f) : C(0.60f);
                b.style.borderBottomWidth = on ? 2 : 0;
                b.style.borderBottomColor = new Color(0.95f, 0.85f, 0.35f);
            }
            _content.Clear();
            switch (idx)
            {
                case TAB_GOLD:    BuildGoldTab();     break;
                case TAB_COST:    BuildCostTab();     break;
                case TAB_PRESTIGE:BuildPrestigeTab(); break;
                case TAB_WAVE:    BuildWaveTab();     break;
                case TAB_SIM:        BuildSimTab();        break;
                case TAB_CFG:        BuildCfgTab();        break;
                case TAB_CONVERSION: BuildConversionTab(); break;
                case TAB_SOFTCAP:    BuildSoftCapTab();    break;
            }
        }

        private void Reload()
        {
            _statusBar.text = StatusText();
            ShowTab(_tab);
        }

        // ════════════════════════════════════════════════════════════════════════
        // TAB 0: Gold Curve
        // ════════════════════════════════════════════════════════════════════════

        private void BuildGoldTab()
        {
            var sc = Scroll(); _content.Add(sc);
            var p = Pad(sc);

            p.Add(H1("Gold Curve — Idle Yield / Second"));
            p.Add(H2("Total passive income as generator copies accumulate. Each line = 1 prestige level."));

            var ctrl = Row(24, Color.clear);
            ctrl.style.marginTop = 10; ctrl.style.marginBottom = 6;
            p.Add(ctrl);

            ctrl.Add(CL("Max copies per gen:"));
            var f = IF(_genCopies, v => { _genCopies = Mathf.Clamp(v, 2, 300); _goldChart?.MarkDirtyRepaint(); });
            ctrl.Add(f);

            if (_economy == null || _genDb == null)
                p.Add(Warn(MissingAssets("EconomyConfigSO", "GeneratorDatabaseSO")));

            _goldChart = Chart(300);
            _goldChart.generateVisualContent += DrawGoldCurve;
            p.Add(_goldChart);

            // Per-gen contribution table
            if (_genDb?.Generators != null && _genDb.Generators.Length > 0)
            {
                p.Add(SecLabel("Yield contribution at prestige 0 (×1 / ×10 / ×50 copies)"));
                p.Add(BuildYieldTable());
            }

            // Run income comparison
            if (_run != null && _economy != null)
            {
                p.Add(SecLabel("Active run modifiers"));
                var runRow = Row(22, Color.clear);
                runRow.style.marginTop = 4;
                p.Add(runRow);
                runRow.Add(InfoChip($"Passive ×{_run.ActiveRunPassiveModifier:F2} during run"));
                runRow.Add(InfoChip($"Enemy gold ×{_run.ActiveRunEnemyGoldMultiplier:F1}"));
                runRow.Add(InfoChip($"Run duration {_run.RunDurationSeconds:F0}s"));
                runRow.Add(InfoChip($"Base drop {_economy.BaseGoldDropPerEnemy:F1} / enemy"));
            }
        }

        private void DrawGoldCurve(MeshGenerationContext ctx)
        {
            float W = _goldChart.resolvedStyle.width, H = _goldChart.resolvedStyle.height;
            if (W < 2 || H < 2) return;

            float pL = 56, pR = 16, pT = 12, pB = 28;
            float cW = W - pL - pR, cH = H - pT - pB;
            int steps = _genCopies + 1;

            double baseY = _economy?.IdleYieldRateBase ?? 10.0;
            // Build curves for prestige 0, 1, 3, 5
            int[] prestiges = { 0, 1, 3, 5 };
            var curves = new double[prestiges.Length][];
            double globalMax = 0;

            for (int pi = 0; pi < prestiges.Length; pi++)
            {
                double mult = (_prestige != null)
                    ? Math.Min(_prestige.MaxPermanentMultiplier,
                               Math.Pow(_prestige.BaseMultiplierPerPrestige, prestiges[pi]))
                    : 1.0;
                curves[pi] = new double[steps];
                for (int n = 0; n < steps; n++)
                {
                    double y = baseY * mult;
                    if (_genDb?.Generators != null)
                        foreach (var g in _genDb.Generators)
                            if (g != null) y += g.BaseYieldPerSecond * n * mult;
                    curves[pi][n] = y;
                }
                globalMax = Math.Max(globalMax, curves[pi][steps - 1]);
            }
            if (globalMax <= 0) globalMax = 1;

            var pen = ctx.painter2D;

            // Grid
            pen.strokeColor = C(0.22f);
            pen.lineWidth   = 1;
            for (int gi = 0; gi <= 4; gi++)
            {
                float gy = pT + cH * (1f - gi / 4f);
                pen.BeginPath(); pen.MoveTo(V2(pL, gy)); pen.LineTo(V2(pL + cW, gy)); pen.Stroke();
                ctx.DrawText(FmtVal(globalMax * gi / 4.0), V2(2, gy - 5), 8, C(0.48f));
            }
            // Axes
            pen.strokeColor = C(0.35f); pen.lineWidth = 1;
            pen.BeginPath();
            pen.MoveTo(V2(pL, pT)); pen.LineTo(V2(pL, pT + cH)); pen.LineTo(V2(pL + cW, pT + cH));
            pen.Stroke();

            // Curves
            Color[] lineColors = {
                new Color(0.35f, 0.80f, 0.45f),
                new Color(0.24f, 0.71f, 1.00f),
                new Color(1.00f, 0.72f, 0.15f),
                new Color(1.00f, 0.40f, 0.40f),
            };

            for (int pi = 0; pi < prestiges.Length; pi++)
            {
                var col = lineColors[pi];
                // Fill
                pen.fillColor = new Color(col.r, col.g, col.b, 0.07f);
                pen.BeginPath(); pen.MoveTo(V2(pL, pT + cH));
                for (int n = 0; n < steps; n++)
                {
                    float x = pL + (float)(n / (double)(steps - 1)) * cW;
                    float y = pT + cH - (float)(curves[pi][n] / globalMax) * cH;
                    pen.LineTo(V2(x, y));
                }
                pen.LineTo(V2(pL + cW, pT + cH)); pen.ClosePath(); pen.Fill();

                // Line
                pen.strokeColor = col; pen.lineWidth = pi == 0 ? 2.5f : 1.5f;
                pen.BeginPath();
                for (int n = 0; n < steps; n++)
                {
                    float x = pL + (float)(n / (double)(steps - 1)) * cW;
                    float y = pT + cH - (float)(curves[pi][n] / globalMax) * cH;
                    if (n == 0) pen.MoveTo(V2(x, y)); else pen.LineTo(V2(x, y));
                }
                pen.Stroke();

                // Legend label
                string lbl = $"P{prestiges[pi]}";
                float lx = pL + cW - 28;
                float ly = pT + cH - (float)(curves[pi][steps - 1] / globalMax) * cH - 10 - pi * 12;
                ctx.DrawText(lbl, V2(lx, ly), 8, col);
            }

            // Cap line — shows where MaxPermanentMultiplier ceiling kicks in
            if (_prestige != null && _economy != null)
            {
                double capYield = _economy.IdleYieldRateBase * _prestige.MaxPermanentMultiplier;
                if (_genDb?.Generators != null)
                    foreach (var g in _genDb.Generators)
                        if (g != null) capYield += g.BaseYieldPerSecond * (_genCopies / 2.0) * _prestige.MaxPermanentMultiplier;
                if (capYield <= globalMax)
                {
                    float capY = pT + cH - (float)(capYield / globalMax) * cH;
                    pen.strokeColor = new Color(1f, 0.85f, 0.1f, 0.7f);
                    pen.lineWidth   = 1;
                    pen.BeginPath(); pen.MoveTo(V2(pL, capY)); pen.LineTo(V2(pL + cW, capY)); pen.Stroke();
                    ctx.DrawText($"cap ×{_prestige.MaxPermanentMultiplier:F0}", V2(pL + 2, capY - 9), 8, new Color(1f, 0.85f, 0.1f));
                }
            }

            // X labels
            int xStep = Mathf.Max(1, _genCopies / 8);
            for (int n = 0; n <= _genCopies; n += xStep)
            {
                float x = pL + (float)(n / (double)_genCopies) * cW;
                ctx.DrawText(n.ToString(), V2(x - 4, pT + cH + 6), 8, C(0.48f));
            }
            ctx.DrawText("copies per gen", V2(pL + cW / 2f - 28, pT + cH + 18), 8, C(0.38f));
        }

        private VisualElement BuildYieldTable()
        {
            var t = new VisualElement(); t.style.marginTop = 6;
            var hdr = TRow(true, C(0.20f));
            hdr.Add(TC("Generator", 180, true));
            hdr.Add(TC("×1 copy", 90, true));
            hdr.Add(TC("×10 copies", 100, true));
            hdr.Add(TC("×50 copies", 100, true));
            hdr.Add(TC("Base cost", 90, true));
            hdr.Add(TC("Scale ×", 70, true));
            t.Add(hdr);
            int ri = 0;
            foreach (var g in _genDb.Generators)
            {
                if (g == null) continue;
                var row = TRow(false, ri++ % 2 == 0 ? C(0.17f) : C(0.19f));
                row.Add(Dot(PaletteColor(ri - 1)));
                row.Add(TC(g.DisplayName, 174));
                row.Add(TC(FmtVal(g.BaseYieldPerSecond),      90));
                row.Add(TC(FmtVal(g.BaseYieldPerSecond * 10), 100));
                row.Add(TC(FmtVal(g.BaseYieldPerSecond * 50), 100));
                row.Add(TC(FmtVal(g.BaseCost),               90));
                row.Add(TC($"×{g.CostScalingFactor:F2}",     70));
                t.Add(row);
            }
            return t;
        }

        // ════════════════════════════════════════════════════════════════════════
        // TAB 1: Generator Costs
        // ════════════════════════════════════════════════════════════════════════

        private void BuildCostTab()
        {
            var sc = Scroll(); _content.Add(sc);
            var p = Pad(sc);

            p.Add(H1("Generator Cost Scaling"));
            p.Add(H2("Cost = BaseCost × ScaleFactor^N where N = copy index (0-based)"));

            if (_genDb?.Generators == null || _genDb.Generators.Length == 0)
            { p.Add(Warn(MissingAssets("GeneratorDatabaseSO"))); return; }

            var ctrl = Row(24, Color.clear);
            ctrl.style.marginTop = 10; ctrl.style.marginBottom = 4;
            p.Add(ctrl);
            ctrl.Add(CL("Copies to show:"));
            ctrl.Add(IF(_costCopies, v =>
            {
                _costCopies = Mathf.Clamp(v, 2, 50);
                ShowTab(TAB_COST);
            }));

            foreach (var g in _genDb.Generators)
            {
                if (g == null) continue;
                var sec = new VisualElement(); sec.style.marginBottom = 18;
                var lbl = new Label($"{g.DisplayName}  ·  base {g.BaseCost:N0}  ·  ×{g.CostScalingFactor:F3}/copy");
                lbl.style.fontSize = 11; lbl.style.color = C(0.80f); lbl.style.marginBottom = 4;
                sec.Add(lbl);

                var chart = Chart(90);
                var captured = g;
                chart.generateVisualContent += (ctx) => DrawCostBars(ctx, chart, captured);
                sec.Add(chart);
                p.Add(sec);
            }

            // Comparison table
            p.Add(SecLabel("Cost at copy #1 / #5 / #10 / #20"));
            p.Add(BuildCostTable());
        }

        private void DrawCostBars(MeshGenerationContext ctx, VisualElement el, GeneratorConfigSO g)
        {
            float W = el.resolvedStyle.width, H = el.resolvedStyle.height;
            if (W < 2 || H < 2) return;
            int n = _costCopies;
            var costs = Enumerable.Range(0, n).Select(i => (double)g.CostForCopy(i)).ToArray();
            double max = costs.Max(); if (max <= 0) return;

            float pL = 8, pR = 8, pT = 8, pB = 18;
            float cW = W - pL - pR, cH = H - pT - pB;
            float gap = cW / n, bW = gap * 0.65f;
            var pen = ctx.painter2D;

            pen.strokeColor = C(0.22f); pen.lineWidth = 1;
            pen.BeginPath(); pen.MoveTo(V2(pL, pT)); pen.LineTo(V2(pL + cW, pT)); pen.Stroke();
            ctx.DrawText(FmtVal(max), V2(pL, pT - 2), 8, C(0.50f));

            for (int i = 0; i < n; i++)
            {
                float bH = (float)(costs[i] / max) * cH;
                float x  = pL + i * gap + gap * 0.175f;
                float y  = pT + cH - bH;
                pen.fillColor = Color.Lerp(new Color(0.24f, 0.71f, 1f), new Color(1f, 0.55f, 0.20f), (float)i / (n - 1));
                pen.BeginPath();
                pen.MoveTo(V2(x, y)); pen.LineTo(V2(x + bW, y));
                pen.LineTo(V2(x + bW, pT + cH)); pen.LineTo(V2(x, pT + cH));
                pen.ClosePath(); pen.Fill();
                if (i % Mathf.Max(1, n / 8) == 0 || i == n - 1)
                    ctx.DrawText((i + 1).ToString(), V2(x, pT + cH + 4), 8, C(0.45f));
            }
        }

        private VisualElement BuildCostTable()
        {
            int[] cps = { 0, 4, 9, 19 };
            var t = new VisualElement(); t.style.marginTop = 6;
            var hdr = TRow(true, C(0.20f));
            hdr.Add(TC("Generator", 180, true));
            foreach (int cp in cps) hdr.Add(TC($"#{cp + 1}", 80, true));
            hdr.Add(TC("×10 total", 90, true));
            hdr.Add(TC("Scale", 70, true));
            t.Add(hdr);
            int ri = 0;
            foreach (var g in _genDb.Generators)
            {
                if (g == null) continue;
                var row = TRow(false, ri++ % 2 == 0 ? C(0.17f) : C(0.19f));
                row.Add(Dot(PaletteColor(ri - 1)));
                row.Add(TC(g.DisplayName, 174));
                foreach (int cp in cps) row.Add(TC(FmtVal(g.CostForCopy(cp)), 80));
                double total10 = Enumerable.Range(0, 10).Sum(i => (double)g.CostForCopy(i));
                row.Add(TC(FmtVal(total10), 90));
                row.Add(TC($"×{g.CostScalingFactor:F3}", 70));
                t.Add(row);
            }
            return t;
        }

        // ════════════════════════════════════════════════════════════════════════
        // TAB 2: Prestige
        // ════════════════════════════════════════════════════════════════════════

        private void BuildPrestigeTab()
        {
            var sc = Scroll(); _content.Add(sc);
            var p = Pad(sc);

            p.Add(H1("Prestige Multiplier Curve"));
            p.Add(H2("Formula: Min(Cap, BaseMultiplier ^ PrestigeCount)"));

            if (_prestige == null)
            {
                p.Add(Warn("PrestigeConfigSO bulunamadı."));
                p.Add(CreateAssetBtn<PrestigeConfigSO>("Assets/Configs/PrestigeConfig.asset", DefaultPrestigeConfig, a => { _prestige = a; Reload(); }));
                return;
            }

            var ctrl = Row(24, Color.clear);
            ctrl.style.marginTop = 10; ctrl.style.marginBottom = 6; p.Add(ctrl);
            ctrl.Add(CL("Show up to prestige:"));
            ctrl.Add(IF(_prestigeMax, v => { _prestigeMax = Mathf.Clamp(v, 1, 100); _prestigeChart?.MarkDirtyRepaint(); }));
            ctrl.Add(SpH(16));
            ctrl.Add(InfoChip($"Base ×{_prestige.BaseMultiplierPerPrestige:F2}"));
            ctrl.Add(InfoChip($"Cap ×{_prestige.MaxPermanentMultiplier:F0}"));
            if (_prestige.MaxPrestigeCount > 0) ctrl.Add(InfoChip($"Hard limit: {_prestige.MaxPrestigeCount}"));
            if (_prestige.MinWaveForPrestige > 0) ctrl.Add(InfoChip($"Min wave to prestige: {_prestige.MinWaveForPrestige}"));

            _prestigeChart = Chart(240);
            _prestigeChart.generateVisualContent += DrawPrestigeChart;
            p.Add(_prestigeChart);

            // Cap hit calculation
            int capHit = CapHitPrestige();
            if (capHit > 0)
            {
                var capNote = new Label($"Multiplier cap (×{_prestige.MaxPermanentMultiplier:F0}) reached at prestige {capHit}. " +
                                        "Beyond this, only MinWave progression matters.");
                capNote.style.fontSize = 10;
                capNote.style.color = new Color(1f, 0.45f, 0.45f);
                capNote.style.marginTop = 6; capNote.style.marginBottom = 10;
                p.Add(capNote);
            }

            p.Add(SecLabel("Multiplier table"));
            p.Add(BuildPrestigeTable());
        }

        private int CapHitPrestige()
        {
            if (_prestige == null) return -1;
            for (int i = 1; i <= 100; i++)
                if (Math.Pow(_prestige.BaseMultiplierPerPrestige, i) >= _prestige.MaxPermanentMultiplier)
                    return i;
            return -1;
        }

        private void DrawPrestigeChart(MeshGenerationContext ctx)
        {
            if (_prestige == null) return;
            float W = _prestigeChart.resolvedStyle.width, H = _prestigeChart.resolvedStyle.height;
            if (W < 2 || H < 2) return;

            float pL = 62, pR = 20, pT = 12, pB = 28;
            float cW = W - pL - pR, cH = H - pT - pB;
            int steps = _prestigeMax + 1;
            double cap = _prestige.MaxPermanentMultiplier, baseM = _prestige.BaseMultiplierPerPrestige;
            var mults = Enumerable.Range(0, steps).Select(i => Math.Min(cap, Math.Pow(baseM, i))).ToArray();
            double maxM = mults[steps - 1]; if (maxM <= 0) maxM = 1;

            var pen = ctx.painter2D;
            pen.strokeColor = C(0.22f); pen.lineWidth = 1;
            for (int gi = 0; gi <= 4; gi++)
            {
                float gy = pT + cH * (1f - gi / 4f);
                pen.BeginPath(); pen.MoveTo(V2(pL, gy)); pen.LineTo(V2(pL + cW, gy)); pen.Stroke();
                ctx.DrawText($"×{FmtVal(maxM * gi / 4.0)}", V2(2, gy - 5), 8, C(0.48f));
            }

            // Cap line
            if (cap <= maxM + 0.001)
            {
                float capY = pT + cH * (1f - (float)(cap / maxM));
                pen.strokeColor = new Color(1f, 0.35f, 0.35f, 0.70f); pen.lineWidth = 1;
                pen.BeginPath(); pen.MoveTo(V2(pL, capY)); pen.LineTo(V2(pL + cW, capY)); pen.Stroke();
                ctx.DrawText("cap", V2(pL + cW + 2, capY - 5), 8, new Color(1f, 0.35f, 0.35f, 0.9f));
            }

            // Axes
            pen.strokeColor = C(0.35f); pen.lineWidth = 1;
            pen.BeginPath(); pen.MoveTo(V2(pL, pT)); pen.LineTo(V2(pL, pT + cH)); pen.LineTo(V2(pL + cW, pT + cH)); pen.Stroke();

            var col = new Color(0.95f, 0.85f, 0.35f);
            pen.fillColor = new Color(col.r, col.g, col.b, 0.10f);
            pen.BeginPath(); pen.MoveTo(V2(pL, pT + cH));
            for (int i = 0; i < steps; i++)
            {
                float x = pL + (float)(i / (double)(steps - 1)) * cW;
                float y = pT + cH - (float)(mults[i] / maxM) * cH;
                pen.LineTo(V2(x, y));
            }
            pen.LineTo(V2(pL + cW, pT + cH)); pen.ClosePath(); pen.Fill();

            pen.strokeColor = col; pen.lineWidth = 2;
            pen.BeginPath();
            for (int i = 0; i < steps; i++)
            {
                float x = pL + (float)(i / (double)(steps - 1)) * cW;
                float y = pT + cH - (float)(mults[i] / maxM) * cH;
                if (i == 0) pen.MoveTo(V2(x, y)); else pen.LineTo(V2(x, y));
            }
            pen.Stroke();

            int xStep = Mathf.Max(1, _prestigeMax / 8);
            for (int i = 0; i <= _prestigeMax; i += xStep)
            {
                float x = pL + (float)(i / (double)_prestigeMax) * cW;
                ctx.DrawText(i.ToString(), V2(x - 3, pT + cH + 6), 8, C(0.48f));
            }
            ctx.DrawText("prestige count", V2(pL + cW / 2f - 28, pT + cH + 18), 8, C(0.38f));
        }

        private VisualElement BuildPrestigeTable()
        {
            double cap = _prestige.MaxPermanentMultiplier, baseM = _prestige.BaseMultiplierPerPrestige;
            double prev = 1.0;
            int limit = _prestige.MaxPrestigeCount > 0
                ? Math.Min(_prestige.MaxPrestigeCount, _prestigeMax) : _prestigeMax;

            var t = new VisualElement(); t.style.marginTop = 6;
            var hdr = TRow(true, C(0.20f));
            hdr.Add(TC("#", 40, true));
            hdr.Add(TC("Multiplier", 100, true));
            hdr.Add(TC("+delta", 90, true));
            hdr.Add(TC("Capped", 70, true));
            hdr.Add(TC("Min wave", 80, true));
            if (_economy != null) hdr.Add(TC("Idle/s (base)", 120, true));
            if (_economy != null) hdr.Add(TC("Enemy drop", 100, true));
            t.Add(hdr);

            for (int i = 0; i <= limit; i++)
            {
                double m = Math.Min(cap, Math.Pow(baseM, i));
                bool capped = m >= cap - 0.0001;
                var row = TRow(false, i % 2 == 0 ? C(0.17f) : C(0.19f));

                var n = TC(i.ToString(), 40); n.style.color = C(0.70f); row.Add(n);
                var ml = TC($"×{m:F2}", 100);
                ml.style.color = capped ? new Color(1f, 0.45f, 0.45f) : new Color(0.95f, 0.85f, 0.35f);
                row.Add(ml);

                var dl = TC($"+{FmtVal(m - prev)}", 90);
                dl.style.color = new Color(0.5f, 0.85f, 0.55f);
                row.Add(dl);

                row.Add(TC(capped ? "YES" : "-", 70));
                row.Add(TC(_prestige.MinWaveForPrestige > 0 ? $"W{_prestige.MinWaveForPrestige * (i + 1)}" : "-", 80));
                if (_economy != null)
                {
                    row.Add(TC(FmtVal(_economy.IdleYieldRateBase * m) + "/s", 120));
                    row.Add(TC(FmtVal(_economy.BaseGoldDropPerEnemy * m) + " base", 100));
                }
                t.Add(row);
                prev = m;
            }
            return t;
        }

        // ════════════════════════════════════════════════════════════════════════
        // TAB 3: Wave Economy
        // ════════════════════════════════════════════════════════════════════════

        private void BuildWaveTab()
        {
            var sc = Scroll(); _content.Add(sc);
            var p = Pad(sc);

            p.Add(H1("Wave Economy"));
            p.Add(H2("Enemy count and gold drop scaling per wave number."));

            if (_wave == null || _economy == null)
            { p.Add(Warn(MissingAssets("WaveConfigSO", "EconomyConfigSO"))); }

            if (_wave != null)
            {
                var chips = Row(22, Color.clear); chips.style.marginTop = 8; chips.style.marginBottom = 6;
                p.Add(chips);
                chips.Add(InfoChip($"Base enemies: {_wave.BaseEnemyCountPerWave}"));
                chips.Add(InfoChip($"Scale ×{_wave.EnemyCountScalingFactor:F3}/wave"));
                chips.Add(InfoChip($"Cap on screen: {_wave.HardCapEnemiesOnScreen}"));
                chips.Add(InfoChip($"Elite every {_wave.EliteWaveInterval} waves (×{_wave.EliteStatMultiplier:F1})"));
                chips.Add(InfoChip($"Boss every {_wave.BossWaveInterval} waves"));
            }

            p.Add(BuildWaveTable());
        }

        private VisualElement BuildWaveTable()
        {
            var t = new VisualElement(); t.style.marginTop = 8;
            var hdr = TRow(true, C(0.20f));
            hdr.Add(TC("Wave", 55, true));
            hdr.Add(TC("Enemies", 70, true));
            hdr.Add(TC("Type", 70, true));
            if (_economy != null) hdr.Add(TC("Drop/enemy", 90, true));
            if (_economy != null) hdr.Add(TC("Total drop", 90, true));
            if (_run != null && _economy != null) hdr.Add(TC("×run bonus", 100, true));
            if (_wave != null)
            {
                hdr.Add(TC("Upgrade?", 70, true));
                hdr.Add(TC("Save?", 55, true));
            }
            t.Add(hdr);

            int[] waves = { 1, 5, 10, 15, 20, 25, 30, 40, 50, 75, 100 };
            int ri = 0;
            foreach (int w in waves)
            {
                int enemies = _wave != null
                    ? Mathf.Min(_wave.HardCapEnemiesOnScreen,
                                Mathf.FloorToInt(_wave.BaseEnemyCountPerWave *
                                    (float)Math.Pow(_wave.EnemyCountScalingFactor, w - 1)))
                    : w * 5;

                string wtype = "-";
                Color wcolor = C(0.65f);
                if (_wave != null)
                {
                    if (w % _wave.BossWaveInterval == 0) { wtype = "BOSS"; wcolor = new Color(1f, 0.4f, 0.4f); }
                    else if (w % _wave.EliteWaveInterval == 0) { wtype = "ELITE"; wcolor = new Color(1f, 0.7f, 0.2f); }
                    else wtype = "Normal";
                }

                double drop = _economy != null
                    ? _economy.BaseGoldDropPerEnemy * Math.Pow(_economy.GoldDropScalingExponent, w - 1)
                    : 0;
                double total = drop * enemies;
                double runBonus = (_run != null && _economy != null) ? total * _run.ActiveRunEnemyGoldMultiplier : 0;

                bool upgradeWave = _wave != null && w % _wave.UpgradeSelectionWaveInterval == 0;
                bool saveWave    = _wave != null && w % _wave.WaveSaveMilestoneInterval == 0;

                var row = TRow(false, ri++ % 2 == 0 ? C(0.17f) : C(0.19f));
                row.Add(TC(w.ToString(), 55));
                row.Add(TC(enemies.ToString(), 70));
                var wl = TC(wtype, 70); wl.style.color = wcolor; row.Add(wl);
                if (_economy != null)
                {
                    row.Add(TC(FmtVal(drop), 90));
                    row.Add(TC(FmtVal(total), 90));
                }
                if (_run != null && _economy != null)
                {
                    var bl = TC(FmtVal(runBonus), 100);
                    bl.style.color = new Color(0.5f, 0.85f, 0.55f);
                    row.Add(bl);
                }
                if (_wave != null)
                {
                    var ul = TC(upgradeWave ? "YES" : "-", 70);
                    ul.style.color = upgradeWave ? new Color(0.24f, 0.71f, 1f) : C(0.50f);
                    row.Add(ul);
                    var sl = TC(saveWave ? "YES" : "-", 55);
                    sl.style.color = saveWave ? new Color(0.95f, 0.85f, 0.35f) : C(0.50f);
                    row.Add(sl);
                }
                t.Add(row);
            }
            return t;
        }

        // ════════════════════════════════════════════════════════════════════════
        // TAB 4: Simulation — full cross-system snapshot
        // ════════════════════════════════════════════════════════════════════════

        private void BuildSimTab()
        {
            var sc = Scroll(); _content.Add(sc);
            var p = Pad(sc);

            p.Add(H1("Economy Simulation"));
            p.Add(H2("Set player state → see projected income across all systems simultaneously."));

            // Input grid
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap      = Wrap.Wrap;
            grid.style.marginTop     = 10;
            grid.style.marginBottom  = 12;
            p.Add(grid);

            grid.Add(SimField("Prestige count",    _simPrestige,   v => { _simPrestige   = Mathf.Max(0, v); RefreshSim(); }));
            grid.Add(SimField("Wave reached",      _simWave,       v => { _simWave        = Mathf.Max(1, v); RefreshSim(); }));
            grid.Add(SimField("Gen copies (each)", _simGenCopies,  v => { _simGenCopies   = Mathf.Max(0, v); RefreshSim(); }));
            grid.Add(SimFloatField("Offline hours", _simOfflineHrs, v => { _simOfflineHrs = Mathf.Max(0, v); RefreshSim(); }));

            var toggleRow = Row(22, Color.clear); toggleRow.style.marginBottom = 12; p.Add(toggleRow);
            var tog = new Toggle("Active run active (applies run modifier)") { value = _simActiveRun };
            tog.style.fontSize = 10; tog.style.color = C(0.65f);
            tog.RegisterValueChangedCallback(e => { _simActiveRun = e.newValue; RefreshSim(); });
            toggleRow.Add(tog);

            p.Add(SecLabel("Projected income"));

            // Result cards
            var cards = new VisualElement();
            cards.style.flexDirection = FlexDirection.Row;
            cards.style.flexWrap = Wrap.Wrap;
            cards.style.marginBottom = 12;
            p.Add(cards);

            _simIdleLabel       = SimCard(cards, "Idle yield/s",          "-");
            _simRunLabel        = SimCard(cards, "Run passive yield/s",    "-");
            _simOfflineLabel    = SimCard(cards, "Offline credit",         "-");
            _simGoldDropLabel   = SimCard(cards, "Gold/wave (this wave)",  "-");
            _simTotalRunLabel   = SimCard(cards, "Total run gold (est.)",  "-");

            RefreshSim();

            // Prestige threshold note — rebuilt on every RefreshSim
            p.Add(SecLabel("Prestige readiness"));
            _simReadinessContainer = new VisualElement();
            p.Add(_simReadinessContainer);
        }

        private void RefreshSim()
        {
            if (_simIdleLabel == null) return;

            double prestigeMult = 1.0;
            if (_prestige != null)
                prestigeMult = Math.Min(_prestige.MaxPermanentMultiplier,
                                        Math.Pow(_prestige.BaseMultiplierPerPrestige, _simPrestige));

            // Idle yield/s
            double idle = (_economy?.IdleYieldRateBase ?? 10.0) * prestigeMult;
            if (_genDb?.Generators != null)
                foreach (var g in _genDb.Generators)
                    if (g != null) idle += g.BaseYieldPerSecond * _simGenCopies * prestigeMult;

            // Run modifier only applies when active run toggle is on
            float runPassiveMod  = _simActiveRun ? (_run?.ActiveRunPassiveModifier      ?? 1f) : 1f;
            float runEnemyGoldMod = _simActiveRun ? (_run?.ActiveRunEnemyGoldMultiplier ?? 1f) : 1f;

            double runPassive = idle * runPassiveMod;

            // Offline credit (never affected by run modifier — offline = not in run)
            float capHrs = _economy?.OfflineCapHours ?? 8f;
            float offlineHrs = Mathf.Min(_simOfflineHrs, capHrs);
            double offline = idle * offlineHrs * 3600.0;

            // Gold/wave
            double drop = (_economy?.BaseGoldDropPerEnemy ?? 1.0)
                          * Math.Pow(_economy?.GoldDropScalingExponent ?? 1.2, _simWave - 1);
            int enemies = _wave != null
                ? Mathf.Min(_wave.HardCapEnemiesOnScreen,
                            Mathf.FloorToInt(_wave.BaseEnemyCountPerWave *
                                (float)Math.Pow(_wave.EnemyCountScalingFactor, _simWave - 1)))
                : _simWave * 5;
            double waveGold = drop * enemies * runEnemyGoldMod * prestigeMult;

            // Total run gold (passive + waves)
            float runSecs = _run?.RunDurationSeconds ?? 120f;
            int wavesPerRun = _wave != null ? Mathf.CeilToInt(runSecs / (_wave.WaveDurationSeconds + _wave.WaveTransitionDelaySeconds)) : 5;
            double totalRunGold = runPassive * runSecs;
            if (_wave != null && _economy != null)
            {
                for (int w = 1; w <= wavesPerRun; w++)
                {
                    double d = _economy.BaseGoldDropPerEnemy * Math.Pow(_economy.GoldDropScalingExponent, w - 1);
                    int e = Mathf.Min(_wave.HardCapEnemiesOnScreen,
                                Mathf.FloorToInt(_wave.BaseEnemyCountPerWave * (float)Math.Pow(_wave.EnemyCountScalingFactor, w - 1)));
                    totalRunGold += d * e * runEnemyGoldMod * prestigeMult;
                }
            }

            string runTag = _simActiveRun ? "  [run ×active]" : "  [idle]";
            _simIdleLabel.text     = FmtVal(idle) + "/s";
            _simRunLabel.text      = FmtVal(runPassive) + "/s" + runTag;
            _simOfflineLabel.text  = FmtVal(offline) + $"  ({offlineHrs:F1}h capped at {capHrs:F0}h)";
            _simGoldDropLabel.text = FmtVal(waveGold) + $"  (wave {_simWave}, {enemies} enemies)" + runTag;
            _simTotalRunLabel.text = FmtVal(totalRunGold) + $"  ({wavesPerRun} waves × {runSecs:F0}s run)" + runTag;

            // Rebuild prestige readiness so wave/prestige input changes are reflected
            if (_simReadinessContainer != null)
            {
                _simReadinessContainer.Clear();
                _simReadinessContainer.Add(BuildPrestigeReadiness());
            }
        }

        private VisualElement BuildPrestigeReadiness()
        {
            var wrap = new VisualElement(); wrap.style.marginTop = 6;
            if (_prestige == null) { wrap.Add(Warn("PrestigeConfigSO yüklü değil.")); return wrap; }

            bool minWaveOk = _simWave >= _prestige.MinWaveForPrestige;
            bool notCapped = _prestige.MaxPrestigeCount == 0 || _simPrestige < _prestige.MaxPrestigeCount;
            double nextMult = Math.Min(_prestige.MaxPermanentMultiplier,
                                       Math.Pow(_prestige.BaseMultiplierPerPrestige, _simPrestige + 1));

            var row = Row(22, Color.clear); row.style.marginBottom = 4;
            wrap.Add(row);
            row.Add(StatusDot(minWaveOk));
            row.Add(new Label($"Min wave requirement: {_prestige.MinWaveForPrestige}  (current: {_simWave})  " +
                              (minWaveOk ? "OK" : "NOT YET"))
                { style = { fontSize = 10, color = minWaveOk ? new Color(0.4f, 0.9f, 0.5f) : new Color(1f, 0.5f, 0.3f) } });

            var row2 = Row(22, Color.clear); row2.style.marginBottom = 4;
            wrap.Add(row2);
            row2.Add(StatusDot(notCapped));
            row2.Add(new Label($"Prestige count: {_simPrestige}  " +
                               (_prestige.MaxPrestigeCount > 0 ? $"/ max {_prestige.MaxPrestigeCount}" : "no limit") +
                               "  → next multiplier: ×" + nextMult.ToString("F2"))
                { style = { fontSize = 10, color = notCapped ? C(0.70f) : new Color(1f, 0.5f, 0.3f) } });

            return wrap;
        }

        // ════════════════════════════════════════════════════════════════════════
        // TAB 5: Config Editor — inline edit all SO fields
        // ════════════════════════════════════════════════════════════════════════

        private void BuildCfgTab()
        {
            var sc = Scroll(); _content.Add(sc);
            var p = Pad(sc);

            p.Add(H1("Config Editor"));
            p.Add(H2("Inline edit balance values. Changes are saved to disk immediately on Save."));

            // Render each loaded SO as an inline property editor
            if (_economy     != null) p.Add(SoSection("Economy Config",     _economy,     () => Reload()));
            if (_prestige    != null) p.Add(SoSection("Prestige Config",    _prestige,    () => Reload()));
            if (_run         != null) p.Add(SoSection("Run Config",         _run,         () => Reload()));
            if (_wave        != null) p.Add(SoSection("Wave Config",        _wave,        () => Reload()));
            if (_playerStats != null) p.Add(SoSection("Player Base Stats",  _playerStats, () => Reload()));

            bool anyNull = _economy == null || _prestige == null || _run == null || _wave == null || _playerStats == null;
            if (anyNull)
            {
                var missing = new List<string>();
                if (_economy     == null) missing.Add("EconomyConfigSO");
                if (_prestige    == null) missing.Add("PrestigeConfigSO");
                if (_run         == null) missing.Add("RunConfigSO");
                if (_wave        == null) missing.Add("WaveConfigSO");
                if (_playerStats == null) missing.Add("PlayerBaseStatConfigSO");
                p.Add(Warn($"Not loaded: {string.Join(", ", missing)}. Use toolbar to load or create."));

                if (_prestige == null)
                    p.Add(CreateAssetBtn<PrestigeConfigSO>("Assets/Configs/PrestigeConfig.asset",
                        DefaultPrestigeConfig, a => { _prestige = a; Reload(); }));
            }
        }

        private VisualElement SoSection(string title, ScriptableObject so, Action onSave)
        {
            var wrap = new VisualElement();
            wrap.style.marginBottom = 20;
            wrap.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            wrap.style.borderTopLeftRadius = wrap.style.borderTopRightRadius =
            wrap.style.borderBottomLeftRadius = wrap.style.borderBottomRightRadius = 4;
            wrap.style.paddingTop = wrap.style.paddingBottom =
            wrap.style.paddingLeft = wrap.style.paddingRight = 12;

            var hdrRow = Row(22, Color.clear);
            hdrRow.style.marginBottom = 8;
            var ttl = new Label(title);
            ttl.style.fontSize = 12;
            ttl.style.unityFontStyleAndWeight = FontStyle.Bold;
            ttl.style.color = C(0.88f);
            hdrRow.Add(ttl);
            var sp = new VisualElement(); sp.style.flexGrow = 1; hdrRow.Add(sp);
            var path = AssetDatabase.GetAssetPath(so);
            var pathLbl = new Label(path);
            pathLbl.style.fontSize = 9; pathLbl.style.color = C(0.40f);
            pathLbl.style.alignSelf = Align.Center;
            hdrRow.Add(pathLbl);

            var saveBtn = new Button(() =>
            {
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();
                onSave?.Invoke();
            }) { text = "Save" };
            saveBtn.style.height = 20;
            saveBtn.style.paddingLeft = saveBtn.style.paddingRight = 10;
            saveBtn.style.marginLeft  = 8;
            saveBtn.style.backgroundColor = new Color(0.20f, 0.48f, 0.20f);
            saveBtn.style.borderTopLeftRadius = saveBtn.style.borderTopRightRadius =
            saveBtn.style.borderBottomLeftRadius = saveBtn.style.borderBottomRightRadius = 3;
            hdrRow.Add(saveBtn);
            wrap.Add(hdrRow);

            // Render each serialized field as a field widget
            var serialized = new SerializedObject(so);
            serialized.Update();
            var prop = serialized.GetIterator();
            prop.NextVisible(true); // skip m_Script
            while (prop.NextVisible(false))
            {
                var pf = new PropertyField(prop.Copy());
                pf.style.marginBottom = 3;
                pf.Bind(serialized);
                pf.RegisterCallback<ChangeEvent<int>>(_ => serialized.ApplyModifiedProperties());
                pf.RegisterCallback<ChangeEvent<float>>(_ => serialized.ApplyModifiedProperties());
                pf.RegisterCallback<ChangeEvent<bool>>(_ => serialized.ApplyModifiedProperties());
                pf.RegisterCallback<ChangeEvent<string>>(_ => serialized.ApplyModifiedProperties());
                wrap.Add(pf);
            }

            return wrap;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Asset creation helpers
        // ════════════════════════════════════════════════════════════════════════

        private static void DefaultPrestigeConfig(PrestigeConfigSO so)
        {
            so.MinWaveForPrestige      = 20;
            so.MaxPrestigeCount        = 0;
            so.BaseMultiplierPerPrestige = 1.5f;
            so.MaxPermanentMultiplier  = 100f;
            so.StatsAmplifiedByPrestige = new[] { StatType.Damage, StatType.MaxHP };
        }

        private VisualElement CreateAssetBtn<T>(string path, Action<T> configure, Action<T> onCreated)
            where T : ScriptableObject
        {
            var wrap = Row(28, Color.clear); wrap.style.marginTop = 8;
            var btn = new Button(() =>
            {
                var so = ScriptableObject.CreateInstance<T>();
                configure?.Invoke(so);
                AssetDatabase.CreateAsset(so, path);
                AssetDatabase.SaveAssets();
                onCreated?.Invoke(so);
            }) { text = $"Create {typeof(T).Name} at {path}" };
            btn.style.height = 24;
            btn.style.paddingLeft = btn.style.paddingRight = 14;
            btn.style.backgroundColor = new Color(0.22f, 0.45f, 0.65f);
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 3;
            wrap.Add(btn);
            return wrap;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Helper widgets
        // ════════════════════════════════════════════════════════════════════════

        private static Label SimCard(VisualElement parent, string caption, string value)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 4;
            card.style.paddingTop = card.style.paddingBottom = 10;
            card.style.paddingLeft = card.style.paddingRight = 14;
            card.style.marginRight = 8; card.style.marginBottom = 8;
            card.style.minWidth = 180;

            var cl = new Label(caption.ToUpper());
            cl.style.fontSize = 8; cl.style.color = C(0.50f);
            cl.style.unityFontStyleAndWeight = FontStyle.Bold;
            cl.style.marginBottom = 4;
            card.Add(cl);

            var vl = new Label(value);
            vl.style.fontSize = 14;
            vl.style.color = new Color(0.95f, 0.85f, 0.35f);
            vl.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(vl);

            parent.Add(card);
            return vl;
        }

        private static VisualElement SimField(string label, int initial, Action<int> onChange)
        {
            var wrap = new VisualElement();
            wrap.style.marginRight = 16; wrap.style.marginBottom = 6;
            var lbl = new Label(label);
            lbl.style.fontSize = 9; lbl.style.color = C(0.55f);
            lbl.style.marginBottom = 2;
            wrap.Add(lbl);
            var f = new IntegerField { value = initial };
            f.style.width = 80;
            f.RegisterValueChangedCallback(e => onChange(e.newValue));
            wrap.Add(f);
            return wrap;
        }

        private static VisualElement SimFloatField(string label, float initial, Action<float> onChange)
        {
            var wrap = new VisualElement();
            wrap.style.marginRight = 16; wrap.style.marginBottom = 6;
            var lbl = new Label(label);
            lbl.style.fontSize = 9; lbl.style.color = C(0.55f); lbl.style.marginBottom = 2;
            wrap.Add(lbl);
            var f = new FloatField { value = initial };
            f.style.width = 80;
            f.RegisterValueChangedCallback(e => onChange(e.newValue));
            wrap.Add(f);
            return wrap;
        }

        private static IntegerField IF(int v, Action<int> cb)
        {
            var f = new IntegerField { value = v };
            f.style.width = 60; f.RegisterValueChangedCallback(e => cb(e.newValue)); return f;
        }

        private static VisualElement InfoChip(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 9; l.style.color = C(0.65f);
            l.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            l.style.paddingTop = l.style.paddingBottom = 2;
            l.style.paddingLeft = l.style.paddingRight = 6;
            l.style.marginRight = 4;
            l.style.borderTopLeftRadius = l.style.borderTopRightRadius =
            l.style.borderBottomLeftRadius = l.style.borderBottomRightRadius = 10;
            return l;
        }

        private static VisualElement StatusDot(bool ok)
        {
            var d = new VisualElement();
            d.style.width = 8; d.style.height = 8;
            d.style.borderTopLeftRadius = d.style.borderTopRightRadius =
            d.style.borderBottomLeftRadius = d.style.borderBottomRightRadius = 4;
            d.style.backgroundColor = ok ? new Color(0.3f, 0.85f, 0.4f) : new Color(1f, 0.4f, 0.3f);
            d.style.marginRight = 6; d.style.alignSelf = Align.Center;
            return d;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Layout & style helpers
        // ════════════════════════════════════════════════════════════════════════

        private static ScrollView Scroll()
        {
            var s = new ScrollView(ScrollViewMode.Vertical); s.style.flexGrow = 1; return s;
        }

        private static VisualElement Pad(VisualElement parent)
        {
            var p = new VisualElement();
            p.style.paddingTop = p.style.paddingBottom = 16;
            p.style.paddingLeft = p.style.paddingRight = 20;
            parent.Add(p); return p;
        }

        private static VisualElement Row(float h, Color bg)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems    = Align.Center;
            if (h > 0) r.style.height = h;
            if (bg != Color.clear) r.style.backgroundColor = bg;
            return r;
        }

        private static VisualElement Chart(float height)
        {
            var el = new VisualElement();
            el.style.height = height; el.style.marginTop = 6; el.style.marginBottom = 10;
            el.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius =
            el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = 4;
            return el;
        }

        private static Label H1(string t)
        {
            var l = new Label(t);
            l.style.fontSize = 14; l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = C(0.90f); l.style.marginBottom = 3; return l;
        }
        private static Label H2(string t)
        {
            var l = new Label(t);
            l.style.fontSize = 10; l.style.color = C(0.52f); l.style.marginBottom = 6; return l;
        }
        private static Label SecLabel(string t)
        {
            var l = new Label(t.ToUpper());
            l.style.fontSize = 9; l.style.color = C(0.50f);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 14; l.style.marginBottom = 4;
            l.style.borderBottomWidth = 1; l.style.borderBottomColor = C(0.22f);
            return l;
        }
        private static Label CL(string t)
        {
            var l = new Label(t); l.style.fontSize = 10; l.style.color = C(0.60f);
            l.style.marginRight = 6; l.style.alignSelf = Align.Center; return l;
        }
        private static Label Warn(string t)
        {
            var l = new Label(t); l.style.fontSize = 11;
            l.style.color = new Color(1f, 0.70f, 0.30f); l.style.marginTop = 10; return l;
        }
        private static Label SubLabel(string t)
        {
            var l = new Label(t); l.style.fontSize = 10; l.style.color = C(0.50f); return l;
        }
        private static VisualElement Spacer(int h) { var v = new VisualElement(); v.style.height = h; return v; }
        private static VisualElement SpH(float w) { var s = new VisualElement(); s.style.width = w; return s; }
        private static VisualElement TRow(bool hdr, Color bg)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.paddingTop = r.style.paddingBottom = hdr ? 4 : 3;
            r.style.paddingLeft = 8; r.style.backgroundColor = bg; return r;
        }
        private static Label TC(string t, float w, bool bold = false)
        {
            var l = new Label(t); l.style.width = w; l.style.fontSize = 10;
            l.style.color = bold ? C(0.75f) : C(0.65f);
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold; return l;
        }
        private static VisualElement Dot(Color col)
        {
            var d = new VisualElement();
            d.style.width = 6; d.style.height = 6;
            d.style.borderTopLeftRadius = d.style.borderTopRightRadius =
            d.style.borderBottomLeftRadius = d.style.borderBottomRightRadius = 3;
            d.style.backgroundColor = col; d.style.marginRight = 6; d.style.alignSelf = Align.Center;
            return d;
        }
        private static Color PaletteColor(int i)
        {
            Color[] p = {
                new Color(1.00f, 0.55f, 0.20f), new Color(0.24f, 0.71f, 1.00f),
                new Color(0.24f, 0.78f, 0.47f), new Color(0.86f, 0.24f, 0.24f),
                new Color(0.71f, 0.31f, 1.00f), new Color(0.95f, 0.85f, 0.35f),
            };
            return p[i % p.Length];
        }
        private static Color C(float v) => new Color(v, v, v);
        private static Vector2 V2(float x, float y) => new Vector2(x, y);

        private static Button TBtn(string lbl, System.Action act)
        {
            var b = new Button(act) { text = lbl };
            b.style.marginRight = 3; b.style.height = 22;
            b.style.paddingLeft = b.style.paddingRight = 8;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 3;
            return b;
        }

        private static void Pick<T>(Action<T> cb) where T : ScriptableObject
        {
            var path = EditorUtility.OpenFilePanel($"Select {typeof(T).Name}", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;
            path = "Assets" + path.Substring(Application.dataPath.Length);
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) cb(a);
            else EditorUtility.DisplayDialog("Wrong type", $"Not a {typeof(T).Name}.", "OK");
        }

        private string StatusText()
        {
            var parts = new List<string>();
            void Add(string name, bool ok) => parts.Add(ok ? name : $"[{name}]");
            Add("Economy",     _economy     != null);
            Add("Prestige",    _prestige    != null);
            Add("Gen DB",      _genDb       != null);
            Add("Wave",        _wave        != null);
            Add("Run",         _run         != null);
            Add("Player",      _playerStats != null);
            return string.Join("  ", parts);
        }

        private static string MissingAssets(params string[] names) =>
            $"Load required assets via toolbar: {string.Join(", ", names)}";

        private static string FmtVal(double v)
        {
            if (v >= 1_000_000_000) return $"{v / 1_000_000_000:F2}B";
            if (v >= 1_000_000)     return $"{v / 1_000_000:F2}M";
            if (v >= 1_000)         return $"{v / 1_000:F1}K";
            return $"{v:F1}";
        }

        // ════════════════════════════════════════════════════════════════════════
        // TAB 6: Conversion
        // ════════════════════════════════════════════════════════════════════════

        private void BuildConversionTab()
        {
            var sc = Scroll(); _content.Add(sc);
            var p = Pad(sc);

            p.Add(H1("Conversion Recipes"));

            if (_conversionDb == null)
            {
                p.Add(Warn("ConversionDatabaseSO yüklü değil. Toolbar → Conversion DB"));
                p.Add(CreateAssetBtn<ConversionDatabaseSO>(
                    "Assets/Configs/ConversionDatabase.asset",
                    so => { },
                    a => { _conversionDb = a; Reload(); }));
                _content.Add(p);
                return;
            }

            if (_conversionDb.Recipes == null || _conversionDb.Recipes.Length == 0)
            {
                p.Add(Warn("ConversionDatabase boş. Inspector'dan recipe ekleyin."));
                _content.Add(p);
                return;
            }

            // Header row
            var hdr = Row(24, new Color(0.20f, 0.20f, 0.20f));
            hdr.Add(TC("Recipe ID",     180, true));
            hdr.Add(TC("Input",         160, true));
            hdr.Add(TC("Output",        160, true));
            hdr.Add(TC("Ratio",          80, true));
            hdr.Add(TC("Cooldown",       80, true));
            hdr.Add(TC("Bulk",           50, true));
            p.Add(hdr);

            bool alt = false;
            foreach (var recipe in _conversionDb.Recipes)
            {
                if (recipe == null) continue;
                var row = Row(22, alt ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.20f, 0.20f, 0.20f));
                alt = !alt;

                double ratio = recipe.InputAmount > 0 ? recipe.OutputAmount / recipe.InputAmount : 0;
                string cooldownStr = recipe.CooldownSeconds > 0 ? $"{recipe.CooldownSeconds:F0}s" : "—";

                row.Add(TC(recipe.RecipeId,  180, false));
                row.Add(TC($"{FmtVal(recipe.InputAmount)} {recipe.InputCurrencyId}",  160, false));
                row.Add(TC($"{FmtVal(recipe.OutputAmount)} {recipe.OutputCurrencyId}", 160, false));
                row.Add(TC($"{ratio:F4}",    80, false));
                row.Add(TC(cooldownStr,       80, false));
                row.Add(TC(recipe.AllowBulk ? "✓" : "—", 50, false));
                p.Add(row);
            }

            // Summary
            p.Add(Spacer(8));
            p.Add(SubLabel($"{_conversionDb.Recipes.Length} recipe tanımlı"));

            _content.Add(p);
        }

        // ════════════════════════════════════════════════════════════════════════
        // TAB 7: Soft Cap
        // ════════════════════════════════════════════════════════════════════════

        private void BuildSoftCapTab()
        {
            var sc = Scroll(); _content.Add(sc);
            var p = Pad(sc);

            p.Add(H1("Soft Cap / Diminishing Returns"));

            if (_softCap == null)
            {
                p.Add(Warn("SoftCapConfigSO yüklü değil. Toolbar → Soft Cap"));
                p.Add(CreateAssetBtn<SoftCapConfigSO>(
                    "Assets/Configs/SoftCapConfig.asset",
                    so => { so.Threshold = 1000; so.HardCeiling = 10000; },
                    a => { _softCap = a; Reload(); }));
                _content.Add(p);
                return;
            }

            // Config summary
            var info = Row(22, new Color(0.18f, 0.18f, 0.18f));
            info.style.marginBottom = 8;
            info.Add(TC($"Curve: {_softCap.CurveType}", 200, true));
            info.Add(TC($"Threshold: {FmtVal(_softCap.Threshold)}", 180, true));
            info.Add(TC($"K: {_softCap.K:F3}", 80, true));
            if (_softCap.CurveType == SoftCapCurveType.Asymptotic)
                info.Add(TC($"Ceiling: {FmtVal(_softCap.HardCeiling)}", 160, true));
            p.Add(info);

            // Sample table: raw vs effective
            double maxRaw = _softCap.CurveType == SoftCapCurveType.Asymptotic
                ? _softCap.HardCeiling * 3
                : _softCap.Threshold * 10;

            var samples = EndlessEngine.Economy.SoftCapEvaluator.Sample(_softCap, maxRaw, sampleCount: 20);

            var hdr = Row(24, new Color(0.22f, 0.22f, 0.22f));
            hdr.Add(TC("Raw Value",       160, true));
            hdr.Add(TC("Effective Value", 160, true));
            hdr.Add(TC("Reduction %",     120, true));
            p.Add(hdr);

            bool alt = false;
            foreach (var (raw, effective) in samples)
            {
                var row = Row(22, alt ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.20f, 0.20f, 0.20f));
                alt = !alt;

                double reduction = raw > 0 ? (1.0 - effective / raw) * 100.0 : 0;
                bool isSoftCapped = raw > _softCap.Threshold;

                var rawLbl  = TC(FmtVal(raw),       160, false);
                var effLbl  = TC(FmtVal(effective),  160, false);
                var redLbl  = TC(isSoftCapped ? $"{reduction:F1}%" : "—", 120, false);

                if (isSoftCapped)
                    effLbl.style.color = new Color(1f, 0.75f, 0.3f);

                row.Add(rawLbl); row.Add(effLbl); row.Add(redLbl);
                p.Add(row);
            }

            // Curve chart
            p.Add(BuildSoftCapChart(samples, maxRaw));

            _content.Add(p);
        }

        private VisualElement BuildSoftCapChart(
            (double raw, double effective)[] samples, double maxRaw)
        {
            const float W = 400, H = 160;
            var container = new VisualElement();
            container.style.width  = W;
            container.style.height = H;
            container.style.backgroundColor = new Color(0.10f, 0.10f, 0.10f);
            container.style.marginBottom = 8;

            var chart = new UnityEngine.UIElements.VisualElement();
            chart.style.width  = W;
            chart.style.height = H;

            double maxEff = 0;
            foreach (var (_, eff) in samples)
                if (eff > maxEff) maxEff = eff;
            if (maxEff <= 0) maxEff = 1;

            chart.generateVisualContent += ctx =>
            {
                var painter = ctx.painter2D;

                // Diagonal (raw = effective reference line)
                painter.strokeColor = new Color(0.4f, 0.4f, 0.4f);
                painter.lineWidth   = 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, H));
                painter.LineTo(new Vector2(W, 0));
                painter.Stroke();

                // Threshold vertical line
                if (_softCap != null && maxRaw > 0)
                {
                    float tx = (float)(_softCap.Threshold / maxRaw * W);
                    painter.strokeColor = new Color(0.9f, 0.6f, 0.2f, 0.5f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(tx, 0));
                    painter.LineTo(new Vector2(tx, H));
                    painter.Stroke();
                }

                // Effective curve
                painter.strokeColor = new Color(0.3f, 0.85f, 0.4f);
                painter.lineWidth   = 2f;
                painter.BeginPath();
                bool first = true;
                foreach (var (raw, eff) in samples)
                {
                    float x = maxRaw > 0 ? (float)(raw / maxRaw * W) : 0;
                    float y = H - (float)(eff / maxEff * H);
                    y = Mathf.Clamp(y, 0, H);
                    if (first) { painter.MoveTo(new Vector2(x, y)); first = false; }
                    else         painter.LineTo(new Vector2(x, y));
                }
                painter.Stroke();
            };

            container.Add(chart);

            // Legend
            var legend = new Label($"Green = effective  |  Grey = raw (diagonal)  |  Orange = threshold ({FmtVal(_softCap?.Threshold ?? 0)})");
            legend.style.fontSize = 9;
            legend.style.color    = C(0.50f);
            legend.style.marginTop = 4;
            container.Add(legend);

            return container;
        }

    }
}
