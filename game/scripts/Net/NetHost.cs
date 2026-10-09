using Godot;

namespace FpsChess.Net;

/// <summary>
/// Creates the separate multiplayer branches. Server and client can live in one process (games against the
/// computer and LAN hosting): each gets its own <see cref="MultiplayerApi"/> rooted at its own node.
/// </summary>
public static class NetHost
{
    public static Error Listen(Node root, int port, string bindAddress)
    {
        var peer = new WebSocketMultiplayerPeer();
        var error = peer.CreateServer(port, bindAddress);
        if (error != Error.Ok) return error;
        Attach(root, peer);
        return Error.Ok;
    }

    public static Error Connect(Node root, string url)
    {
        var peer = new WebSocketMultiplayerPeer();
        var error = peer.CreateClient(url);
        if (error != Error.Ok) return error;
        Attach(root, peer);
        return Error.Ok;
    }

    private static void Attach(Node root, MultiplayerPeer peer)
    {
        var api = MultiplayerApi.CreateDefaultInterface();
        root.GetTree().SetMultiplayer(api, root.GetPath());
        api.MultiplayerPeer = peer;
    }

    /// <summary>Closes the connection and detaches the branch, so the node can be freed cleanly.</summary>
    public static void Close(Node root)
    {
        if (!root.IsInsideTree()) return;
        var api = root.GetTree().GetMultiplayer(root.GetPath());
        api.MultiplayerPeer?.Close();
        api.MultiplayerPeer = null;
    }
}
