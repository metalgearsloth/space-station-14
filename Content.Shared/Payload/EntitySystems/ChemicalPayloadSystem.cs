using Content.Shared.Containers.ItemSlots;
using Content.Shared.Payload.Components;
using Robust.Shared.Containers;

namespace Content.Shared.Payload.EntitySystems;

public sealed class ChemicalPayloadSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChemicalPayloadComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ChemicalPayloadComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<ChemicalPayloadComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<ChemicalPayloadComponent, EntRemovedFromContainerMessage>(OnContainerModified);
    }

    private void OnContainerModified(EntityUid uid, ChemicalPayloadComponent component, ContainerModifiedMessage args)
    {
        UpdateAppearance(uid, component);
    }

    private void UpdateAppearance(EntityUid uid, ChemicalPayloadComponent? component = null, AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref component, ref appearance, false))
            return;

        var filled = ChemicalPayloadFilledSlots.None;

        if (component.BeakerSlotA.HasItem)
            filled |= ChemicalPayloadFilledSlots.Left;

        if (component.BeakerSlotB.HasItem)
            filled |= ChemicalPayloadFilledSlots.Right;

        _appearance.SetData(uid, ChemicalPayloadVisuals.Slots, filled, appearance);
    }

    private void OnComponentInit(EntityUid uid, ChemicalPayloadComponent payload, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, "BeakerSlotA", payload.BeakerSlotA);
        _itemSlotsSystem.AddItemSlot(uid, "BeakerSlotB", payload.BeakerSlotB);
    }

    private void OnComponentShutdown(EntityUid uid, ChemicalPayloadComponent payload, ComponentShutdown args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, payload.BeakerSlotA);
        _itemSlotsSystem.RemoveItemSlot(uid, payload.BeakerSlotB);
    }
}
