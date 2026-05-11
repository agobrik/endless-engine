using System.IO;
using UnityEditor;
using UnityEngine;
using EndlessEngine.Bootstrap;
using EndlessEngine.ClickLoop;
using EndlessEngine.Harvest;

namespace EndlessEngine.Editor
{
    /// <summary>
    /// Tools → Endless Engine → Create Package Prefabs
    ///
    /// Generates ready-to-use gameplay prefabs and saves them into:
    ///   Packages/com.endlessengine.idle/Runtime/Prefabs/
    ///
    /// Prefabs are organized by game type. The New Game Wizard references these
    /// when building scenes, so no code needs to be written per-project.
    ///
    /// Re-run at any time to regenerate or add missing prefabs.
    /// </summary>
    public static class PackagePrefabFactory
    {
        private const string PrefabRoot = "Packages/com.endlessengine.idle/Runtime/Prefabs";

        [MenuItem("Tools/Endless Engine/Create Package Prefabs", priority = 10)]
        public static void CreateAll()
        {
            EnsureDir(PrefabRoot);

            CreateBootstrapPrefabs();
            CreateClickTargetPrefabs();
            CreateHarvestNodePrefabs();
            CreateEnemyPrefab();
            CreateTowerSlotPrefab();
            CreateMergeItemPrefabs();
            CreateFarmPlotPrefab();
            CreateBuildingSlotPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PackagePrefabFactory] All package prefabs created in {PrefabRoot}/");
            EditorUtility.DisplayDialog("Endless Engine",
                $"Package prefabs created!\n\n{PrefabRoot}/", "OK");
        }

        // ── Bootstrap prefab ──────────────────────────────────────────────────────

        private static void CreateBootstrapPrefabs()
        {
            string dir = $"{PrefabRoot}/Bootstrap";
            EnsureDir(dir);

            // AutoSetup Bootstrap GO
            if (!PrefabExists(dir, "AutoSetupBootstrap"))
            {
                var go = new GameObject("AutoSetupBootstrap");
                go.AddComponent<AutoSetupBootstrap>();
                SavePrefab(go, dir, "AutoSetupBootstrap");
                Object.DestroyImmediate(go);
            }
        }

        // ── Click targets (ClickLoop) ─────────────────────────────────────────────

        private static void CreateClickTargetPrefabs()
        {
            string dir = $"{PrefabRoot}/ClickLoop";
            EnsureDir(dir);

            var colorDefs = new (string name, Color color)[]
            {
                ("ClickTarget_Red",   new Color(0.9f, 0.2f, 0.2f)),
                ("ClickTarget_Blue",  new Color(0.2f, 0.5f, 0.9f)),
                ("ClickTarget_Green", new Color(0.2f, 0.9f, 0.4f)),
            };

            foreach (var (name, color) in colorDefs)
            {
                if (PrefabExists(dir, name)) continue;

                var root = new GameObject(name);
                root.transform.localScale = Vector3.one * 1.2f;

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite       = MakeCircle(48, color);
                sr.sortingOrder = 2;

                var col = root.AddComponent<CircleCollider2D>();
                col.radius = 0.5f;

                root.AddComponent<EndlessEngine.ClickLoop.ClickTarget>();

                // Glow ring child
                var glow = new GameObject("Glow");
                glow.transform.SetParent(root.transform, false);
                glow.transform.localScale = Vector3.one * 1.35f;
                var gsr = glow.AddComponent<SpriteRenderer>();
                gsr.sprite       = MakeCircle(48, new Color(color.r, color.g, color.b, 0.18f));
                gsr.sortingOrder = 1;

                // HP bar child
                BuildHPBar(root, new Color(0f, 0.7f, 0f), new Vector3(0, 0.75f, 0), 1f);

                SavePrefab(root, dir, name);
                Object.DestroyImmediate(root);
            }
        }

        // ── Harvest nodes ─────────────────────────────────────────────────────────

