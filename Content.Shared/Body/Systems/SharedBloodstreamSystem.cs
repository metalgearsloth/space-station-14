using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Drunk;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Body.Systems;

public abstract class SharedBloodstreamSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedDrunkSystem _drunkSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly SharedStutteringSystem _stutteringSystem = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodstreamComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BloodstreamComponent, RejuvenateEvent>(OnRejuvenate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BloodstreamComponent>();
        var curTime = Timing.CurTime;

        while (query.MoveNext(out var uid, out var bloodstream))
        {
            if (bloodstream.NextUpdate > curTime)
                continue;

            bloodstream.NextUpdate += TimeSpan.FromSeconds(bloodstream.UpdateInterval);
            Dirty(uid, bloodstream);

            if (!_solutionContainerSystem.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
                continue;

            // Adds blood to their blood level if it is below the maximum; Blood regeneration. Must be alive.
            if (bloodSolution.Volume < bloodSolution.MaxVolume && !_mobStateSystem.IsDead(uid))
            {
                TryModifyBloodLevel(uid, bloodstream.BloodRefreshAmount, bloodstream);
            }

            // Removes blood from the bloodstream based on bleed amount (bleed rate)
            // as well as stop their bleeding to a certain extent.
            if (bloodstream.BleedAmount > 0)
            {
                // Blood is removed from the bloodstream at a 1-1 rate with the bleed amount
                TryModifyBloodLevel(uid, (-bloodstream.BleedAmount), bloodstream);
                // Bleed rate is reduced by the bleed reduction amount in the bloodstream component.
                TryModifyBleedAmount(uid, -bloodstream.BleedReductionAmount, bloodstream);
            }

            // deal bloodloss damage if their blood level is below a threshold.
            var bloodPercentage = GetBloodLevelPercentage(uid, bloodstream);
            if (bloodPercentage < bloodstream.BloodlossThreshold && !_mobStateSystem.IsDead(uid))
            {
                // bloodloss damage is based on the base value, and modified by how low your blood level is.
                var amt = bloodstream.BloodlossDamage / (0.1f + bloodPercentage);

                _damageableSystem.TryChangeDamage(uid, amt, false, false);

                // Apply dizziness as a symptom of bloodloss.
                // The effect is applied in a way that it will never be cleared without being healthy.
                // Multiplying by 2 is arbitrary but works for this case, it just prevents the time from running out
                _drunkSystem.TryApplyDrunkenness(uid, bloodstream.UpdateInterval*2, false);
                _stutteringSystem.DoStutter(uid, TimeSpan.FromSeconds(bloodstream.UpdateInterval*2), false);

                // storing the drunk and stutter time so we can remove it independently from other effects additions
                bloodstream.StatusTime += bloodstream.UpdateInterval * 2;
            }
            else if (!_mobStateSystem.IsDead(uid))
            {
                // If they're healthy, we'll try and heal some bloodloss instead.
                _damageableSystem.TryChangeDamage(uid, bloodstream.BloodlossHealDamage * bloodPercentage, true, false);

                // Remove the drunk effect when healthy. Should only remove the amount of drunk and stutter added by low blood level
                _drunkSystem.TryRemoveDrunkenessTime(uid, bloodstream.StatusTime);
                _stutteringSystem.DoRemoveStutterTime(uid, bloodstream.StatusTime);
                // Reset the drunk and stutter time to zero
                bloodstream.StatusTime = 0;
            }
        }
    }

    private void OnDamageChanged(EntityUid uid, BloodstreamComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta is null)
            return;

        // definitely don't make them bleed if they got healed
        if (!args.DamageIncreased)
            return;

        // TODO probably cache this or something. humans get hurt a lot
        if (!PrototypeManager.TryIndex(component.DamageBleedModifiers, out var modifiers))
            return;

        var bloodloss = DamageSpecifier.ApplyModifierSet(args.DamageDelta, modifiers);

        if (bloodloss.Empty)
            return;

        // Does the calculation of how much bleed rate should be added/removed, then applies it
        var oldBleedAmount = component.BleedAmount;
        var total = bloodloss.GetTotal();
        var totalFloat = total.Float();
        TryModifyBleedAmount(uid, totalFloat, component);

        // Why was this just a doc summary in the middle of the class.
        /*
         * Critical hit. Causes target to lose blood, using the bleed rate modifier of the weapon, currently divided by 5
        * The crit chance is currently the bleed rate modifier divided by 25.
        * Higher damage weapons have a higher chance to crit!
         */
        var prob = Math.Clamp(totalFloat / 25, 0, 1);
        if (totalFloat > 0 && _robustRandom.Prob(prob))
        {
            TryModifyBloodLevel(uid, (-total) / 5, component);
            _audio.PlayPvs(component.InstantBloodSound, uid);
        }

        // Heat damage will cauterize, causing the bleed rate to be reduced.
        else if (totalFloat < 0 && oldBleedAmount > 0)
        {
            // Magically, this damage has healed some bleeding, likely
            // because it's burn damage that cauterized their wounds.

            // We'll play a special sound and popup for feedback.
            _audio.PlayPvs(component.BloodHealedSound, uid);
            _popupSystem.PopupEntity(Loc.GetString("bloodstream-component-wounds-cauterized"), uid,
                uid, PopupType.Medium);
        }
    }

    private void OnRejuvenate(Entity<BloodstreamComponent> entity, ref RejuvenateEvent args)
    {
        TryModifyBleedAmount(entity.Owner, -entity.Comp.BleedAmount, entity.Comp);

        if (_solutionContainerSystem.ResolveSolution(entity.Owner, entity.Comp.BloodSolutionName, ref entity.Comp.BloodSolution, out var bloodSolution))
            TryModifyBloodLevel(entity.Owner, bloodSolution.AvailableVolume, entity.Comp);

        if (_solutionContainerSystem.ResolveSolution(entity.Owner, entity.Comp.ChemicalSolutionName, ref entity.Comp.ChemicalSolution))
            _solutionContainerSystem.RemoveAllSolution(entity.Comp.ChemicalSolution.Value);
    }

    /// <summary>
    ///     Attempt to transfer provided solution to internal solution.
    /// </summary>
    public bool TryAddToChemicals(EntityUid uid, Solution solution, BloodstreamComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (!_solutionContainerSystem.ResolveSolution(uid, component.ChemicalSolutionName, ref component.ChemicalSolution))
            return false;

        return _solutionContainerSystem.TryAddSolution(component.ChemicalSolution.Value, solution);
    }

    public bool FlushChemicals(EntityUid uid, string excludedReagentID, FixedPoint2 quantity, BloodstreamComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (!_solutionContainerSystem.ResolveSolution(uid, component.ChemicalSolutionName, ref component.ChemicalSolution, out var chemSolution))
            return false;

        for (var i = chemSolution.Contents.Count - 1; i >= 0; i--)
        {
            var (reagentId, _) = chemSolution.Contents[i];
            if (reagentId.Prototype != excludedReagentID)
            {
                _solutionContainerSystem.RemoveReagent(component.ChemicalSolution.Value, reagentId, quantity);
            }
        }

        return true;
    }

    public float GetBloodLevelPercentage(EntityUid uid, BloodstreamComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return 0.0f;

        if (!_solutionContainerSystem.ResolveSolution(uid, component.BloodSolutionName, ref component.BloodSolution, out var bloodSolution))
            return 0.0f;

        return bloodSolution.FillFraction;
    }

    public void SetBloodLossThreshold(EntityUid uid, float threshold, BloodstreamComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.BloodlossThreshold = threshold;
        Dirty(uid, comp);
    }

    /// <summary>
    ///     Attempts to modify the blood level of this entity directly.
    /// </summary>
    public virtual bool TryModifyBloodLevel(EntityUid uid, FixedPoint2 amount, BloodstreamComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        if (!_solutionContainerSystem.ResolveSolution(uid, component.BloodSolutionName, ref component.BloodSolution))
            return false;

        if (amount >= 0)
            return _solutionContainerSystem.TryAddReagent(component.BloodSolution.Value, component.BloodReagent, amount, out _);

        // Removal is more involved,
        // since we also wanna handle moving it to the temporary solution
        // and then spilling it if necessary.
        var newSol = _solutionContainerSystem.SplitSolution(component.BloodSolution.Value, -amount);

        if (!_solutionContainerSystem.ResolveSolution(uid, component.BloodTemporarySolutionName, ref component.TemporarySolution, out var tempSolution))
            return true;

        tempSolution.AddSolution(newSol, PrototypeManager);
        _solutionContainerSystem.UpdateChemicals(component.TemporarySolution.Value);

        return true;
    }

    /// <summary>
    ///     Tries to make an entity bleed more or less
    /// </summary>
    public bool TryModifyBleedAmount(EntityUid uid, float amount, BloodstreamComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        component.BleedAmount += amount;
        component.BleedAmount = Math.Clamp(component.BleedAmount, 0, component.MaxBleedAmount);

        if (component.BleedAmount == 0)
            _alertsSystem.ClearAlert(uid, AlertType.Bleed);
        else
        {
            var severity = (short) Math.Clamp(Math.Round(component.BleedAmount, MidpointRounding.ToZero), 0, 10);
            _alertsSystem.ShowAlert(uid, AlertType.Bleed, severity);
        }

        Dirty(uid, component);
        return true;
    }

    /// <summary>
    ///     BLOOD FOR THE BLOOD GOD
    /// </summary>
    public virtual void SpillAllSolutions(EntityUid uid, BloodstreamComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var tempSol = new Solution();

        if (_solutionContainerSystem.ResolveSolution(uid, component.BloodSolutionName, ref component.BloodSolution, out var bloodSolution))
        {
            tempSol.MaxVolume += bloodSolution.MaxVolume;
            tempSol.AddSolution(bloodSolution, PrototypeManager);
            _solutionContainerSystem.RemoveAllSolution(component.BloodSolution.Value);
        }

        if (_solutionContainerSystem.ResolveSolution(uid, component.ChemicalSolutionName, ref component.ChemicalSolution, out var chemSolution))
        {
            tempSol.MaxVolume += chemSolution.MaxVolume;
            tempSol.AddSolution(chemSolution, PrototypeManager);
            _solutionContainerSystem.RemoveAllSolution(component.ChemicalSolution.Value);
        }

        if (_solutionContainerSystem.ResolveSolution(uid, component.BloodTemporarySolutionName, ref component.TemporarySolution, out var tempSolution))
        {
            tempSol.MaxVolume += tempSolution.MaxVolume;
            tempSol.AddSolution(tempSolution, PrototypeManager);
            _solutionContainerSystem.RemoveAllSolution(component.TemporarySolution.Value);
        }
    }

    /// <summary>
    ///     Change what someone's blood is made of, on the fly.
    /// </summary>
    public void ChangeBloodReagent(EntityUid uid, string reagent, BloodstreamComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (reagent == component.BloodReagent)
            return;

        if (!_solutionContainerSystem.ResolveSolution(uid, component.BloodSolutionName, ref component.BloodSolution, out var bloodSolution))
        {
            component.BloodReagent = reagent;
            return;
        }

        var currentVolume = bloodSolution.RemoveReagent(component.BloodReagent, bloodSolution.Volume);

        component.BloodReagent = reagent;

        if (currentVolume > 0)
            _solutionContainerSystem.TryAddReagent(component.BloodSolution.Value, component.BloodReagent, currentVolume, out _);

        Dirty(uid, component);
    }
}
