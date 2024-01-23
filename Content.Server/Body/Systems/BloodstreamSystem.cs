using Content.Server.Body.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Chemistry.ReactionEffects;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Forensics;
using Content.Server.HealthExaminable;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;

namespace Content.Server.Body.Systems;

public sealed class BloodstreamSystem : SharedBloodstreamSystem
{
    [Dependency] private readonly ForensicsSystem _forensicsSystem = default!;
    [Dependency] private readonly PuddleSystem _puddleSystem = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodstreamComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BloodstreamComponent, HealthBeingExaminedEvent>(OnHealthBeingExamined);
        SubscribeLocalEvent<BloodstreamComponent, BeingGibbedEvent>(OnBeingGibbed);
        SubscribeLocalEvent<BloodstreamComponent, ApplyMetabolicMultiplierEvent>(OnApplyMetabolicMultiplier);
        SubscribeLocalEvent<BloodstreamComponent, ReactionAttemptEvent>(OnReactionAttempt);
        SubscribeLocalEvent<BloodstreamComponent, SolutionRelayEvent<ReactionAttemptEvent>>(OnReactionAttempt);
    }

    private void OnComponentInit(Entity<BloodstreamComponent> entity, ref ComponentInit args)
    {
        var chemicalSolution = _solutionContainerSystem.EnsureSolution(entity.Owner, entity.Comp.ChemicalSolutionName);
        var bloodSolution = _solutionContainerSystem.EnsureSolution(entity.Owner, entity.Comp.BloodSolutionName);
        var tempSolution = _solutionContainerSystem.EnsureSolution(entity.Owner, entity.Comp.BloodTemporarySolutionName);

        chemicalSolution.MaxVolume = entity.Comp.ChemicalMaxVolume;
        bloodSolution.MaxVolume = entity.Comp.BloodMaxVolume;
        tempSolution.MaxVolume = entity.Comp.BleedPuddleThreshold * 4; // give some leeway, for chemstream as well

        // Fill blood solution with BLOOD
        bloodSolution.AddReagent(entity.Comp.BloodReagent, entity.Comp.BloodMaxVolume - bloodSolution.Volume);
    }

    private void OnReactionAttempt(Entity<BloodstreamComponent> entity, ref SolutionRelayEvent<ReactionAttemptEvent> args)
    {
        if (args.Name != BloodstreamComponent.DefaultBloodSolutionName
            && args.Name != BloodstreamComponent.DefaultChemicalsSolutionName
            && args.Name != BloodstreamComponent.DefaultBloodTemporarySolutionName)
            return;

        OnReactionAttempt(entity, ref args.Event);
    }

    private void OnReactionAttempt(Entity<BloodstreamComponent> entity, ref ReactionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        foreach (var effect in args.Reaction.Effects)
        {
            switch (effect)
            {
                case CreateEntityReactionEffect: // Prevent entities from spawning in the bloodstream
                case AreaReactionEffect: // No spontaneous smoke or foam leaking out of blood vessels.
                    args.Cancelled = true;
                    return;
            }
        }

        // The area-reaction effect canceling is part of avoiding smoke-fork-bombs (create two smoke bombs, that when
        // ingested by mobs create more smoke). This also used to act as a rapid chemical-purge, because all the
        // reagents would get carried away by the smoke/foam. This does still work for the stomach (I guess people vomit
        // up the smoke or spawned entities?).

        // TODO apply organ damage instead of just blocking the reaction?
        // Having cheese-clots form in your veins can't be good for you.
    }

    /// <summary>
    ///     Shows text on health examine, based on bleed rate and blood level.
    /// </summary>
    private void OnHealthBeingExamined(EntityUid uid, BloodstreamComponent component, HealthBeingExaminedEvent args)
    {
        // Shows profusely bleeding at half the max bleed rate.
        if (component.BleedAmount > component.MaxBleedAmount / 2)
        {
            args.Message.PushNewline();
            args.Message.AddMarkup(Loc.GetString("bloodstream-component-profusely-bleeding", ("target", Identity.Entity(uid, EntityManager))));
        }
        // Shows bleeding message when bleeding, but less than profusely.
        else if (component.BleedAmount > 0)
        {
            args.Message.PushNewline();
            args.Message.AddMarkup(Loc.GetString("bloodstream-component-bleeding", ("target", Identity.Entity(uid, EntityManager))));
        }

        // If the mob's blood level is below the damage threshhold, the pale message is added.
        if (GetBloodLevelPercentage(uid, component) < component.BloodlossThreshold)
        {
            args.Message.PushNewline();
            args.Message.AddMarkup(Loc.GetString("bloodstream-component-looks-pale", ("target", Identity.Entity(uid, EntityManager))));
        }
    }

    private void OnBeingGibbed(EntityUid uid, BloodstreamComponent component, BeingGibbedEvent args)
    {
        SpillAllSolutions(uid, component);
    }

    private void OnApplyMetabolicMultiplier(EntityUid uid, BloodstreamComponent component, ApplyMetabolicMultiplierEvent args)
    {
        Dirty(uid, component);

        if (args.Apply)
        {
            component.UpdateInterval *= args.Multiplier;
            return;
        }
        component.UpdateInterval /= args.Multiplier;
        // TODO: This code is shit if you need to do this.
        // Reset the accumulator properly
        if ((component.NextUpdate - Timing.CurTime).TotalSeconds > component.UpdateInterval)
            component.NextUpdate = Timing.CurTime + TimeSpan.FromSeconds(component.UpdateInterval);
    }

    public override bool TryModifyBloodLevel(EntityUid uid, FixedPoint2 amount, BloodstreamComponent? component = null)
    {
        if (!base.TryModifyBloodLevel(uid, amount, component))
            return false;

        if (!Resolve(uid, ref component))
            return false;

        _solutionContainerSystem.ResolveSolution(uid, component.BloodTemporarySolutionName, ref component.TemporarySolution, out var tempSolution);

        if (tempSolution != null && tempSolution.Volume > component.BleedPuddleThreshold)
        {
            // Pass some of the chemstream into the spilled blood.
            if (_solutionContainerSystem.ResolveSolution(uid, component.ChemicalSolutionName, ref component.ChemicalSolution))
            {
                var temp = _solutionContainerSystem.SplitSolution(component.ChemicalSolution.Value, tempSolution.Volume / 10);
                tempSolution.AddSolution(temp, PrototypeManager);
            }

            if (_puddleSystem.TrySpillAt(uid, tempSolution, out var puddleUid, false))
            {
                _forensicsSystem.TransferDna(puddleUid, uid, false);
            }

            tempSolution.RemoveAllSolution();
        }

        return true;
    }

    public override void SpillAllSolutions(EntityUid uid, BloodstreamComponent? component = null)
    {
        base.SpillAllSolutions(uid, component);

        if (!Resolve(uid, ref component))
            return;

        _solutionContainerSystem.ResolveSolution(uid, component.BloodTemporarySolutionName,
            ref component.TemporarySolution, out var tempSolution);

        if (tempSolution != null && _puddleSystem.TrySpillAt(uid, tempSolution, out var puddleUid))
        {
            _forensicsSystem.TransferDna(puddleUid, uid, false);
        }
    }
}