        private static void CreateHarvestNodePrefabs()
        {
            string dir = $"{PrefabRoot}/Harvest";
            EnsureDir(dir);

            var colorDefs = new (string name, Color body, Color crown)[]
            {
                ("HarvestNode_Green",  new Color(0.2f, 0.7f, 0.2f), new Color(0.16f, 0.7f, 0.16f)),
                ("HarvestNode_Stone",  new Color(0.55f, 0.5f, 0.45f), new Color(0.45f, 0.4f, 0.35f)),
                ("HarvestNode_Golden", new Color(0.85f, 0.7f, 0.15f), new Color(0.9f, 0.8f, 0.2f)),
            };

            foreach (var (name, body, crown) in colorDefs)
            {
                if (PrefabExists(dir, name)) continue;

                var root = new GameObject(name);

                var bodyGO = new GameObject("Body");
                bodyGO.transform.SetParent(root.transform, false);
                bodyGO.transform.localScale = Vector3.one * 1.1f;
                var bsr = bodyGO.AddComponent<SpriteRenderer>();
                bsr.sprite       = MakeCircle(48, body);
                bsr.sortingOrder = 2;

                var crownGO = new GameObject("Crown");
                crownGO.transform.SetParent(root.transform, false);
                crownGO.transform.localPosition = new Vector3(0, 0.55f, 0);
                crownGO.transform.localScale    = Vector3.one * 0.7f;
                var csr = crownGO.AddComponent<SpriteRenderer>();
                csr.sprite       = MakeCircle(48, crown);
                csr.sortingOrder = 3;

                var col = root.AddComponent<CircleCollider2D>();
                col.radius    = 0.55f;
                col.isTrigger = true;

                root.AddComponent<EndlessEngine.Harvest.HarvestNode>();
                BuildHPBar(root, new Color(0.2f, 0.8f, 0.2f), new Vector3(0, 0.9f, 0), 0.9f);

                SavePrefab(root, dir, name);
                Object.DestroyImmediate(root);
            }
        }

        // ── Enemy prefab (wave combat) ─────────────────────────────────────────────

        private static void CreateEnemyPrefab()
        {
            string dir = $"{PrefabRoot}/Combat";
            EnsureDir(dir);

            if (PrefabExists(dir, "Enemy_Default")) return;

            var go = new GameObject("Enemy_Default");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeCircle(48, new Color(0.9f, 0.15f, 0.15f));
            sr.sortingOrder = 3;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale            = 0;
            rb.freezeRotation          = true;
            rb.collisionDetectionMode  = CollisionDetectionMode2D.Continuous;

            go.AddComponent<CircleCollider2D>();
            BuildHPBar(go, new Color(0.1f, 0.8f, 0.1f), new Vector3(0, 0.7f, 0), 0.8f);

            SavePrefab(go, dir, "Enemy_Default");
            Object.DestroyImmediate(go);
        }

        // ── Tower slot (tower defense) ─────────────────────────────────────────────

        private static void CreateTowerSlotPrefab()
        {
            string dir = $"{PrefabRoot}/TowerDefense";
            EnsureDir(dir);

            if (PrefabExists(dir, "TowerSlot_Default")) return;

            var slot = new GameObject("TowerSlot_Default");
            var sr   = slot.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeRect(new Color(0.3f, 0.25f, 0.1f));
            sr.sortingOrder = 1;
            slot.transform.localScale = Vector3.one * 0.9f;
            slot.AddComponent<BoxCollider2D>();

            var tower = new GameObject("Tower");
            tower.transform.SetParent(slot.transform, false);
            var tsr = tower.AddComponent<SpriteRenderer>();
            tsr.sprite       = MakeCircle(32, new Color(0.5f, 0.5f, 0.55f));
            tsr.sortingOrder = 2;
            tower.transform.localScale = Vector3.one * 0.7f;

            var label = new GameObject("EmptyLabel");
            label.transform.SetParent(slot.transform, false);
            label.transform.localPosition = new Vector3(0, -0.6f, 0);
            label.transform.localScale    = Vector3.one * 0.22f;

            SavePrefab(slot, dir, "TowerSlot_Default");
            Object.DestroyImmediate(slot);
        }

        // ── Merge items ───────────────────────────────────────────────────────────

