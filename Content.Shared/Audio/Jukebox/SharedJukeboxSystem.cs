using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Audio.Jukebox;

public abstract partial class SharedJukeboxSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected SharedPowerReceiverSystem ReceiverSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JukeboxComponent, JukeboxSelectedMessage>(OnJukeboxSelected);
        SubscribeLocalEvent<JukeboxComponent, JukeboxPlayingMessage>(OnJukeboxPlay);
        SubscribeLocalEvent<JukeboxComponent, JukeboxPauseMessage>(OnJukeboxPause);
        SubscribeLocalEvent<JukeboxComponent, JukeboxStopMessage>(OnJukeboxStop);
        SubscribeLocalEvent<JukeboxComponent, PowerChangedEvent>(OnPowerChanged);
    }

    protected virtual void UpdateUi(Entity<JukeboxComponent?> jukebox)
    {

    }

    private void OnPowerChanged(Entity<JukeboxComponent> entity, ref PowerChangedEvent args)
    {
        TryUpdateVisualState(entity.AsNullable());

        if (!ReceiverSystem.IsPowered(entity.Owner))
        {
            Stop(entity.AsNullable());
        }
    }

    private void OnJukeboxPlay(Entity<JukeboxComponent> ent, ref JukeboxPlayingMessage args)
    {
        TryPlay(ent.AsNullable());
    }

    private void OnJukeboxPause(Entity<JukeboxComponent> ent, ref JukeboxPauseMessage args)
    {
        Pause(ent.AsNullable());
    }

    private void OnJukeboxStop(Entity<JukeboxComponent> entity, ref JukeboxStopMessage args)
    {
        Stop(entity.AsNullable());
    }

    private void OnJukeboxSelected(EntityUid uid, JukeboxComponent component, JukeboxSelectedMessage args)
    {
        SetSelectedTrack((uid, component), args.SongId);
    }

    public bool TrySetSelectedTrackState(Entity<JukeboxComponent?> entity, ProtoId<JukeboxPrototype> track)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        if (entity.Comp.PlaybackState == JukeboxPlaybackState.Playing)
            return false;

        entity.Comp.SelectedSongId = track;
        entity.Comp.PlaybackState = JukeboxPlaybackState.Stopped;
        Dirty(entity);
        UpdateUi(entity);
        return true;
    }

    public bool TrySetPlaybackState(Entity<JukeboxComponent?> entity, JukeboxPlaybackState state)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        if (entity.Comp.PlaybackState == state)
            return false;

        if (state == JukeboxPlaybackState.Playing && string.IsNullOrEmpty(entity.Comp.SelectedSongId))
            return false;

        entity.Comp.PlaybackState = state;
        Dirty(entity);
        UpdateUi(entity);
        return true;
    }

    /// <summary>
    /// Set the selected track of the jukebox to the specified prototype.
    /// </summary>
    public virtual void SetSelectedTrack(Entity<JukeboxComponent?> ent, ProtoId<JukeboxPrototype> track)
    {
        if (!Resolve(ent, ref ent.Comp) || !TrySetSelectedTrackState(ent, track))
            return;

        // TODO: Flick
    }

    public virtual bool TryPlay(Entity<JukeboxComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp) || !TrySetPlaybackState(ent, JukeboxPlaybackState.Playing))
            return false;

        return true;
    }

    public virtual void Pause(Entity<JukeboxComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        TrySetPlaybackState(ent, JukeboxPlaybackState.Paused);
    }

    public virtual void Stop(Entity<JukeboxComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        TrySetPlaybackState(ent, JukeboxPlaybackState.Stopped);
        ent.Comp.AudioStream = Audio.Stop(ent.Comp.AudioStream);
        Dirty(ent);
    }

    public virtual void SetTime(Entity<JukeboxComponent?> ent, float songTime)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        Audio.SetPlaybackPosition(ent.Comp.AudioStream, songTime);
    }

    private void DirectSetVisualState(Entity<JukeboxComponent?> ent, JukeboxVisualState state)
    {

    }

    protected void TryUpdateVisualState(Entity<JukeboxComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var finalState = JukeboxVisualState.On;

        if (!ReceiverSystem.IsPowered(ent.Owner))
        {
            finalState = JukeboxVisualState.Off;
        }

        _appearanceSystem.SetData(ent, JukeboxVisuals.VisualState, finalState);
    }

    /// <summary>
    /// Returns whether or not the given jukebox is currently playing a song.
    /// </summary>
    public bool IsPlaying(Entity<JukeboxComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        return entity.Comp.AudioStream is { } audio && Audio.IsPlaying(audio);
    }
}
