using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace SharedServices.Auditing
{
    /// <summary>Recognises a save that keeps a record's row and only marks it as deleted.</summary>
    internal static class SoftDelete
    {
        private const string Flag = "IsDeleted";

        /// <summary>True when this change turns the record's IsDeleted flag on.</summary>
        public static bool IsBeingDeleted(EntityEntry entry)
        {
            if (entry.State != EntityState.Modified || entry.Metadata.FindProperty(Flag)?.ClrType != typeof(bool))
                return false;
            var flag = entry.Property(Flag);
            return flag.OriginalValue is false && flag.CurrentValue is true;
        }
    }
}
