namespace CavRn.StorageControl
{
    using Eco.Core.Plugins.Interfaces;

    public class StorageControlMod: IModInit
    {
        public static ModRegistration Register() => new()
        {
            ModName = "StorageControl",
            ModDescription = "Take full control of your storages: choose what they accept or forbid, hide them from other storages, ...",
            ModDisplayName = "Storage Control"
        };
    }
}
