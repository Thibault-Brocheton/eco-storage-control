namespace CavRn.StorageControl
{
    using Eco.Gameplay.Components;
    using Eco.Gameplay.Components.Storage;
    using Eco.Core.Controller;
    using Eco.Gameplay.Objects;
    using Eco.Gameplay.Players;
    using Eco.Shared.Networking;
    using Eco.Shared.Serialization;
    using Eco.Core.Utils;
    using Eco.Gameplay.Items;
    using Eco.Gameplay.Items.SearchAndSelect;
    using Eco.Shared.Localization;
    using Eco.Shared.Utils;
	using System.Collections.Generic;
    using Eco.Shared.Logging;
	using System.Linq;
	using System;
    using Eco.Gameplay.Systems.EnvVars;
    using Eco.Gameplay.Utils;
    
    [Serialized]
    [HasIcon("StorageComponent")]
    [RequireComponent(typeof(StorageComponent))]
    [RequireComponent(typeof(LinkComponent))]
    [CreateComponentTabLoc("Storage Control", true), LocDescription("Customize storage rules."), Priority(1)]
    public class StorageControlComponent : WorldObjectComponent, IHasEnvVars
    {
        public override WorldObjectComponentClientAvailability Availability => WorldObjectComponentClientAvailability.Always;

        [SyncToView] public override string IconName => "StorageComponent";
        
        private StorageComponent storageComponent;
        private LinkComponent linkComponent;

        // UI Type ButtonGrid
        //[Autogen, RPC, UITypeName("BigButton")] public void SortAlphabetically(Player player) => SortAlphabeticallyInternal(this.linkComponent, player);
        //[Autogen, RPC] public void SortByType(Player player) => SortByTypeInternal(this.linkComponent, player);

        [Eco, UITypeName("ItemChoice")] public ThreadSafeHashSet<Item> ItemChoices { get; set; }
        [Eco] public ThreadSafeHashSet<Item> ItemChoices2 { get; set; }

        [Eco] public bool HideFromOtherStorages { get; set; }

        /*[Autogen, RPC, UITypeName("BigButton")]
        public void AddRestrictedItem(Player player)
        {
            var i = new SearchAndSelectItem("", "", true);
            this.RestrictToItems.Stacks.Add(i);
            i.OnClick(player);
        }*/

        [Serialized, Autogen, AutoRPC, SyncToView]
        public SearchAndSelectInventory RestrictToItems { get; private set; } = new SearchAndSelectInventory(10000, string.Empty, Localizer.DoStr("Choose items to restrict the storage to"), true);

        /*[Autogen, RPC, UITypeName("BigButton")]
        public void AddForbiddenItem(Player player)
        {
            var i = new SearchAndSelectItem("", "", true);
            this.ForbiddenItems.Stacks.Add(i);
            i.OnClick(player);
        }*/
        
        [Serialized, Autogen, AutoRPC, SyncToView]
        public SearchAndSelectInventory ForbiddenItems { get; private set; } = new SearchAndSelectInventory(10000, string.Empty, Localizer.DoStr("Choose items to forbid in this storage"), true);
        
        private List<InventoryRestriction> _savedRestrictions = new List<InventoryRestriction>();
        
        public override void Initialize()
        {
            base.Initialize();
            this.storageComponent = this.Parent.GetComponent<StorageComponent>();
            this.linkComponent = this.Parent.GetComponent<LinkComponent>();
            this.linkComponent.Hidden = this.HideFromOtherStorages;
            
            this.Subscribe(nameof(this.HideFromOtherStorages), () =>
            {
                Log.WriteLineLoc($"lala {this.HideFromOtherStorages}");
                this.linkComponent.Hidden = this.HideFromOtherStorages;
            });
            
            this._savedRestrictions = this.storageComponent.Inventory.Restrictions.ToList();

            this.RestrictToItems.OnSelectionChanged.Add(this.RefreshRestrictions);
            this.ForbiddenItems.OnSelectionChanged.Add(this.RefreshRestrictions);

            this.RefreshRestrictions();
        }

        public void RefreshRestrictions()
        {
            this.storageComponent.Inventory.ClearRestrictions();
            this.storageComponent.Inventory.AddInvRestrictions(this._savedRestrictions);

            if (this.RestrictToItems.GetSelection().Any())
            {
                this.storageComponent.Inventory.AddInvRestriction(
                    new ItemTypeLimiterRestriction(
                        this.RestrictToItems.GetSelection().Where(s => s.Item is not null).Select(s => s.Item!.Type).ToArray(),
                        new LocString("items defined in StorageControl Component")
                    )
                );
            }
            
            if (this.ForbiddenItems.GetSelection().Any())
            {
                this.storageComponent.Inventory.AddInvRestriction(
                    new ForbiddenItemTypeRestriction(
                        this.ForbiddenItems.GetSelection().Where(s => s.Item is not null).Select(s => s.Item!.Type).ToArray(),
                        new LocString("items defined in StorageControl Component")
                    )
                );
            }
        }

        public static void SortAlphabeticallyInternal(LinkComponent link, Player player)
        {
            var linked = link.GetLinkedStoragesWithSettings(player.User).OrderBy(l => l.Storage.Parent.Name).ToList();

            for (var i = 0; i < linked.Count; i++)
            {
                Log.WriteLineLoc($"ALPHA - Priority of {linked[i].Storage.Parent.Name} is {linked[i].Settings.Priority}");
                link.SetObjectPriority(player.User, linked[i].Storage, i);
            }
        }
        
        public static void SortByTypeInternal(LinkComponent link, Player player)
        {
            var linked = link.GetLinkedStoragesWithSettings(player.User).OrderBy(l => l.Storage.Parent.TypeID()).ToList();

            for (var i = 0; i < linked.Count; i++)
            {
                Log.WriteLineLoc($"BY TYPE - Priority of {linked[i].Storage.Parent.Name} is {linked[i].Settings.Priority}");
                link.SetObjectPriority(player.User, linked[i].Storage, i);
            }
        }
    }
    
    public class ForbiddenItemTypeRestriction : InventoryRestriction
    {
        private readonly Type[] _types;
        private readonly LocString _descriptor;

        public ForbiddenItemTypeRestriction(Type[] types, LocString descriptor)
        {
            this._types = types;
            this._descriptor = descriptor;
        }

        public override LocString Message => Localizer.Do($"Inventory doesn't accepts {this._descriptor}.");
        public override int MaxAccepted(Item item, int currentQuantity) => this._types.Any(x=>item.GetType().DerivesFrom(x)) ? 0 : -1;
    }
}


