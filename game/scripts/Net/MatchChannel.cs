using Godot;

namespace FpsChess.Net;

public interface IMatchServer
{
    void OnSubmitMove(int peer, string uci);
    void OnResign(int peer);
    void OnSaveRequested(int peer);
    void OnPauseRequested(int peer, bool paused);
    void OnDuelInput(int peer, Vector3 position, float yaw, float pitch);
    void OnTrigger(int peer, bool held);
    void OnFire(int peer, Vector3 origin, Vector3[] ends, int hitMask);
    void OnReload(int peer);
    void OnDash(int peer);
    void OnThrowGrenade(int peer, Vector3 origin, Vector3 velocity);
}

public interface IMatchClient
{
    void OnState(string json);
    void OnMovePlayed(string uci, int outcome);
    void OnDuelStarted(string json);
    void OnDuelSnapshot(Vector3 attacker, float attackerYaw, float attackerPitch, Vector3 defender, float defenderYaw, float defenderPitch, float[] stats);
    void OnShotFired(int role, Vector3 origin, Vector3[] ends);
    void OnDamaged(int role, float amount, bool headshot);
    void OnGrenadeThrown(int role, Vector3 origin, Vector3 velocity);
    void OnGrenadeExploded(Vector3 position);
    void OnDuelEnded(int outcome);
    void OnSaveData(string json);
    void OnNotice(string text);
    void OnClockSync(long whiteMs, long blackMs);
}

/// <summary>
/// RPC endpoint of one room, at "Rooms/&lt;code&gt;" on both sides (see <see cref="LobbyChannel"/>).
/// Outcomes and roles travel as ints: <see cref="Chess.Core.DuelOutcome"/> and <see cref="Chess.Core.Duels.DuelRole"/>, -1 for none.
/// </summary>
public partial class MatchChannel : Node
{
    public IMatchServer? Server { get; set; }
    public IMatchClient? Client { get; set; }

    private int Sender => Multiplayer.GetRemoteSenderId();

    // ---- client to server

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SubmitMove(string uci) => Server?.OnSubmitMove(Sender, uci);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void Resign() => Server?.OnResign(Sender);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void RequestSave() => Server?.OnSaveRequested(Sender);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SetPaused(bool paused) => Server?.OnPauseRequested(Sender, paused);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 1)]
    public void DuelInput(Vector3 position, float yaw, float pitch) => Server?.OnDuelInput(Sender, position, yaw, pitch);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void Trigger(bool held) => Server?.OnTrigger(Sender, held);

    /// <summary>
    /// One shot as the shooter saw it: where each pellet ended and, as a bit mask, which pellets hit the
    /// opponent on the shooter's screen. The server checks every claimed hit before applying damage.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void Fire(Vector3 origin, Vector3[] ends, int hitMask) => Server?.OnFire(Sender, origin, ends, hitMask);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void Reload() => Server?.OnReload(Sender);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void Dash() => Server?.OnDash(Sender);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ThrowGrenade(Vector3 origin, Vector3 velocity) => Server?.OnThrowGrenade(Sender, origin, velocity);

    // ---- server to client

    [Rpc]
    public void State(string json) => Client?.OnState(json);

    [Rpc]
    public void MovePlayed(string uci, int outcome) => Client?.OnMovePlayed(uci, outcome);

    [Rpc]
    public void DuelStarted(string json) => Client?.OnDuelStarted(json);

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 1)]
    public void DuelSnapshot(Vector3 attacker, float attackerYaw, float attackerPitch, Vector3 defender, float defenderYaw, float defenderPitch, float[] stats) =>
        Client?.OnDuelSnapshot(attacker, attackerYaw, attackerPitch, defender, defenderYaw, defenderPitch, stats);

    [Rpc]
    public void ShotFired(int role, Vector3 origin, Vector3[] ends) => Client?.OnShotFired(role, origin, ends);

    [Rpc]
    public void Damaged(int role, float amount, bool headshot) => Client?.OnDamaged(role, amount, headshot);

    [Rpc]
    public void GrenadeThrown(int role, Vector3 origin, Vector3 velocity) => Client?.OnGrenadeThrown(role, origin, velocity);

    [Rpc]
    public void GrenadeExploded(Vector3 position) => Client?.OnGrenadeExploded(position);

    [Rpc]
    public void DuelEnded(int outcome) => Client?.OnDuelEnded(outcome);

    [Rpc]
    public void SaveData(string json) => Client?.OnSaveData(json);

    [Rpc]
    public void Notice(string text) => Client?.OnNotice(text);

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 1)]
    public void ClockSync(long whiteMs, long blackMs) => Client?.OnClockSync(whiteMs, blackMs);
}