        private static void CreateMergeItemPrefabs()
        {
            string dir = $"{PrefabRoot}/Merge";
            EnsureDir(dir);

            var tiers = new (string name, Color color, int tier)[]
            {
                ("MergeItem_T1", new Color(0.95f, 0.75f, 0.1f), 1),
                ("MergeItem_T2", new Color(0.9f, 0.5f, 0.1f), 2),
                ("MergeItem_T3", new Color(0.8f, 0.2f, 0.8f), 3),
            };

            foreach (var (name, color, tier) in tiers)
            {
                if (PrefabExists(dir, name)) continue;

                var go = new GameObject(name);
                go.transform.localScale = Vector3.one * 0.65f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = MakeCircle(32, color);
                sr.sortingOrder = 3;
                go.AddComponent<BoxCollider2D>();

                // Tier stored as tag so MergeService can read it at runtime
                go.tag = "Untagged";  // replace with tier tag if project defines them

                SavePrefab(go, dir, name);
                Object.DestroyImmediate(go);
            }
        }

        // ── Farm plot ─────────────────────────────────────────────────────────────

        private static void CreateFarmPlotPrefab()
        {
            string dir = $"{PrefabRoot}/Farm";
            EnsureDir(dir);

            if (PrefabExists(dir, "FarmPlot_Default")) return;

            var plot = new GameObject("FarmPlot_Default");
            plot.transform.localScale = new Vector3(2.5f, 1.3f, 1f);

            var sr = plot.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeRect(new Color(0.3f, 0.5f, 0.2f));
            sr.sortingOrder = 1;
            plot.AddComponent<BoxCollider2D>();

            SavePrefab(plot, dir, "FarmPlot_Default");
            Object.DestroyImmediate(plot);
        }

        // ── Building slot ─────────────────────────────────────────────────────────

        private static void CreateBuildingSlotPrefab()
        {
            string dir = $"{PrefabRoot}/Building";
            EnsureDir(dir);

            if (PrefabExists(dir, "BuildingSlot_Default")) return;

            var b = new GameObject("BuildingSlot_Default");
            b.transform.localScale = new Vector3(2.1f, 2.0f, 1f);

            var sr = b.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeRect(new Color(0.6f, 0.55f, 0.5f));
            sr.sortingOrder = 1;
            b.AddComponent<BoxCollider2D>();
            b.AddComponent<EndlessEngine.Bootstrap.BuildingSlotHandler>();

            SavePrefab(b, dir, "BuildingSlot_Default");
            Object.DestroyImmediate(b);
        }

        // ── HP bar helper ─────────────────────────────────────────────────────────

        private static void BuildHPBar(GameObject root, Color fillColor, Vector3 offset, float width)
        {
            var bar = new GameObject("HPBar");
            bar.transform.SetParent(root.transform, false);
            bar.transform.localPosition = offset;
            bar.transform.localScale    = new Vector3(width, 0.12f, 1f);

            var bgSr = bar.AddComponent<SpriteRenderer>();
            bgSr.sprite       = MakeRect(new Color(0.15f, 0.02f, 0.02f));
            bgSr.sortingOrder = 5;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(bar.transform, false);
            var fsr = fill.AddComponent<SpriteRenderer>();
            fsr.sprite       = MakeRect(fillColor);
            fsr.sortingOrder = 6;
            fill.transform.localScale = Vector3.one;
        }

        // ── Sprite helpers ────────────────────────────────────────────────────────

        private static Sprite MakeCircle(int res, Color c)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float r = res * 0.5f;
            float rSq = (r - 1f) * (r - 1f);
            var px = tex.GetPixels32();
            var cc = ToC32(c);
            var tr = new Color32(0, 0, 0, 0);
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                px[y * res + x] = dx * dx + dy * dy <= rSq ? cc : tr;
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        }

        private static Sprite MakeRect(Color c)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = tex.GetPixels32();
            var cc = ToC32(c);
            for (int i = 0; i < px.Length; i++) px[i] = cc;
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }

        private static Color32 ToC32(Color c) =>
            new Color32((byte)(c.r * 255), (byte)(c.g * 255),
                        (byte)(c.b * 255), (byte)(c.a * 255));

        // ── Asset helpers ─────────────────────────────────────────────────────────

        private static bool PrefabExists(string dir, string name) =>
            File.Exists(Path.Combine(Application.dataPath, "..",
                $"{dir}/{name}.prefab".Replace('/', Path.DirectorySeparatorChar)));

        private static void SavePrefab(GameObject go, string dir, string name)
        {
            string assetPath = $"{dir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, assetPath);
        }

        private static void EnsureDir(string assetPath)
        {
            string full = Path.Combine(Application.dataPath, "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }
    }
}
