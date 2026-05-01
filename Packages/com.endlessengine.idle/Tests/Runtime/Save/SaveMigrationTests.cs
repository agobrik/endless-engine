using System.Collections.Generic;
using NUnit.Framework;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.SaveAndLoad.Migrations;

namespace EndlessEngine.Tests.Save
{
    /// <summary>
    /// Integration tests for the MigrationPipeline and each registered IMigration.
    ///
    /// Gate rule: any new schema bump MUST ship with a corresponding test class in
    /// this file that verifies the migration on a V(n-1) fixture before merging.
    ///
    /// Test fixture JSON equivalents are constructed in-code so there is no
    /// dependency on file I/O or Resources; the tests run in EditMode with no scene.
    /// </summary>
    [TestFixture]
    public class SaveMigrationTests
    {
        // ── MigrationPipeline infrastructure ─────────────────────────────────────

        [Test]
        public void IsSaveCompatible_ReturnsTrue_WhenVersionInRange()
        {
            Assert.IsTrue(MigrationPipeline.IsSaveCompatible(1, 1, 2));
            Assert.IsTrue(MigrationPipeline.IsSaveCompatible(2, 1, 2));
        }

        [Test]
        public void IsSaveCompatible_ReturnsFalse_WhenBelowMin()
        {
            Assert.IsFalse(MigrationPipeline.IsSaveCompatible(0, 1, 2));
        }

        [Test]
        public void IsSaveCompatible_ReturnsFalse_WhenAboveCurrent()
        {
            Assert.IsFalse(MigrationPipeline.IsSaveCompatible(3, 1, 2));
        }

        [Test]
        public void Apply_DoesNothing_WhenAlreadyAtTarget()
        {
            var pipeline = BuildPipeline();
            var data = V3Fixture();
            pipeline.Apply(data, 3);
            Assert.AreEqual(3, data.SchemaVersion);
        }

        [Test]
        public void Apply_ThrowsMissingMigrationException_WhenNoStepRegistered()
        {
            var pipeline = new MigrationPipeline(System.Array.Empty<IMigration>());
            var data = V1Fixture(legacyGold: 1000L);
            Assert.Throws<MissingMigrationException>(() => pipeline.Apply(data, 2));
        }

        [Test]
        public void Apply_ChainsMultipleMigrations_InOrder()
        {
            // Two-step chain: v0 → v1 → v2
            // Step v0→v1: sets WaveNumber to 1 if zero.
            // Step v1→v2: migrates CurrentResources from long to double.
            var step01 = new StubMigration_V0_V1();
            var step12 = new SaveMigration_V1_V2();
            var pipeline = new MigrationPipeline(new IMigration[] { step12, step01 }); // intentionally out-of-order

            var data = new SaveData { SchemaVersion = 0 };
#pragma warning disable CS0618
            data.LegacyCurrentResources = 999L;
#pragma warning restore CS0618

            pipeline.Apply(data, 2);

            Assert.AreEqual(2,     data.SchemaVersion, "Schema should reach v2 after two-step chain.");
            Assert.AreEqual(1,     data.WaveNumber,    "StubMigration_V0_V1 should have set WaveNumber=1.");
            Assert.AreEqual(999.0, data.CurrentResources, 1e-9, "V1→V2 migration should have copied legacy gold.");
        }

        // ── V1 → V2: CurrentResources long → double ───────────────────────────────

        [Test]
        public void V1toV2_MigratesCurrentResources_FromLegacyLong()
        {
            var data = V1Fixture(legacyGold: 1_234_567L);
            var pipeline = BuildPipeline();

            pipeline.Apply(data, 2);

            Assert.AreEqual(2,           data.SchemaVersion);
            Assert.AreEqual(1_234_567.0, data.CurrentResources, 1e-6,
                "CurrentResources (double) should equal LegacyCurrentResources after migration.");
        }

        [Test]
        public void V1toV2_ClearsLegacyField_AfterMigration()
        {
            var data = V1Fixture(legacyGold: 42L);
            BuildPipeline().Apply(data, 2);

#pragma warning disable CS0618
            Assert.AreEqual(0L, data.LegacyCurrentResources, "LegacyCurrentResources must be zeroed after migration.");
#pragma warning restore CS0618
        }

        [Test]
        public void V1toV2_DoesNotOverwrite_WhenCurrentResourcesAlreadySet()
        {
            // If someone somehow loaded a v1 JSON that already had a non-zero CurrentResources double,
            // the migration must not clobber it.
            var data = V1Fixture(legacyGold: 100L);
            data.CurrentResources = 500.0; // already set
            BuildPipeline().Apply(data, 2);

            Assert.AreEqual(500.0, data.CurrentResources, 1e-9,
                "Must not overwrite CurrentResources when it is already non-zero.");
        }

        [Test]
        public void V1toV2_HandlesZeroLegacyGold()
        {
            var data = V1Fixture(legacyGold: 0L);
            BuildPipeline().Apply(data, 2);

            Assert.AreEqual(0.0, data.CurrentResources, 1e-9, "Zero legacy gold should produce zero CurrentResources.");
            Assert.AreEqual(2,   data.SchemaVersion);
        }

