namespace EndlessEngine.SaveAndLoad.Migrations
{
    /// <summary>
    /// Schema v3 → v4: adds BigDouble mantissa/exponent save fields.
    ///
    /// Adds CurrentResourcesMantissa and CurrentResourcesExponent to SaveData.
    /// Pre-v4 saves have these as zero — EconomyService.OnAfterLoad falls back to
    /// CurrentResources (double) when exponent is zero, so no data is lost.
    /// </summary>
    public class SaveMigration_V3_V4 : IMigration
    {
        public int FromVersion => 3;
        public int ToVersion   => 4;

        public void Migrate(SaveData data)
        {
            // Fields default to 0 on old saves — EconomyService reads CurrentResources fallback.
            // Nothing to mutate; migration advances the schema version only.
        }
    }
}
