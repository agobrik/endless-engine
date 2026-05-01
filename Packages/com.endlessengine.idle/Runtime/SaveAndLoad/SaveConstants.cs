namespace EndlessEngine.SaveAndLoad
{
    /// <summary>
    /// File name constants for the save system. Single source of truth — no magic
    /// strings in SaveService or tests.
    ///
    /// ADR: ADR-0002 — Save Serialization Format and Atomic Write Pattern
    /// </summary>
    public static class SaveConstants
    {
        /// <summary>Primary save file name (JSON).</summary>
        public const string PrimaryFile = "save_slot_0.json";

        /// <summary>Temporary file written during atomic write sequence.</summary>
        public const string TempFile = "save_slot_0.tmp";

        /// <summary>Last-known-good backup copied before rename step.</summary>
        public const string BackupFile = "save_slot_0.bak";

        /// <summary>Diagnostic log for support tickets on corruption.</summary>
        public const string DiagnosticsFile = "diagnostics.log";

        /// <summary>HMAC-SHA256 sidecar signature for the primary save file.</summary>
        public const string SignatureFile = "save_slot_0.sig";

        /// <summary>HMAC-SHA256 sidecar signature for the backup save file.</summary>
        public const string BackupSignatureFile = "save_slot_0.bak.sig";

        /// <summary>Provider order constants for ISaveStateProvider.ProviderOrder.</summary>
        public static class SaveProviderOrder
        {
            /// <summary>Economy provider writes CurrentResources (primary gold).</summary>
            public const int Economy = 10;

            /// <summary>CurrencyService provider writes CurrencyBalances (secondary currencies).</summary>
            public const int Currency = 15;

            /// <summary>Upgrade tree provider writes UpgradeNodeStates.</summary>
            public const int UpgradeTree = 20;

            /// <summary>Prestige provider writes PrestigeCount, BaseMultiplierPerPrestige.</summary>
            public const int Prestige = 30;

            /// <summary>Wave and combat provider writes WaveNumber, CurrentRunState.</summary>
            public const int WaveAndCombat = 40;

            /// <summary>Generator provider writes GeneratorStates.</summary>
            public const int Generator = 50;

            /// <summary>Click module provider writes ClickState (TotalClickEarned, AutoClickRateOverride).</summary>
            public const int Click = 60;

            /// <summary>Zone module provider writes ZoneStates.</summary>
            public const int Zone = 70;

            /// <summary>Milestone tracker provider writes CompletedMilestones.</summary>
            public const int Milestone = 80;

            /// <summary>Statistics service writes StatisticsValues (lifetime counters + peaks).</summary>
            public const int Statistics = 85;

            /// <summary>Research service writes CompletedResearchNodes, ResearchQueue, ResearchActiveTicks.</summary>
            public const int Research = 90;

            /// <summary>Ascension state manager writes AscensionCounts.</summary>
            public const int Ascension = 25;

            /// <summary>Inventory service writes InventoryItems.</summary>
            public const int Inventory = 35;

            /// <summary>Skill tree service writes UnlockedSkillNodes and SkillPoints.</summary>
            public const int SkillTree = 45;

            /// <summary>Building service writes PlacedBuildings.</summary>
            public const int Building = 55;

            /// <summary>Pet service writes EquippedPetId and PetLevels.</summary>
            public const int Pet = 65;

            /// <summary>Unlock log service writes UnlockLogEntries.</summary>
            public const int UnlockLog = 75;

            /// <summary>Harvest loop service writes HarvestState (node respawn timers + lifetime stats).</summary>
            public const int Harvest = 88;

            /// <summary>Click loop service writes ClickLoopState (target respawn timers + lifetime stats).</summary>
            public const int ClickLoop = 89;
        }
    }
}
