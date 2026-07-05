# HybridCore Bridge — Rust (Oxide / uMod)

The in-game plugin that connects a **Rust** server to your
[HybridCore](https://github.com/HybridMindLabs/HybridCore) site.

It polls the site for queued commands (vote rewards, store purchases, giveaway
prizes, bans, …) and runs them in the server console, then confirms the ones it
executed. All delivery, retries and expiry are handled by the site.

## How it works

```
POST {site}/api/bridge/poll   →  { "commands": [ { "id": 12, "command": "inventory.giveto STEAMID64 scrap 100" } ] }
   (plugin runs each command via server.Command)
POST {site}/api/bridge/ack    ←  { "ids": [ 12 ] }
```

Authenticated with a per-server bearer token (`hcb_…`).

## Requirements

- **Rust** server with **Oxide / uMod**
- HybridCore **≥ 0.2.0** on the site side

## Installation

1. Copy `HybridCoreBridge.cs` into your server's `oxide/plugins/` folder.
   Oxide compiles and loads it automatically.

2. **Generate a token** — on the site: **Admin → Servers → (your server) →
   Bridge**, enable it and copy the `hcb_…` token (shown once).

3. **Configure** — edit `oxide/config/HybridCoreBridge.json`:
   ```json
   {
     "Site base URL (no trailing slash)": "https://your-community.com",
     "Bridge token (hcb_...)": "hcb_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
     "Poll interval (seconds)": 5.0,
     "Debug logging": false
   }
   ```

4. Reload the plugin:
   ```
   oxide.reload HybridCoreBridge
   ```

## Config

| Key | Default | Description |
| --- | --- | --- |
| Site base URL | `https://your-community.com` | Site base URL, no trailing slash |
| Bridge token | `none` | Per-server bridge token (`hcb_…`) |
| Poll interval (seconds) | `5.0` | Poll interval (minimum 2s) |
| Debug logging | `false` | Verbose console output |

## Commands

- `hcbridge.poll` (perm `hybridcore.bridge.admin`) — force an immediate poll.

## Notes

- Commands run **exactly as queued by the site** — the site substitutes
  placeholders (`{steamid}`, `{name}`, `{prize}`, …) before queueing. Write each
  reward's command for Rust's console (e.g. `inventory.giveto {steamid} scrap 100`).
- `{steamid}` resolves to the player's linked Steam account (**SteamID64**),
  which is exactly what Rust console commands expect.
- Delivery is **at-least-once**: the site re-sends unacked commands, so keep the
  server reachable. Executed commands are confirmed immediately after running.

## License

Proprietary — © HybridMind Labs.
