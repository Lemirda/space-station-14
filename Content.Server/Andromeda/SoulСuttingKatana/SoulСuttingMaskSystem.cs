using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Content.Shared.Andromeda.SoulСuttingKatana;
using Content.Shared.Inventory;
using Content.Shared.Speech;

namespace Content.Server.Andromeda.SoulСuttingKatana;

public sealed class SoulCuttingMaskSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SoulCuttingMaskComponent, GetVerbsEvent<Verb>>(AddMaskVerbs);
        SubscribeLocalEvent<SoulCuttingUserComponent, RecallSoulCuttingKatanaEvent>(OnRecallKatana);
    }

    private void AddMaskVerbs(EntityUid maskUid, SoulCuttingMaskComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<SoulCuttingUserComponent>(args.User, out var userComp))
            return;

        Verb setHostVerb = new Verb
        {
            Text = "Стать владельцем маски",
            Act = () => SetHost(maskUid, component, args.User),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Andromeda/Lemird/VerbRoboisseur/verbroboisseur.png"))
        };

        if (component.OwnerIdentified)
        {
            args.Verbs.Remove(setHostVerb);
        }
        else
        {
            args.Verbs.Add(setHostVerb);
        }

        if (component.OwnerUid == args.User && component.OwnerIdentified)
        {
            Verb saveVerb = new Verb
            {
                Text = "Зафиксировать маску",
                Act = () => SaveMask(maskUid, component, args.User),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Andromeda/Lemird/VerbRoboisseur/verbroboisseur.png"))
            };

            if (component.MaskSealed)
            {
                Verb removeVerb = new Verb
                {
                    Text = "Снять маску",
                    Act = () => RemoveMask(maskUid, component, args.User),
                    Icon = new SpriteSpecifier.Texture(new("/Textures/Andromeda/Lemird/VerbRoboisseur/verbroboisseur.png"))
                };

                args.Verbs.Remove(saveVerb);
                args.Verbs.Add(removeVerb);
            }
            else
            {
                args.Verbs.Add(saveVerb);
            }
        }
    }

    private void SetHost(EntityUid maskUid, SoulCuttingMaskComponent maskComp, EntityUid ownerUid)
    {
        if (TryComp<SoulCuttingUserComponent>(ownerUid, out var ownerComp))
        {
            ownerComp.MaskUid = maskUid;
            maskComp.OwnerUid = ownerUid;
            maskComp.OwnerIdentified = true;
            _popupSystem.PopupCursor("Вы чувствует как вы стали обладателем могущественной маски...", ownerUid, PopupType.Large);
        }
    }

    private void SaveMask(EntityUid maskUid, SoulCuttingMaskComponent maskComp, EntityUid ownerUid)
    {
        if (TryComp<SoulCuttingUserComponent>(ownerUid, out var ownerComp))
        {
            if (!_inventorySystem.TryGetSlotEntity(ownerUid, "mask", out var slotEntity))
            {
                _popupSystem.PopupCursor("Маска не находится на лице.", ownerUid, PopupType.Large);
                return;
            }

            if (TryComp<SpeechComponent>(ownerUid, out var speechComp))
            {
                maskComp.OriginalSpeechSounds = speechComp.SpeechSounds;

                speechComp.SpeechSounds = "SoulCuttingSpech";
                Dirty(ownerUid, speechComp);
            }

            maskComp.MaskSealed = true;

            AddComp<UnremoveableComponent>(maskUid);
            _popupSystem.PopupCursor("Вы чувствует как маска наполняется энергией и запечатывается...", ownerComp.OwnerUid, PopupType.Large);
            _actionSystem.AddAction(ownerUid, ref maskComp.RecallKatanaActionSoulCuttingEntity, maskComp.RecallKatanaSoulCuttingAction);
        }
    }

    private void RemoveMask(EntityUid maskUid, SoulCuttingMaskComponent maskComp, EntityUid ownerUid)
    {
        if (maskComp.MaskSealed)
        {
            if (TryComp<SpeechComponent>(ownerUid, out var speechComp) && maskComp.OriginalSpeechSounds.HasValue)
            {
                speechComp.SpeechSounds = maskComp.OriginalSpeechSounds;
                Dirty(ownerUid, speechComp);
            }

            maskComp.MaskSealed = false;

            RemComp<UnremoveableComponent>(maskUid);
            _actionSystem.RemoveAction(ownerUid, maskComp.RecallKatanaActionSoulCuttingEntity);
        }
    }

    private void OnRecallKatana(EntityUid ownerUid, SoulCuttingUserComponent component, RecallSoulCuttingKatanaEvent args)
    {
        if (TryComp<SoulCuttingUserComponent>(ownerUid, out var ownerComp) && TryComp<SoulCuttingMaskComponent>(ownerComp.MaskUid, out var maskComp))
        {
            if (component.KatanaUid == null)
            {
                Log.Error($"Katana Uid не найден");
                return;
            }

            var user = args.Performer;
            var katana = component.KatanaUid.Value;

            if (maskComp.MaskSealed)
            {
                _hands.TryPickupAnyHand(user, katana);
                _popupSystem.PopupEntity("Катана появляется в руках.", user, user);
                args.Handled = true;
            }
            else
            {
                _popupSystem.PopupEntity("Маска не зафиксированна.", user, user);
            }
        }
        else
        {
            Log.Error($"Не удалось извлечь SoulCuttingUserComponent и SoulCuttingMaskComponent");
        }
    }
}