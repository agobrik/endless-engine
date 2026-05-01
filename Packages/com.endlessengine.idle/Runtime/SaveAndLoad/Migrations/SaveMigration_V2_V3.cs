namespace EndlessEngine.SaveAndLoad.Migrations
{
    /// <summary>
    /// Schema v2 → v3: adds NumberBackendName metadata field.
    ///
    /// No game-data values change — CurrentResources (double) is identical on-wire for
    /// both DoubleNumber and BigDouble backends. This migration stamps "DoubleNumber" as
    /// the assumed backend on all pre-v3 saves so the field is always populated.
    /// </summary>
    public class SaveMigration_V2_V3 : IMigration
    {
        public int FromVersion => 2;
        public int ToVersion   => 3;

        public void Migrate(SaveData data)
        {
            if (string.IsNullOrEmpty(data.NumberBackendName))
                data.NumberBackendName = "DoubleNumber";
        }
    }
}
