namespace CavRn.StorageControl
{
	using System.Linq;
	using System;
    using Eco.Core.Controller;
    using Eco.Core.Utils;
    using Eco.Gameplay.Civics.GameValues;
    using Eco.Gameplay.Components.Storage;
    using Eco.Gameplay.Components;
    using Eco.Gameplay.Items;
    using Eco.Gameplay.Objects;
    using Eco.Shared.Localization;
    using Eco.Shared.Logging;
    using Eco.Shared.Networking;
    using Eco.Shared.Serialization;
    using Eco.Shared.Utils;
    using System.Collections.Generic;
    using System.Collections;
    using System.Reflection;

    [Serialized, Eco, Localized]
    public enum VisibilityMode { Hidden, Visible }

    [Serialized, Eco, Localized]
    public enum RestrictionMode { Blacklist, Whitelist }

    [Serialized]
    [HasIcon("StorageComponent")]
    [RequireComponent(typeof(LinkComponent))]
    [CreateComponentTabLoc("Storage Control", true), LocDescription("Customize storage rules."), Priority(PriorityAttribute.VeryLow)]
    public class StorageControlComponent : WorldObjectComponent
    {
        public override WorldObjectComponentClientAvailability Availability => WorldObjectComponentClientAvailability.Always;
        [SyncToView] public override string IconName => "StorageComponent";

        private StorageComponent? storageComponent;
        private LinkComponent linkComponent;
        private StorageControlRestriction? storageControlRestriction;

        [Eco, Sort(1), LocDescription("Controls whether the listed items are forbidden in this storage (Blacklist) or whether only these items are allowed (Whitelist).")]
        public RestrictionMode RestrictionMode { get; set; } = RestrictionMode.Blacklist;

        [Eco, Sort(2), UITypeName("Selector"), AllowEmpty] public GamePickerList Items { get; set; } = GamePickerListFactory.Create(typeof(Item));

        [Eco, Sort(3), LocDescription("Control whether this storage is Hidden from other storages or Visible to them.")]
        public VisibilityMode Visibility { get; set; } = VisibilityMode.Visible;

        // UI Type ButtonGrid
        //[Autogen, RPC, UITypeName("BigButton")] public void SortAlphabetically(Player player) => SortAlphabeticallyInternal(this.linkComponent, player);
        //[Autogen, RPC] public void SortByType(Player player) => SortByTypeInternal(this.linkComponent, player);

        public override void Initialize()
        {
            base.Initialize();

            this.linkComponent = this.Parent.GetComponent<LinkComponent>();
            this.Subscribe(nameof(this.Visibility), this.Apply);

            this.storageComponent = this.Parent.GetComponent<StorageComponent>();
            if (this.storageComponent is not null)
            {
                this.Subscribe(nameof(this.RestrictionMode), this.Apply);
                this.Items.Entries.Callbacks.OnChanged.Add(this.Apply);
                this.storageControlRestriction = new StorageControlRestriction(this.RestrictionMode, this.Items.Entries.Select(s => Item.Get((Type)s)).ToArray());
                this.storageComponent.Inventory.AddInvRestriction(this.storageControlRestriction);
            }
            else
            {
                Log.WriteErrorLineLoc($"{this.Parent.Name} has no storage component but has StorageControlComponent.");
            }

            this.Apply();
        }

        public override void Destroy()
        {
            this.Unsubscribe(nameof(this.Visibility), this.Apply);
            this.Unsubscribe(nameof(this.RestrictionMode), this.Apply);
            this.Items.Entries.Callbacks.OnChanged.Remove(this.Apply);
            base.Destroy();
        }

        // Allows to access ConcurrentHashSet without accessing it because its assembly is not available in the eco .cs build
        private static IEnumerable<LinkComponent> RawLinked(LinkComponent link)
        {
            var prop = typeof(LinkComponent).GetProperty("LinkedObjects",
                BindingFlags.Instance | BindingFlags.Public);
            var enumerable = (IEnumerable)prop?.GetValue(link)!;
            foreach (var x in enumerable) yield return (LinkComponent)x;
        }

        private void Apply()
        {
            var newVal = this.Visibility == VisibilityMode.Hidden;

            if (this.linkComponent.Hidden != newVal)
            {
                this.linkComponent.Hidden = newVal;
                // Force call to private function NotifyChanged
                typeof(LinkComponent).GetMethod("NotifyChanged", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(this.linkComponent, null);

                // Force call to private function NotifyChanged to all linkedObjects of our linkComponent
                foreach (var linkedComponents in RawLinked(this.linkComponent))
                {
                    typeof(LinkComponent).GetMethod("NotifyChanged", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(linkedComponents, null);
                }
            }

            if (this.storageComponent is not null)
            {
                this.storageControlRestriction!.UpdateRestrictions(this.RestrictionMode, this.Items.Entries.Select(s => Item.Get((Type)s)).ToArray());
            }
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