        [Test]
        public void V1toV2_HandlesMaxLong()
        {
            var data = V1Fixture(legacyGold: long.MaxValue);
            BuildPipeline().Apply(data, 2);

            // long.MaxValue = 9_223_372_036_854_775_807 ≈ 9.22e18
            // (double) cast is lossy beyond 2^53 but must not throw.
            Assert.AreEqual((double)long.MaxValue, data.CurrentResources, 1.0,
                "Large long values must survive the cast without throwing.");
        }

        // ── V2 → V3: NumberBackendName metadata stamp ────────────────────────────

        [Test]
        public void V2toV3_StampsDoubleNumber_WhenBackendNameAbsent()
        {
            var data = V2Fixture();
            Assert.IsNull(data.NumberBackendName, "Pre-condition: no backend name on v2 fixture.");

            BuildPipeline().Apply(data, 3);

            Assert.AreEqual(3, data.SchemaVersion);
            Assert.AreEqual("DoubleNumber", data.NumberBackendName,
                "V2→V3 migration must stamp 'DoubleNumber' when field was absent.");
        }

        [Test]
        public void V2toV3_DoesNotOverwrite_WhenBackendNameAlreadySet()
        {
            var data = V2Fixture();
            data.NumberBackendName = "BigDouble";

            BuildPipeline().Apply(data, 3);

            Assert.AreEqual("BigDouble", data.NumberBackendName,
                "V2→V3 must not overwrite a backend name that was already set.");
        }

        [Test]
        public void V1toV3_FullMigrationChain_Works()
        {
            var data = V1Fixture(legacyGold: 5000L);

            BuildPipeline().Apply(data, 3);

            Assert.AreEqual(3,      data.SchemaVersion);
            Assert.AreEqual(5000.0, data.CurrentResources, 1e-6);
            Assert.AreEqual("DoubleNumber", data.NumberBackendName);
        }

        // ── Idempotency ───────────────────────────────────────────────────────────

        [Test]
        public void Pipeline_IsIdempotent_WhenAppliedTwice()
        {
            var data = V1Fixture(legacyGold: 777L);
            var pipeline = BuildPipeline();

            pipeline.Apply(data, 3);
            double firstResult = data.CurrentResources;

            // Second apply should be a no-op (data.SchemaVersion == targetVersion)
            pipeline.Apply(data, 3);

            Assert.AreEqual(firstResult, data.CurrentResources, 1e-9, "Double-apply must be idempotent.");
        }

        // ── SaveSigner round-trip ─────────────────────────────────────────────────

        [Test]
        public void SaveSigner_Sign_ThenVerify_ReturnsTrue()
        {
            const string json = "{\"SchemaVersion\":2,\"CurrentResources\":1000.0}";
            string sig = SaveSigner.Sign(json);
            Assert.IsTrue(SaveSigner.Verify(json, sig), "Signature produced by Sign() must be verified by Verify().");
        }

        [Test]
        public void SaveSigner_Verify_ReturnsFalse_WhenJsonTampered()
        {
            const string json         = "{\"SchemaVersion\":2,\"CurrentResources\":1000.0}";
            const string tamperedJson = "{\"SchemaVersion\":2,\"CurrentResources\":9999999.0}";
            string sig = SaveSigner.Sign(json);
            Assert.IsFalse(SaveSigner.Verify(tamperedJson, sig), "Tampered JSON must fail signature verification.");
        }

        [Test]
        public void SaveSigner_Verify_ReturnsFalse_ForEmptyInputs()
        {
            Assert.IsFalse(SaveSigner.Verify(null,   "abc"));
            Assert.IsFalse(SaveSigner.Verify("json", null));
            Assert.IsFalse(SaveSigner.Verify("",     ""));
        }

        [Test]
        public void SaveSigner_Verify_IsDeterministic()
        {
            const string json = "{\"CurrentResources\":42.0}";
            string sig1 = SaveSigner.Sign(json);
            string sig2 = SaveSigner.Sign(json);
            Assert.AreEqual(sig1, sig2, "Sign() must be deterministic for the same input.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static MigrationPipeline BuildPipeline()
            => new MigrationPipeline(new IMigration[] { new SaveMigration_V1_V2(), new SaveMigration_V2_V3() });

        private static SaveData V1Fixture(long legacyGold)
        {
            var d = new SaveData { SchemaVersion = 1 };
#pragma warning disable CS0618
            d.LegacyCurrentResources = legacyGold;
#pragma warning restore CS0618
            return d;
        }

        private static SaveData V2Fixture()
            => new SaveData { SchemaVersion = 2, CurrentResources = 100.0 };

        private static SaveData V3Fixture()
            => new SaveData { SchemaVersion = 3, CurrentResources = 100.0, NumberBackendName = "DoubleNumber" };

        // Stub migration for multi-step chain test — not part of production code.
        private class StubMigration_V0_V1 : IMigration
        {
            public int FromVersion => 0;
            public int ToVersion   => 1;
            public void Migrate(SaveData data)
            {
                if (data.WaveNumber <= 0) data.WaveNumber = 1;
            }
        }
    }
}
