namespace EndlessEngine.SaveAndLoad
{
    /// <summary>
    /// Implemented by every system that owns a slice of <see cref="SaveData"/>.
    /// SaveService calls <see cref="OnBeforeSave"/> (pull pattern — not event/push)
    /// and <see cref="OnAfterLoad"/> in <see cref="ProviderOrder"/> order.
    ///
    /// Registration: call <c>SaveService.RegisterStateProvider(this)</c> in <c>Start()</c>.
    ///
    /// Systems that do NOT own save state must NOT implement this interface:
    /// DamageSystem, VFXFeedbackSystem, AudioService, HUDController (ADR-0004).
    ///
    /// ADR: ADR-0004 — ISaveStateProvider Pull-Based Save Collection
    /// </summary>
    public interface ISaveStateProvider
    {
        /// <summary>
        /// Called before each save. Write runtime state into <paramref name="saveData"/>.
        /// Must complete in &lt; 1ms — called on the main thread before background I/O.
        /// </summary>
        void OnBeforeSave(SaveData saveData);

        /// <summary>
        /// Called after load completes. Initialize runtime state from <paramref name="saveData"/>.
        /// Must complete in &lt; 1ms.
        /// </summary>
        void OnAfterLoad(SaveData saveData);

        /// <summary>
        /// Determines call order. Use <see cref="SaveConstants.SaveProviderOrder"/> constants.
        /// Lower order = called earlier.
        /// </summary>
        int ProviderOrder { get; }
    }
}
