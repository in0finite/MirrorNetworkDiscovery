
## MirrorNetworkDiscovery

Network discovery for [Mirror](https://github.com/vis2k/Mirror).


## Features

- Simple. 1 script. 600 lines of code.

- Uses C#'s UDP sockets for broadcasting and sending responses.

- Independent of current transport.

- Single-threaded.

- Tested on: Linux, Windows, Android.

- Can lookup specific servers on the internet (outside of local network).

- Has a separate [GUI script](/NetworkDiscoveryHUD.cs) for easy testing. This GUI can only connect to a server using Telepathy transport.

- Has support for custom response data.

- By default, server responds with: current scene, number of players, max number of players, game server port number, game signature.

- No impact on performance.


## Usage

Attach [NetworkDiscovery](/NetworkDiscovery.cs) script to NetworkManager's game object. Assign game server port number.

Now, you can use [NetworkDiscoveryHUD](/NetworkDiscoveryHUD.cs) script to test it (by attaching it to NetworkManager), or use the API directly:

```cs
// register listener
NetworkDiscovery.onReceivedServerResponse += (NetworkDiscovery.DiscoveryInfo info) =>
{
	// we received response from server
	// add it to list of servers, or connect immediately...
};

// send broadcast on LAN
// when server receives the packet, he will respond
NetworkDiscovery.SendBroadcast();

// on server side, you can register custom data for responding
NetworkDiscovery.RegisterResponseData("Game mode", "Deathmatch");
```

For more details on how to use it, check out NetworkDiscoveryHUD script.


## Inspector

![](/NetworkDiscoveryInInspector.png)


## Example GUI

![](/HUD.png)


## TODO

- Measure ping - requires that all socket operations are done in a separate thread, or using async methods

- Prevent detection of multiple localhost servers (by assigning GUID to each packet) ?

- Add "Refresh" button in GUI next to each server

- Join "Players" and "MaxNumPlayers" columns ?

- Catch the other exception which is thrown on windows - it's harmless, so it should not be logged

- Make sure packet-to-string conversion works with non-ascii characters

