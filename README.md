# JellyFlix plugin

The server side of [JellyFlix](https://github.com/yourmomsbasement/JellyFlix-Web), a Netflix-style Jellyfin. One plugin with three parts:

- **Profiles**: one household login, several Netflix-style profiles ("Who's watching?"). Each profile is a hidden
  Jellyfin user, so watch history, Continue Watching, Next Up and parental controls are tracked per profile. Optional
  4-digit PINs and kids profiles.
- **Recommendations**: Top Picks, "Because you watched…", "More Like This" and the match percentage, learned from each
  profile's history. See [how it works](#how-recommendations-work).
- **Web Updater**: keeps the JellyFlix web client up to date from
  [JellyFlix-Web releases](https://github.com/yourmomsbasement/JellyFlix-Web/releases).

Requires **Jellyfin 13** (the `unstable` builds), the same version as the JellyFlix web client.

## Install

1. In Jellyfin, open **Dashboard → Plugins → Repositories** (Catalog → ⚙ on some versions), add a repository:
   - Name: `JellyFlix`
   - URL: `https://raw.githubusercontent.com/yourmomsbasement/JellyFlix-Plugin/main/manifest.json`
2. Install **JellyFlix** from the catalog and restart Jellyfin.
3. Open **Dashboard → Plugins → JellyFlix** and press **Check for updates now** to download the web client.
4. Point Jellyfin's web folder at the folder shown on that page, once, and restart Jellyfin:
   - **Linux package**: in `/etc/default/jellyfin` set `JELLYFIN_WEB_OPT="--webdir=/var/lib/jellyfin/jellyflix-web"`
   - **Docker**: add the environment variable `JELLYFIN_WEB_DIR=/config/jellyflix-web`
   - **Windows**: add `--webdir "C:\ProgramData\Jellyfin\Server\jellyflix-web"` to the server's startup arguments

   From then on, new JellyFlix web releases are installed automatically (at startup and every six hours), and
   Jellyfin package upgrades no longer replace the JellyFlix web client.

Upgrading from the separate *JellyFlix Profiles* / *JellyFlix Recommendations* plugins: remove both from
**Dashboard → Plugins** (or delete their folders from the plugins folder) before installing this one. Households and
profiles are kept; the profile settings return to their defaults.

## How the web updater stays safe

- Only releases built for the running Jellyfin version (major.minor) are installed.
- Every download is checked against the SHA-256 published with the release.
- The new client is unpacked next to the old one and swapped in with a folder rename, so a failed update never leaves a
  half-written client. The installed build is recorded in `jellyflix-release.json`.

## How recommendations work

Titles become vectors of their genres, tags, studios, directors, writers, top cast and decade, weighted by kind and by
how rare each trait is in the library. A profile's taste is the weighted sum of what it watched, how far it got into
shows, its My List and its thumbs up/down, with recent activity counting more. Unwatched titles are ranked by
similarity to that taste plus a little rating and freshness, then re-ranked so one genre doesn't fill a row. New
profiles get well-rated, recently added titles. Only titles the profile may see are considered.

## API

All endpoints use the signed-in user's session.

| Method | Path | Description |
| --- | --- | --- |
| `GET` | `/JellyFlix/Profiles` | The household and its profiles. |
| `POST` | `/JellyFlix/Profiles` | Add a profile (owner). |
| `POST` | `/JellyFlix/Profiles/{id}` | Change a profile (owner, or the profile itself). |
| `DELETE` | `/JellyFlix/Profiles/{id}` | Remove a profile and its watch history (owner). |
| `POST` | `/JellyFlix/Profiles/{id}/Switch` | Switch this device to a profile (`{ Pin? }`). |
| `GET` | `/JellyFlix/Recommendations` | Top Picks and Because-you-watched rows. |
| `GET` | `/JellyFlix/Recommendations/Similar/{id}` | More Like This and the match percentage. |
| `GET` | `/JellyFlix/WebUpdate` | Web Updater status (administrators). |
| `POST` | `/JellyFlix/WebUpdate/Check` | Check for and install a web client now (administrators). |

## Development

The plugin builds against the JellyFlix server fork, included as the `jellyfin-server` submodule, so it always matches
the Jellyfin version it targets.

```sh
git clone --recurse-submodules https://github.com/yourmomsbasement/JellyFlix-Plugin.git
cd JellyFlix-Plugin
dotnet test JellyFlix.slnx
dotnet build src/JellyFlix -c Release   # → src/JellyFlix/bin/Release/net10.0/JellyFlix.dll
```

Releasing: push a tag such as `v1.0.1`, or run the Release workflow from the Actions tab with version `1.0.1`. The release workflow tests, builds `jellyflix_1.0.1.0.zip`, attaches it to a
GitHub release and adds it to `manifest.json`, which Jellyfin's catalog reads.
