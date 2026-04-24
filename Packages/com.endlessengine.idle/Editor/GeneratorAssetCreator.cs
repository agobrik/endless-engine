using UnityEngine;
using UnityEditor;
using EndlessEngine.Config;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Creates the 5 starter generator assets and a GeneratorDatabase asset.
    /// Menu: Tools → Endless Engine → Create Starter Generators
    /// </summary>
    public static class GeneratorAssetCreator
    {
        [MenuItem("Tools/Endless Engine/Create Starter Generators")]
        public static void CreateStarterGenerators()
        {
            const string folder = "Assets/Configs/Generators";

            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/Configs", "Generators");
            }

            var gen1 = CreateGenerator(folder, "gen_basic",       "Basic Producer",    "Generates a trickle of gold.",              1f,   100,  1.15f, 0);
            var gen2 = CreateGenerator(folder, "gen_mine",        "Gold Mine",         "Digs for gold continuously.",               5f,   500,  1.15f, 0);
            var gen3 = CreateGenerator(folder, "gen_factory",     "Gold Factory",      "Processes gold ore at scale.",             25f,  2500, 1.15f, 0);
            var gen4 = CreateGenerator(folder, "gen_vault",       "Vault Network",     "Compounds interest across the network.",   100f, 10000,1.15f, 0);
            var gen5 = CreateGenerator(folder, "gen_black_hole",  "Black Hole",        "Consumes matter, outputs pure energy.",    500f, 50000,1.15f, 0);

            // Wire unlock prerequisites
            if (gen2 != null && gen1 != null) { gen2.UnlockPrerequisite = gen1; gen2.UnlockRequirement = 1; EditorUtility.SetDirty(gen2); }
            if (gen3 != null && gen2 != null) { gen3.UnlockPrerequisite = gen2; gen3.UnlockRequirement = 1; EditorUtility.SetDirty(gen3); }
            if (gen4 != null && gen3 != null) { gen4.UnlockPrerequisite = gen3; gen4.UnlockRequirement = 1; EditorUtility.SetDirty(gen4); }
            if (gen5 != null && gen4 != null) { gen5.UnlockPrerequisite = gen4; gen5.UnlockRequirement = 1; EditorUtility.SetDirty(gen5); }

            // Create GeneratorDatabase
            var dbPath = folder + "/GeneratorDatabase.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GeneratorDatabaseSO>(dbPath);
            if (existing == null)
            {
                var db = ScriptableObject.CreateInstance<GeneratorDatabaseSO>();
                db.Generators = new GeneratorConfigSO[] { gen1, gen2, gen3, gen4, gen5 };
                AssetDatabase.CreateAsset(db, dbPath);
                Debug.Log($"[GeneratorAssetCreator] Created GeneratorDatabase at {dbPath}");
            }
            else
            {
                existing.Generators = new GeneratorConfigSO[] { gen1, gen2, gen3, gen4, gen5 };
                EditorUtility.SetDirty(existing);
                Debug.Log($"[GeneratorAssetCreator] Updated existing GeneratorDatabase.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GeneratorAssetCreator] Done. Assign GeneratorDatabase to Bootstrap Inspector.");
        }

        private static GeneratorConfigSO CreateGenerator(
            string folder, string id, string displayName, string desc,
            float yieldPerSec, long baseCost, float scalingFactor, int unlockReq)
        {
            string path = $"{folder}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GeneratorConfigSO>(path);
            if (existing != null) return existing;

            var cfg = ScriptableObject.CreateInstance<GeneratorConfigSO>();
            cfg.GeneratorId        = id;
            cfg.DisplayName        = displayName;
            cfg.Description        = desc;
            cfg.BaseYieldPerSecond = yieldPerSec;
            cfg.BaseCost           = baseCost;
            cfg.CostScalingFactor  = scalingFactor;
            cfg.MaxCount           = -1;
            cfg.UnlockRequirement  = unlockReq;

            AssetDatabase.CreateAsset(cfg, path);
            Debug.Log($"[GeneratorAssetCreator] Created {path}");
            return cfg;
        }
    }
}
