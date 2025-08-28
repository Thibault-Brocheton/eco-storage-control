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
    using Eco.Shared.Localization;
    using Eco.Shared.Utils;
	using System.Linq;
	using System;
    using Eco.Gameplay.Systems.EnvVars;
    using Eco.Gameplay.Civics.GameValues;

    [Serialized, Eco, Localized]
    public enum VisibilityMode { Hidden, Visible }

    [Serialized, Eco, Localized]
    public enum RestrictionMode { Blacklist, Whitelist }

    [Serialized]
    [HasIcon("StorageComponent")]
    [RequireComponent(typeof(StorageComponent))]
    [RequireComponent(typeof(LinkComponent))]
    [CreateComponentTabLoc("Storage Control", true), LocDescription("Customize storage rules."), Priority(PriorityAttribute.VeryLow), Sort(2)]
    public class StorageControlComponent : WorldObjectComponent, IHasEnvVars
    {
        public override WorldObjectComponentClientAvailability Availability => WorldObjectComponentClientAvailability.Always;

        [SyncToView] public override string IconName => "StorageComponent";

        private StorageComponent storageComponent = null!;
        private LinkComponent linkComponent = null!;

        [Eco, Sort(1), LocDescription("Controls whether the listed items are forbidden in this storage (Blacklist) or whether only these items are allowed (Whitelist).")]
        public RestrictionMode RestrictionMode { get; set; } = RestrictionMode.Blacklist;

        [Eco, Sort(2), UITypeName("Selector"), AllowEmpty] public GamePickerList Items { get; set; } = GamePickerListFactory.Create(typeof(Item));

        [Eco, Sort(3), LocDescription("Control whether this storage is Hidden from other storages or Visible to them.")]
        public VisibilityMode Visibility { get; set; } = VisibilityMode.Visible;

        /*[Autogen, RPC, UITypeName("BigButton"), Sort(6)]
        public void Apply(Player player)
        {
            this.Apply();
        }*/

        // UI Type ButtonGrid
        //[Autogen, RPC, UITypeName("BigButton")] public void SortAlphabetically(Player player) => SortAlphabeticallyInternal(this.linkComponent, player);
        //[Autogen, RPC] public void SortByType(Player player) => SortByTypeInternal(this.linkComponent, player);

        private StorageControlRestriction storageControlRestriction;

        public override void Initialize()
        {
            base.Initialize();

            this.storageComponent = this.Parent.GetComponent<StorageComponent>();
            this.linkComponent = this.Parent.GetComponent<LinkComponent>();

            this.linkComponent.OnLinked.Add(_ =>
            {
                if (this.Visibility == VisibilityMode.Hidden)
                {
                    this.linkComponent.Destroy();
                }
            });

            this.Subscribe(nameof(this.Visibility), this.Apply);
            this.Subscribe(nameof(this.RestrictionMode), this.Apply);
            this.Items.Entries.Callbacks.OnChanged.Add(this.Apply);

            this.storageControlRestriction = new StorageControlRestriction(this.RestrictionMode, this.Items.Entries.Select(s => Item.Get((Type)s)).ToArray());
            this.storageComponent.Inventory.AddInvRestriction(this.storageControlRestriction);

            this.Apply();
        }

        private void Apply()
        {
            this.linkComponent.Hidden = this.Visibility == VisibilityMode.Hidden;

            if (this.linkComponent.Hidden)
            {
                this.linkComponent.Destroy();
            }
            else
            {
                this.linkComponent.OnAfterObjectMoved();
            }

            this.storageControlRestriction.UpdateRestrictions(this.RestrictionMode, this.Items.Entries.Select(s => Item.Get((Type)s)).ToArray());
        }

        /*public static void SortAlphabeticallyInternal(LinkComponent link, Player player)
        {
            var linked = link.GetLinkedStoragesWithSettings(player.User).OrderBy(l => l.Storage.Parent.Name).ToList();

            for (var i = 1000; i < linked.Count; i++)
            {
                Log.WriteLineLoc($"ALPHA - Priority of {linked[i].Storage.Parent.Name} is {linked[i].Settings.Priority}");
                link.SetObjectPriority(player.User, linked[i].Storage, i);
            }
        }

        public static void SortByTypeInternal(LinkComponent link, Player player)
        {
            var linked = link.GetLinkedStoragesWithSettings(player.User).OrderBy(l => l.Storage.Parent.TypeID()).ToList();

            for (var i = 1000; i < linked.Count; i++)
            {
                Log.WriteLineLoc($"BY TYPE - Priority of {linked[i].Storage.Parent.Name} is {linked[i].Settings.Priority}");
                link.SetObjectPriority(player.User, linked[i].Storage, i);
            }
        }*/
    }

    public class StorageControlRestriction : InventoryRestriction
    {
        private RestrictionMode _mode;
        private Type[] _items;
        private string _cachedDescription;

        public StorageControlRestriction(RestrictionMode mode, Item[] items)
        {
            this.UpdateRestrictions(mode, items);
        }

        public void UpdateRestrictions(RestrictionMode mode, Item[] items)
        {
            this._mode = mode;
            this._items = items.Select(i => i.GetType()).ToArray();
            this._cachedDescription = items.Any() ? string.Join(", ", items.Take(15).Select(i => i.MarkedUpName)) + (items.Length > 15 ? $"... + {items.Length - 15} items" : "") : "None";
        }

        public override LocString Message => Localizer.Do($"[StorageControl] {this._mode} - {this._cachedDescription}.");
        public override int MaxAccepted(Item item, int currentQuantity) => this._items.Any(x => item.GetType().DerivesFrom(x))
            ? this._mode == RestrictionMode.Whitelist ? -1 : 0
            : this._mode == RestrictionMode.Whitelist ? 0 : -1;
    }
}


