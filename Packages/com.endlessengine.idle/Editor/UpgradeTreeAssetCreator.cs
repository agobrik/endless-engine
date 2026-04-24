using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Creates/rebuilds UpgradeTreeConfigSO with a grid-based single-root layout.
    ///
    /// GridX = column index, GridY = row index (0-based, origin top-left).
    /// UpgradeScreenController converts (col, row) → pixels:
    ///   px = PadX + col * CellW + CardOffX
    ///   py = PadY + row * CellH + CardOffY
    ///
    /// Layout overview (col, row):
    ///   Root "Awakening"  at (10, 0)  — canvas centre-top
    ///
    ///   Production   columns 0–5,   rows 1–9   (upper-left)
    ///   Economy      columns 0–5,   rows 5–13  (lower-left)
    ///   Combat       columns 14–20, rows 1–9   (upper-right)
    ///   Survival     columns 14–20, rows 5–13  (lower-right)
    ///   Prestige     columns 6–14,  rows 9–14  (bottom-centre)
    ///
    ///   Cross-branch nodes bridge the zones.
    ///
    /// Menu: Tools → Endless Engine → Create Upgrade Tree
    /// Menu: Tools → Endless Engine → Rebuild Upgrade Tree
    /// </summary>
    public static class UpgradeTreeAssetCreator
    {
        private const string Folder = "Assets/Configs";
        private const string Path   = Folder + "/UpgradeTreeConfig.asset";

        [MenuItem("Tools/Endless Engine/Create Upgrade Tree")]
        public static void CreateUpgradeTree()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "Configs");

            var existing = AssetDatabase.LoadAssetAtPath<UpgradeTreeConfigSO>(Path);
            if (existing != null)
            {
                Debug.Log("[UpgradeTreeAssetCreator] Already exists — use Rebuild to overwrite.");
                return;
            }
            WriteAsset(ScriptableObject.CreateInstance<UpgradeTreeConfigSO>(), isNew: true);
        }

        [MenuItem("Tools/Endless Engine/Rebuild Upgrade Tree")]
        public static void RebuildUpgradeTree()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "Configs");

            var so = AssetDatabase.LoadAssetAtPath<UpgradeTreeConfigSO>(Path)
                     ?? ScriptableObject.CreateInstance<UpgradeTreeConfigSO>();
            WriteAsset(so, isNew: !AssetDatabase.Contains(so));
        }

        private static void WriteAsset(UpgradeTreeConfigSO so, bool isNew)
        {
            so.Nodes = BuildNodes();
            if (isNew) AssetDatabase.CreateAsset(so, Path);
            else       EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UpgradeTreeAssetCreator] {(isNew ? "Created" : "Rebuilt")} {Path} — {so.Nodes.Count} nodes.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Grid layout. GridX = col, GridY = row.
        // Each node occupies exactly one cell. No two nodes share the same (col,row).
        // Connections are drawn as orthogonal L-shaped lines in the controller.
        // ─────────────────────────────────────────────────────────────────────────

        private static List<UpgradeNodeDefinition> BuildNodes()
        {
            var n = new List<UpgradeNodeDefinition>();

            // ══════════════════════════════════════════════════════════════════════
            // LAYOUT MAP  (col, row)  —  canvas is 22 cols × 20 rows
            //
            //   col:  0  1  2  3  4  5  6  7  8  9 [10] 11 12 13 14 15 16 17 18 19 20 21
            //
            //         PRODUCTION (upper-left)      ROOT      COMBAT (upper-right)
            //   row 0: ←── chains radiate outward ──[10,7]──── chains radiate outward ──→
            //         ECONOMY (lower-left)                   SURVIVAL (lower-right)
            //         ←───────────────── PRESTIGE (bottom-centre) ──────────────────→
            //
            // Root sits at col 10, row 7 (vertical centre of canvas).
            // Production grows UP and LEFT   from root entry at (8, 5)
            // Combat grows    UP and RIGHT   from root entry at (12,5)
            // Economy grows   DOWN and LEFT  from root entry at (8, 9)
            // Survival grows  DOWN and RIGHT from root entry at (12,9)
            // Prestige grows  DOWN and CENTRE from (10,13) — requires prestige ≥ 1
            // ══════════════════════════════════════════════════════════════════════

            // ── ROOT — col 10, row 7 (canvas centre) ────────────────────────────
            n.Add(N("awakening", "Awakening", "The spark. +10% gold, +5% passive income.",
                UpgradeCategory.Production, StatType.GoldDropMultiplier,
                0.10f, UpgradeEffectType.PercentBonus, 1, 0, 1f,
                col: 10, row: 7));

            // ── PRODUCTION  upper-left  (cols 9→0, rows 6→0) ─────────────────────
            // entry (9,6) — 1 step diagonal from root
            n.Add(N("prod_gen_tune",     "Generator Tune",  "Generators spin 10% faster.",
                UpgradeCategory.Production, StatType.GeneratorSpeed,
                0.10f, UpgradeEffectType.PercentBonus, 5, 120, 1.5f,
                col: 9, row: 6, pre1: "awakening"));

            n.Add(N("prod_gen_speed_1",  "Efficiency I",    "+15% generator speed.",
                UpgradeCategory.Production, StatType.GeneratorSpeed,
                0.15f, UpgradeEffectType.PercentBonus, 5, 500, 1.5f,
                col: 7, row: 6, pre1: "prod_gen_tune"));

            n.Add(N("prod_gen_speed_2",  "Efficiency II",   "+20% generator speed.",
                UpgradeCategory.Production, StatType.GeneratorSpeed,
                0.20f, UpgradeEffectType.PercentBonus, 5, 2000, 1.5f,
                col: 5, row: 6, pre1: "prod_gen_speed_1"));

            n.Add(N("prod_gen_overdrive","Overdrive",       "+30% generator speed.",
                UpgradeCategory.Production, StatType.GeneratorSpeed,
                0.30f, UpgradeEffectType.PercentBonus, 3, 8000, 1.7f,
                col: 3, row: 6, pre1: "prod_gen_speed_2"));

            n.Add(N("prod_double_gen",   "Double Tick",     "+3% double-tick chance.",
                UpgradeCategory.Production, StatType.DoubleGeneratorChance,
                0.03f, UpgradeEffectType.PercentBonus, 5, 800, 1.6f,
                col: 2, row: 4, pre1: "prod_gen_overdrive"));

            n.Add(N("prod_cascade",      "Cascade",         "+6% double-tick chance.",
                UpgradeCategory.Production, StatType.DoubleGeneratorChance,
                0.06f, UpgradeEffectType.PercentBonus, 3, 5000, 1.7f,
                col: 1, row: 2, pre1: "prod_double_gen"));

            n.Add(N("prod_run_passive_1","Run Passive I",   "+10% passive income during runs.",
                UpgradeCategory.Production, StatType.ActiveRunPassiveBonus,
                0.10f, UpgradeEffectType.PercentBonus, 5, 300, 1.5f,
                col: 9, row: 4, pre1: "prod_gen_tune"));

            n.Add(N("prod_run_passive_2","Run Passive II",  "+20% passive income during runs.",
                UpgradeCategory.Production, StatType.ActiveRunPassiveBonus,
                0.20f, UpgradeEffectType.PercentBonus, 5, 1500, 1.5f,
                col: 7, row: 4, pre1: "prod_run_passive_1"));

            n.Add(N("prod_run_passive_3","Passive Mastery", "+35% passive income during runs.",
                UpgradeCategory.Production, StatType.ActiveRunPassiveBonus,
                0.35f, UpgradeEffectType.PercentBonus, 3, 6000, 1.6f,
                col: 5, row: 4, pre1: "prod_run_passive_2"));

            n.Add(N("prod_offline_1",    "Offline Yield I", "+5% gold while offline.",
                UpgradeCategory.Production, StatType.OfflineYieldRate,
                0.05f, UpgradeEffectType.PercentBonus, 5, 200, 1.4f,
                col: 9, row: 2, pre1: "prod_run_passive_1"));

            n.Add(N("prod_offline_2",    "Offline Yield II","+10% offline gold.",
                UpgradeCategory.Production, StatType.OfflineYieldRate,
                0.10f, UpgradeEffectType.PercentBonus, 5, 1000, 1.4f,
                col: 7, row: 2, pre1: "prod_offline_1"));

            n.Add(N("prod_offline_3",    "Offline Vault",   "+20% offline yield cap.",
                UpgradeCategory.Production, StatType.OfflineYieldRate,
                0.20f, UpgradeEffectType.PercentBonus, 5, 4000, 1.4f,
                col: 5, row: 2, pre1: "prod_offline_2"));

            n.Add(N("prod_run_duration_1","Extended Run I", "+10s run duration.",
                UpgradeCategory.Production, StatType.RunDurationBonus,
                10f, UpgradeEffectType.FlatBonus, 5, 250, 1.5f,
                col: 3, row: 4, pre1: "prod_gen_overdrive"));

            n.Add(N("prod_run_duration_2","Extended Run II","+20s run duration.",
                UpgradeCategory.Production, StatType.RunDurationBonus,
                20f, UpgradeEffectType.FlatBonus, 5, 1200, 1.5f,
                col: 1, row: 4, pre1: "prod_run_duration_1"));

            n.Add(N("prod_starting_gold_1","Starting Gold I","+50 gold at run start.",
                UpgradeCategory.Production, StatType.StartingGoldBonus,
                50f, UpgradeEffectType.FlatBonus, 5, 150, 1.4f,
                col: 3, row: 2, pre1: "prod_run_duration_1"));

            n.Add(N("prod_starting_gold_2","Starting Gold II","+100 gold at run start.",
                UpgradeCategory.Production, StatType.StartingGoldBonus,
                100f, UpgradeEffectType.FlatBonus, 5, 600, 1.4f,
                col: 1, row: 0, pre1: "prod_starting_gold_1"));

            // ── COMBAT  upper-right  (cols 11→20, rows 6→0) ──────────────────────
            // entry (11,6) — 1 step diagonal from root
            n.Add(N("cbt_sharp_edge",  "Sharp Edge",        "+8 attack damage.",
                UpgradeCategory.Combat, StatType.Damage,
                8f, UpgradeEffectType.FlatBonus, 5, 100, 1.5f,
                col: 11, row: 6, pre1: "awakening"));

            n.Add(N("cbt_dmg_1",       "Battle-Hardened I", "+12 attack damage.",
                UpgradeCategory.Combat, StatType.Damage,
                12f, UpgradeEffectType.FlatBonus, 5, 500, 1.5f,
                col: 13, row: 6, pre1: "cbt_sharp_edge"));

            n.Add(N("cbt_dmg_2",       "Battle-Hardened II","+18 attack damage.",
                UpgradeCategory.Combat, StatType.Damage,
                18f, UpgradeEffectType.FlatBonus, 5, 2000, 1.5f,
                col: 15, row: 6, pre1: "cbt_dmg_1"));

            n.Add(N("cbt_apex",        "Apex Predator",     "+30 attack damage.",
                UpgradeCategory.Combat, StatType.Damage,
                30f, UpgradeEffectType.FlatBonus, 3, 8000, 1.7f,
                col: 17, row: 6, pre1: "cbt_dmg_2"));

            n.Add(N("cbt_atkspd_1",    "Swift Strikes I",   "+8% attack speed.",
                UpgradeCategory.Combat, StatType.AttackInterval,
                0.08f, UpgradeEffectType.PercentBonus, 5, 150, 1.5f,
                col: 11, row: 4, pre1: "cbt_sharp_edge"));

            n.Add(N("cbt_atkspd_2",    "Swift Strikes II",  "+12% attack speed.",
                UpgradeCategory.Combat, StatType.AttackInterval,
                0.12f, UpgradeEffectType.PercentBonus, 5, 700, 1.5f,
                col: 13, row: 4, pre1: "cbt_atkspd_1"));

            n.Add(N("cbt_atkspd_3",    "Swift Strikes III", "+18% attack speed.",
                UpgradeCategory.Combat, StatType.AttackInterval,
                0.18f, UpgradeEffectType.PercentBonus, 5, 2500, 1.5f,
                col: 15, row: 4, pre1: "cbt_atkspd_2"));

            n.Add(N("cbt_blur",        "Blur Speed",        "+28% attack speed.",
                UpgradeCategory.Combat, StatType.AttackInterval,
                0.28f, UpgradeEffectType.PercentBonus, 3, 8000, 1.7f,
                col: 17, row: 4, pre1: "cbt_atkspd_3"));

            n.Add(N("cbt_crit_1",      "Critical Eye I",    "+3% crit chance.",
                UpgradeCategory.Combat, StatType.CritChance,
                0.03f, UpgradeEffectType.PercentBonus, 5, 200, 1.5f,
                col: 11, row: 2, pre1: "cbt_atkspd_1"));

            n.Add(N("cbt_crit_2",      "Critical Eye II",   "+5% crit chance.",
                UpgradeCategory.Combat, StatType.CritChance,
                0.05f, UpgradeEffectType.PercentBonus, 5, 900, 1.5f,
                col: 13, row: 2, pre1: "cbt_crit_1"));

            n.Add(N("cbt_deadeye",     "Deadeye",           "+8% crit chance.",
                UpgradeCategory.Combat, StatType.CritChance,
                0.08f, UpgradeEffectType.PercentBonus, 3, 5000, 1.6f,
                col: 15, row: 2, pre1: "cbt_crit_2"));

            n.Add(N("cbt_range_1",     "Long Reach I",      "+10% attack range.",
                UpgradeCategory.Combat, StatType.AttackRange,
                0.10f, UpgradeEffectType.PercentBonus, 5, 300, 1.5f,
                col: 17, row: 2, pre1: "cbt_dmg_2"));

            n.Add(N("cbt_range_2",     "Long Reach II",     "+20% attack range.",
                UpgradeCategory.Combat, StatType.AttackRange,
                0.20f, UpgradeEffectType.PercentBonus, 5, 1200, 1.5f,
                col: 19, row: 2, pre1: "cbt_range_1"));

            n.Add(N("cbt_aoe_1",       "Blast Radius I",    "+10% area damage.",
                UpgradeCategory.Combat, StatType.AreaDamage,
                0.10f, UpgradeEffectType.PercentBonus, 5, 400, 1.5f,
                col: 19, row: 4, pre1: "cbt_range_1"));

            n.Add(N("cbt_aoe_2",       "Blast Radius II",   "+20% area damage.",
                UpgradeCategory.Combat, StatType.AreaDamage,
                0.20f, UpgradeEffectType.PercentBonus, 5, 1800, 1.5f,
                col: 19, row: 6, pre1: "cbt_aoe_1"));

            n.Add(N("cbt_shockwave",   "Shockwave",         "+35% area damage.",
                UpgradeCategory.Combat, StatType.AreaDamage,
                0.35f, UpgradeEffectType.PercentBonus, 3, 6000, 1.7f,
                col: 19, row: 0, pre1: "cbt_range_2"));

            n.Add(N("cbt_combo",       "Combo Breaker",     "+5% combo multiplier.",
                UpgradeCategory.Combat, StatType.ComboMultiplier,
                0.05f, UpgradeEffectType.PercentBonus, 5, 500, 1.6f,
                col: 17, row: 0, pre1: "cbt_atkspd_2"));

            // ── ECONOMY  lower-left  (cols 9→0, rows 8→14) ───────────────────────
            // entry (9,8) — 1 step diagonal from root
            n.Add(N("eco_opportunist",  "Opportunist",       "+10% gold from all sources.",
                UpgradeCategory.Economy, StatType.GoldDropMultiplier,
                0.10f, UpgradeEffectType.PercentBonus, 5, 100, 1.5f,
                col: 9, row: 8, pre1: "awakening"));

            n.Add(N("eco_gold_drop_1",  "Looter I",          "+15% gold from enemies.",
                UpgradeCategory.Economy, StatType.GoldDropMultiplier,
                0.15f, UpgradeEffectType.PercentBonus, 5, 500, 1.5f,
                col: 7, row: 8, pre1: "eco_opportunist"));

            n.Add(N("eco_gold_drop_2",  "Looter II",         "+20% gold from enemies.",
                UpgradeCategory.Economy, StatType.GoldDropMultiplier,
                0.20f, UpgradeEffectType.PercentBonus, 5, 2000, 1.5f,
                col: 5, row: 8, pre1: "eco_gold_drop_1"));

            n.Add(N("eco_gold_rush",    "Gold Rush",         "+30% gold from enemies.",
                UpgradeCategory.Economy, StatType.GoldDropMultiplier,
                0.30f, UpgradeEffectType.PercentBonus, 3, 8000, 1.7f,
                col: 3, row: 8, pre1: "eco_gold_drop_2"));

            n.Add(N("eco_pickup_1",     "Magnet I",          "+15% gold pickup range.",
                UpgradeCategory.Economy, StatType.GoldPickupRange,
                0.15f, UpgradeEffectType.PercentBonus, 5, 150, 1.4f,
                col: 9, row: 10, pre1: "eco_opportunist"));

            n.Add(N("eco_pickup_2",     "Magnet II",         "+30% gold pickup range.",
                UpgradeCategory.Economy, StatType.GoldPickupRange,
                0.30f, UpgradeEffectType.PercentBonus, 5, 700, 1.4f,
                col: 7, row: 10, pre1: "eco_pickup_1"));

            n.Add(N("eco_vacuum",       "Vacuum Field",      "+50% gold pickup range.",
                UpgradeCategory.Economy, StatType.GoldPickupRange,
                0.50f, UpgradeEffectType.PercentBonus, 3, 5000, 1.6f,
                col: 5, row: 10, pre1: "eco_pickup_2"));

            n.Add(N("eco_run_reward_1", "Bonus Reward I",    "+10% end-of-run gold.",
                UpgradeCategory.Economy, StatType.BonusRunReward,
                0.10f, UpgradeEffectType.PercentBonus, 5, 200, 1.5f,
                col: 7, row: 12, pre1: "eco_pickup_1"));

            n.Add(N("eco_run_reward_2", "Bonus Reward II",   "+20% end-of-run gold.",
                UpgradeCategory.Economy, StatType.BonusRunReward,
                0.20f, UpgradeEffectType.PercentBonus, 5, 900, 1.5f,
                col: 5, row: 12, pre1: "eco_run_reward_1"));

            n.Add(N("eco_jackpot",      "Jackpot",           "+35% end-of-run gold.",
                UpgradeCategory.Economy, StatType.BonusRunReward,
                0.35f, UpgradeEffectType.PercentBonus, 3, 6000, 1.7f,
                col: 3, row: 12, pre1: "eco_run_reward_2"));

            n.Add(N("eco_combo_1",      "Combo Economist I", "+5% gold per combo.",
                UpgradeCategory.Economy, StatType.ComboMultiplier,
                0.05f, UpgradeEffectType.PercentBonus, 5, 300, 1.5f,
                col: 3, row: 10, pre1: "eco_gold_rush"));

            n.Add(N("eco_combo_2",      "Combo Economist II","+10% gold per combo.",
                UpgradeCategory.Economy, StatType.ComboMultiplier,
                0.10f, UpgradeEffectType.PercentBonus, 5, 1300, 1.5f,
                col: 1, row: 10, pre1: "eco_combo_1"));

            n.Add(N("eco_interest_1",   "Compound Interest I","+1% current gold/sec.",
                UpgradeCategory.Economy, StatType.GoldDropMultiplier,
                0.01f, UpgradeEffectType.PercentBonus, 3, 2000, 1.8f,
                col: 1, row: 12, pre1: "eco_combo_2"));

            n.Add(N("eco_interest_2",   "Compound Interest II","+2% current gold/sec.",
                UpgradeCategory.Economy, StatType.GoldDropMultiplier,
                0.02f, UpgradeEffectType.PercentBonus, 3, 8000, 1.8f,
                col: 1, row: 14, pre1: "eco_interest_1"));

            // ── SURVIVAL  lower-right  (cols 11→20, rows 8→14) ───────────────────
            // entry (11,8) — 1 step diagonal from root
            n.Add(N("sur_hardened",    "Hardened",      "+30 max HP.",
                UpgradeCategory.Survival, StatType.MaxHP,
                30f, UpgradeEffectType.FlatBonus, 5, 100, 1.4f,
                col: 11, row: 8, pre1: "cbt_sharp_edge"));

            n.Add(N("sur_hp_1",        "Fortitude I",   "+50 max HP.",
                UpgradeCategory.Survival, StatType.MaxHP,
                50f, UpgradeEffectType.FlatBonus, 5, 500, 1.4f,
                col: 13, row: 8, pre1: "sur_hardened"));

            n.Add(N("sur_hp_2",        "Fortitude II",  "+80 max HP.",
                UpgradeCategory.Survival, StatType.MaxHP,
                80f, UpgradeEffectType.FlatBonus, 5, 2000, 1.4f,
                col: 15, row: 8, pre1: "sur_hp_1"));

            n.Add(N("sur_colossus",    "Colossus",      "+150 max HP.",
                UpgradeCategory.Survival, StatType.MaxHP,
                150f, UpgradeEffectType.FlatBonus, 3, 8000, 1.6f,
                col: 17, row: 8, pre1: "sur_hp_2"));

            n.Add(N("sur_dr_1",        "Iron Skin I",   "+3% damage reduction.",
                UpgradeCategory.Survival, StatType.DamageReduction,
                0.03f, UpgradeEffectType.PercentBonus, 5, 150, 1.5f,
                col: 11, row: 10, pre1: "sur_hardened"));

            n.Add(N("sur_dr_2",        "Iron Skin II",  "+5% damage reduction.",
                UpgradeCategory.Survival, StatType.DamageReduction,
                0.05f, UpgradeEffectType.PercentBonus, 5, 700, 1.5f,
                col: 13, row: 10, pre1: "sur_dr_1"));

            n.Add(N("sur_dr_3",        "Iron Skin III", "+8% damage reduction.",
                UpgradeCategory.Survival, StatType.DamageReduction,
                0.08f, UpgradeEffectType.PercentBonus, 5, 2500, 1.5f,
                col: 15, row: 10, pre1: "sur_dr_2"));

            n.Add(N("sur_impenetrable","Impenetrable",  "+12% damage reduction.",
                UpgradeCategory.Survival, StatType.DamageReduction,
                0.12f, UpgradeEffectType.PercentBonus, 3, 7000, 1.7f,
                col: 17, row: 10, pre1: "sur_dr_3"));

            n.Add(N("sur_movespd_1",   "Fleet Foot I",  "+5% move speed.",
                UpgradeCategory.Survival, StatType.MoveSpeed,
                0.05f, UpgradeEffectType.PercentBonus, 5, 120, 1.4f,
                col: 11, row: 12, pre1: "sur_dr_1"));

            n.Add(N("sur_movespd_2",   "Fleet Foot II", "+10% move speed.",
                UpgradeCategory.Survival, StatType.MoveSpeed,
                0.10f, UpgradeEffectType.PercentBonus, 5, 550, 1.4f,
                col: 13, row: 12, pre1: "sur_movespd_1"));

            n.Add(N("sur_phantom",     "Phantom Step",  "+25% move speed.",
                UpgradeCategory.Survival, StatType.MoveSpeed,
                0.25f, UpgradeEffectType.PercentBonus, 3, 9000, 1.7f,
                col: 15, row: 12, pre1: "sur_movespd_2"));

            n.Add(N("sur_regen_1",     "Regeneration I","+1 HP/sec.",
                UpgradeCategory.Survival, StatType.HPRegen,
                1f, UpgradeEffectType.FlatBonus, 5, 200, 1.5f,
                col: 17, row: 12, pre1: "sur_hp_2"));

            n.Add(N("sur_regen_2",     "Regeneration II","+2 HP/sec.",
                UpgradeCategory.Survival, StatType.HPRegen,
                2f, UpgradeEffectType.FlatBonus, 5, 900, 1.5f,
                col: 19, row: 10, pre1: "sur_regen_1"));

            n.Add(N("sur_lifedrain_1", "Life Drain I",  "+3 HP per kill.",
                UpgradeCategory.Survival, StatType.HPRegen,
                3f, UpgradeEffectType.FlatBonus, 5, 500, 1.6f,
                col: 19, row: 8, pre1: "sur_colossus"));

            n.Add(N("sur_lifedrain_2", "Life Drain II", "+5 HP per kill.",
                UpgradeCategory.Survival, StatType.HPRegen,
                5f, UpgradeEffectType.FlatBonus, 5, 2000, 1.6f,
                col: 19, row: 12, pre1: "sur_lifedrain_1"));

            // ── CROSS-BRANCH 1 — Engine Efficiency (Prod ↔ Eco, left side row 7) ─
            n.Add(N("engine_efficiency","Engine Efficiency","+10% gen speed AND +10% gold.",
                UpgradeCategory.Production, StatType.GeneratorSpeed,
                0.10f, UpgradeEffectType.PercentBonus, 3, 3000, 1.6f,
                col: 5, row: 7, pres: new[] { "prod_gen_speed_1", "eco_gold_drop_1" }));

            // ── CROSS-BRANCH 2 — Blood Money (Eco ↔ Sur, centre row 9) ──────────
            n.Add(N("blood_money",     "Blood Money",     "+15% gold from kills.",
                UpgradeCategory.Economy, StatType.GoldDropMultiplier,
                0.15f, UpgradeEffectType.PercentBonus, 3, 4000, 1.7f,
                col: 10, row: 9, pres: new[] { "eco_opportunist", "sur_hardened" }));

            // ── CROSS-BRANCH 3 — Idle Guardian (Sur → Prestige bridge) ───────────
            n.Add(N("idle_guardian",   "Idle Guardian",   "+5% DR AND +1 HP/sec.",
                UpgradeCategory.Survival, StatType.DamageReduction,
                0.05f, UpgradeEffectType.PercentBonus, 3, 5000, 1.6f,
                col: 15, row: 14, pres: new[] { "sur_dr_2", "sur_regen_1" }));

            // ── PRESTIGE  bottom-centre  (cols 4–16, rows 11–17) ─────────────────
            n.Add(N("pre_long_game",   "The Long Game",   "+10% prestige multiplier.",
                UpgradeCategory.Prestige, StatType.PrestigeMultiplier,
                0.10f, UpgradeEffectType.PercentBonus, 5, 500, 1.6f,
                col: 10, row: 11,
                pres: new[] { "prod_run_passive_1", "eco_opportunist", "cbt_sharp_edge" },
                pg: 1));

            n.Add(N("pre_surge_1",     "Prestige Surge I","+15% prestige multiplier.",
                UpgradeCategory.Prestige, StatType.PrestigeMultiplier,
                0.15f, UpgradeEffectType.PercentBonus, 5, 2500, 1.6f,
                col: 8, row: 11, pre1: "pre_long_game", pg: 1));

            n.Add(N("pre_surge_2",     "Prestige Surge II","+20% prestige multiplier.",
                UpgradeCategory.Prestige, StatType.PrestigeMultiplier,
                0.20f, UpgradeEffectType.PercentBonus, 5, 10000, 1.6f,
                col: 6, row: 13, pre1: "pre_surge_1", pg: 1));

            n.Add(N("pre_apex",        "Prestige Apex",   "+30% prestige multiplier.",
                UpgradeCategory.Prestige, StatType.PrestigeMultiplier,
                0.30f, UpgradeEffectType.PercentBonus, 3, 25000, 1.8f,
                col: 4, row: 15, pre1: "pre_surge_2", pg: 3));

            n.Add(N("pre_head_start_1","Head Start I",    "+200 starting gold after prestige.",
                UpgradeCategory.Prestige, StatType.StartingGoldBonus,
                200f, UpgradeEffectType.FlatBonus, 5, 800, 1.5f,
                col: 12, row: 11, pre1: "pre_long_game", pg: 1));

            n.Add(N("pre_head_start_2","Head Start II",   "+400 starting gold after prestige.",
                UpgradeCategory.Prestige, StatType.StartingGoldBonus,
                400f, UpgradeEffectType.FlatBonus, 5, 3500, 1.5f,
                col: 14, row: 13, pre1: "pre_head_start_1", pg: 1));

            n.Add(N("pre_grand_start", "Grand Head Start","+1000 starting gold after prestige.",
                UpgradeCategory.Prestige, StatType.StartingGoldBonus,
                1000f, UpgradeEffectType.FlatBonus, 3, 15000, 1.7f,
                col: 16, row: 15, pre1: "pre_head_start_2", pg: 2));

            n.Add(N("pre_retainer_1",  "Retainer I",      "+5% gen speed retained.",
                UpgradeCategory.Prestige, StatType.GeneratorSpeed,
                0.05f, UpgradeEffectType.PercentBonus, 3, 1500, 1.7f,
                col: 10, row: 13, pre1: "pre_long_game", pg: 1));

            n.Add(N("pre_retainer_2",  "Retainer II",     "+10% gen speed retained.",
                UpgradeCategory.Prestige, StatType.GeneratorSpeed,
                0.10f, UpgradeEffectType.PercentBonus, 3, 6000, 1.7f,
                col: 8, row: 13, pre1: "pre_retainer_1", pg: 2));

            n.Add(N("pre_vet_runs_1",  "Veteran Runs I",  "+15% run gold reward.",
                UpgradeCategory.Prestige, StatType.BonusRunReward,
                0.15f, UpgradeEffectType.PercentBonus, 5, 1000, 1.5f,
                col: 12, row: 13, pre1: "pre_head_start_1", pg: 1));

            n.Add(N("pre_vet_runs_2",  "Veteran Runs II", "+25% run gold reward.",
                UpgradeCategory.Prestige, StatType.BonusRunReward,
                0.25f, UpgradeEffectType.PercentBonus, 5, 4500, 1.5f,
                col: 14, row: 15, pre1: "pre_vet_runs_1", pg: 2));

            n.Add(N("pre_warrior_legacy","Warrior's Legacy","+20 attack damage (permanent).",
                UpgradeCategory.Prestige, StatType.Damage,
                20f, UpgradeEffectType.FlatBonus, 5, 3000, 1.6f,
                col: 6, row: 15, pre1: "pre_surge_2", pg: 1));

            n.Add(N("pre_survivor_legacy","Survivor's Legacy","+100 max HP (permanent).",
                UpgradeCategory.Prestige, StatType.MaxHP,
                100f, UpgradeEffectType.FlatBonus, 5, 3000, 1.6f,
                col: 8, row: 15, pre1: "pre_retainer_2", pg: 1));

            n.Add(N("pre_builder_legacy","Builder's Legacy","+15% gen speed (permanent).",
                UpgradeCategory.Prestige, StatType.GeneratorSpeed,
                0.15f, UpgradeEffectType.PercentBonus, 5, 4000, 1.6f,
                col: 12, row: 15, pre1: "pre_vet_runs_2", pg: 1));

            n.Add(N("pre_ascension",   "Ascension",       "+50% all prestige bonuses.",
                UpgradeCategory.Prestige, StatType.PrestigeMultiplier,
                0.50f, UpgradeEffectType.PercentBonus, 1, 50000, 2.0f,
                col: 10, row: 17, pres: new[] { "pre_apex", "pre_grand_start" }, pg: 5));

            return n;
        }

        private static UpgradeNodeDefinition N(
            string id, string name, string desc,
            UpgradeCategory cat, StatType stat, float effectPerRank,
            UpgradeEffectType effectType, int maxRank, float baseCost,
            float scalingFactor,
            int col = 0, int row = 0,
            string pre1 = null, string[] pres = null,
            int pg = 0)
        {
            string[] prereqIds;
            if      (pres != null) prereqIds = pres;
            else if (pre1 != null) prereqIds = new[] { pre1 };
            else                   prereqIds = System.Array.Empty<string>();

            return new UpgradeNodeDefinition
            {
                NodeId                  = id,
                DisplayName             = name,
                Description             = desc,
                Category                = cat,
                AffectedStat            = stat,
                EffectPerRank           = effectPerRank,
                EffectType              = effectType,
                MaxRank                 = maxRank,
                BaseCost                = baseCost,
                CostScalingFactor       = scalingFactor,
                PrerequisiteNodeIDs     = prereqIds,
                PrestigeGateRequirement = pg,
                SelectionWeight         = 50f,
                GridX                   = col,
                GridY                   = row,
            };
        }
    }
}
