namespace EndlessEngine.SaveAndLoad.Migrations
{
    /// <summary>
    /// Schema v1 → v2: CurrentResources promoted from long to double.
    ///
    /// v1 saved CurrentResources as long (max ~9.2e18).
    /// v2 saves CurrentResources as double (max ~1.8e308, precise to ~1e15).
    ///
    /// Migration reads LegacyCurrentResources (the old long field Newtonsoft
    /// deserializes from "CurrentResources" in v1 JSON via the rename) and
    /// writes it into the new double CurrentResources field.
    ///
    /// Note on JSON field name: Newtonsoft.Json deserializes the old "CurrentResources"
    /// long value into LegacyCurrentResources via [JsonProperty] aliasing.
    /// New saves write "CurrentResources" as double.
    /// </summary>
    public class SaveMigration_V1_V2 : IMigration
    {
        public int FromVersion => 1;
        public int ToVersion   => 2;

        public void Migrate(SaveData data)
        {
#pragma warning disable CS0618
            if (data.LegacyCurrentResources != 0 && data.CurrentResources == 0)
                data.CurrentResources = (double)data.LegacyCurrentResources;

            data.LegacyCurrentResources = 0;
#pragma warning restore CS0618
        }
    }
}
