using System;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Marks a ScriptableObject field or property as non-overridable.
    /// The <c>EndlessEngine.Analyzers.NonOverridableFieldAnalyzer</c> will emit
    /// compile error ENDLESSENG002 if any subclass redeclares a member with this attribute.
    ///
    /// Usage: place on base SO fields that must not be shadowed by subclasses.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class NonOverridableAttribute : Attribute { }
}
