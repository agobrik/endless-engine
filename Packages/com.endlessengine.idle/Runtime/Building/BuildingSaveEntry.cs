namespace EndlessEngine.Building
{
    /// <summary>
    /// Serializable snapshot of a BuildingInstance for SaveData.
    /// </summary>
    [System.Serializable]
    public class BuildingSaveEntry
    {
        public string BuildingId;
        public int    GridX;
        public int    GridY;
        public int    UpgradeTier;
    }
}
