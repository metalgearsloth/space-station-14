using Content.Server.Power.Components;
using Content.Shared.Audio.Jukebox;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Audio.Jukebox;

public sealed partial class JukeboxSystem : SharedJukeboxSystem
{
    [Dependency] private IPrototypeManager _protoManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JukeboxComponent, JukeboxSetTimeMessage>(OnJukeboxSetTime);
        SubscribeLocalEvent<JukeboxComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<JukeboxComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(Entity<JukeboxComponent> ent, ref ComponentInit args)
    {
        if (HasComp<ApcPowerReceiverComponent>(ent))
        {
            TryUpdateVisualState(ent.AsNullable());
        }
    }


    private void OnJukeboxSetTime(Entity<JukeboxComponent> ent, ref JukeboxSetTimeMessage args)
    {
        SetTime(ent.AsNullable(), args.SongTime);
    }

    private void OnComponentShutdown(Entity<JukeboxComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.AudioStream = Audio.Stop(ent.Comp.AudioStream);
    }

    /// <summary>
    /// Attempts to play the jukebox's current selected track.
    /// </summary>
    /// <returns>false if no track is selected or the track prototype cannot be found, otherwise true.</returns>
    public override bool TryPlay(Entity<JukeboxComponent?> ent)
    {
        if (!base.TryPlay(ent) || !Resolve(ent, ref ent.Comp))
            return false;

        if (Exists(ent.Comp.AudioStream))
        {
            Audio.SetState(ent.Comp.AudioStream, AudioState.Playing);
        }
        else
        {
            if (string.IsNullOrEmpty(ent.Comp.SelectedSongId) ||
                !_protoManager.Resolve(ent.Comp.SelectedSongId, out var jukeboxProto))
            {
                ent.Comp.PlaybackState = JukeboxPlaybackState.Stopped;
                Dirty(ent);
                return false;
            }

            ent.Comp.AudioStream = Audio.PlayPvs(jukeboxProto.Path, ent, AudioParams.Default
                .WithMaxDistance(10f)
                .WithStartMode(AudioStartMode.Synchronized))
                ?.Entity;
            Dirty(ent);
        }
        return true;
    }

    /// <summary>
    /// Stops any track that may currently be playing.
    /// </summary>
    public override void SetSelectedTrack(Entity<JukeboxComponent?> ent, ProtoId<JukeboxPrototype> track)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.PlaybackState == JukeboxPlaybackState.Playing)
            return;

        base.SetSelectedTrack(ent, track);
        ent.Comp.AudioStream = Audio.Stop(ent.Comp.AudioStream);
    }

    /// <summary>
    /// Pauses any track that may currently be playing.
    /// </summary>
    public override void Stop(Entity<JukeboxComponent?> entity)
    {
        base.Stop(entity);
    }

    /// <summary>
    /// Sets the playback position within the current audio track.
    /// </summary>
    /// <remarks>
    /// If setting based on user input, you may need to compensate for the player's ping.
    /// </remarks>
    public override void Pause(Entity<JukeboxComponent?> entity)
    {
        base.Pause(entity);

        if (!Resolve(entity, ref entity.Comp, false))
            return;

        Audio.SetState(entity.Comp.AudioStream, AudioState.Paused);
    }

    public override void SetTime(Entity<JukeboxComponent?> entity, float songTime)
    {
        base.SetTime(entity, songTime);
    }
}
