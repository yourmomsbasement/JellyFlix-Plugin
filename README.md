<p align="center"><img alt="JellyFlix" src="images/banner.png"/></p>

# JellyFlix plugin

The server side of [JellyFlix](https://github.com/yourmomsbasement/JellyFlix-Web), a Netflix-style Jellyfin. One plugin
that does four things:

- **Profiles**: one household login, several Netflix-style profiles ("Who's watching?"). Each profile is a hidden
  Jellyfin user, so watch history, Continue Watching, Next Up and parental controls are tracked per profile. Optional
  4-digit PINs and kids profiles.
- **Recommendations**: Top Picks, "Because you watched…", "More Like This" and the match percentage, learned from each
  profile's history. See [how it works](#how-recommendations-work).
- **Requests**: search shows movies and shows that aren't in the library, with a **Request** button that goes
  through [Seerr](https://github.com/seerr-team/seerr) (Jellyseerr / Overseerr). See [requests](#requests).
- **Web client**: downloads the JellyFlix web client from
  [JellyFlix-Web releases](https://github.com/yourmomsbasement/JellyFlix-Web/releases), serves it in place of Jellyfin's
  own and keeps it up to date.

<p align="center">
<img alt="Who's watching?" src="docs/screenshots/profiles.jpg" width="49%"/>
<img alt="TV home" src="docs/screenshots/tv-home.jpg" width="49%"/>
</p>

Requires **Jellyfin 13** (the `unstable` builds), the same version as the JellyFlix web client.

## Install

1. In Jellyfin, open **Dashboard → Plugins → Repositories** (Catalog → ⚙ on some versions) and add a repository:
   - Name: `JellyFlix`
   - URL: `https://raw.githubusercontent.com/yourmomsbasement/JellyFlix-Plugin/main/manifest.json`
2. Install **JellyFlix** from the catalog and restart Jellyfin.

That's it. On startup the plugin downloads the newest JellyFlix web client built for your Jellyfin version and serves it
at `/web` in place of the stock one, on Linux, Docker and Windows alike: no `--webdir`, environment variable or file
copying. New releases are installed automatically at startup and every six hours, and Jellyfin package upgrades no
longer replace the JellyFlix web client.

Settings live under **JellyFlix → Settings** in the dashboard's side menu (or **Dashboard → Plugins → JellyFlix**), and
the dashboard home gets a JellyFlix card. See [settings](#settings).

<p align="center">
<img alt="Dashboard" src="docs/screenshots/dashboard.jpg" width="49%"/>
<img alt="JellyFlix settings" src="docs/screenshots/dashboard-jellyflix.jpg" width="49%"/>
</p>

**Updating:** new plugin versions show up in **Dashboard → Plugins**; update JellyFlix there and restart Jellyfin.
The settings page tells you when a setting needs a newer plugin than the one installed.

**Upgrading from the separate *JellyFlix Profiles* / *JellyFlix Recommendations* plugins:** just install this one.
On its first start it removes the old plugins, carries their settings over and asks for one more restart (the dashboard
shows *Restart required*). Households and profiles are kept.

**Getting Jellyfin's own web client back:** untick **Serve the JellyFlix web client** in the JellyFlix settings. If
the web client itself is broken and you can't reach the dashboard, set `<ServeWebClient>false</ServeWebClient>` in
`plugins/configurations/JellyFlix.xml` in Jellyfin's data folder (or delete the `jellyflix-web` folder there) and
restart Jellyfin.

## Settings

| Setting | Default | What it does |
| --- | --- | --- |
| Maximum profiles per household | 5 | Including the account owner's own profile. |
| Highest parental rating score for kids profiles | 10 | 10 is PG / TV-PG; unrated titles are always hidden from kids profiles. |
| Let non-administrator accounts create profiles | On | Otherwise only administrators can have profiles. |
| Let profiles request titles that aren't in the library | Off | Shows request cards in search (needs Seerr below). |
| Seerr address / API key | — | Where Seerr runs and its API key (Seerr → Settings → General). |
| Let kids profiles request titles | Off | Kids profiles don't see request cards otherwise. |
| Serve the JellyFlix web client | On | Serve JellyFlix at `/web`; off gives Jellyfin's own web client back. |
| Install new JellyFlix web releases automatically | On | At startup and every six hours. |
| Release repository | `yourmomsbasement/JellyFlix-Web` | Where web client releases come from. |

Saving reads the settings back from the server and names any it didn't keep (for example, a setting an older plugin
doesn't have). **Save and test connection** and **Check for updates now** each show their result in a panel underneath:
a progress bar while they run, then whether it worked and, if not, why.

## Requests

In the JellyFlix settings, turn on **Requests** and enter the Seerr address (e.g. `http://192.168.1.10:5055`) and its
API key (Seerr → Settings → General), then press **Save and test connection**. A panel underneath confirms the
connection and Seerr's version, or shows Seerr's error (such as an invalid API key) and what to check.

<p align="center">
<img alt="Testing the Seerr connection" src="docs/screenshots/dashboard-requests.jpg" width="49%"/>
<img alt="Requesting from search" src="docs/screenshots/search-requests.jpg" width="49%"/>
</p>

- Search results get a **Not in your library** row: matching movies and shows from Seerr that the library doesn't
  have, with what's already requested or on its way. Titles that are in the library, and people, are left out.
- **Request** asks Seerr for the movie, or every season of a show. Profiles aren't Seerr users, so the request is made
  as the household owner's Seerr account: users imported into Seerr from Jellyfin are matched automatically; without a
  match, the request is made as the API key's user. Seerr's own permissions and approval rules apply.
- Kids profiles don't see the row unless **Let kids profiles request titles** is on.
- The API key stays on the server: browsers only talk to the plugin.

## How the web updater works

- Releases are read newest first, and the check stops at the first one built for the running Jellyfin version
  (major.minor), so a check normally downloads one small `release.json` and, if it's new, the ~36 MB client.
- The **Update JellyFlix web client** scheduled task (Dashboard → Scheduled Tasks → JellyFlix) shows its progress
  while the client downloads. A download that stalls gives up after 15 minutes, so it never blocks later checks.
- Only releases built for the running Jellyfin version are installed, and every download is checked against the SHA-256
  published with the release.
- The new client is unpacked next to the old one and swapped in with a folder rename, so a failed update never leaves a
  half-written client. The installed build is recorded in `jellyflix-release.json`.
- It lives in `jellyflix-web` in Jellyfin's data folder (`/var/lib/jellyfin`, `/config` in Docker,
  `C:\ProgramData\Jellyfin\Server` on Windows). The plugin serves it through a small ASP.NET startup filter that runs
  ahead of Jellyfin's own web hosting; anything the build doesn't contain, and everything outside `/web`, is left to
  Jellyfin.

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
| `GET` | `/JellyFlix/Requests/Search?query=` | Titles matching a search that aren't in the library, from Seerr. |
| `POST` | `/JellyFlix/Requests` | Request a title (`{ TmdbId, MediaType: "movie" \| "tv" }`). |
| `POST` | `/JellyFlix/Requests/Test` | Check the Seerr connection (administrators). |
| `GET` | `/JellyFlix/WebUpdate` | Web Updater status (administrators). |
| `POST` | `/JellyFlix/WebUpdate/Check` | Check for and install a web client now (administrators). |

## Versions

| Version | Changes |
| --- | --- |
| 1.1.1 | Faster web client checks (stop at the first compatible release), download progress, a 15-minute download limit. |
| 1.1.0 | Requests through Seerr from search. |
| 1.0.1 | Serves the web client itself (no `--webdir`), removes the old separate plugins and carries their settings over. |
| 1.0.0 | Profiles, recommendations and the web updater in one plugin. |

## Development

The plugin builds against the JellyFlix server fork, included as the `jellyfin-server` submodule, so it always matches
the Jellyfin version it targets.

```sh
git clone --recurse-submodules https://github.com/yourmomsbasement/JellyFlix-Plugin.git
cd JellyFlix-Plugin
dotnet test JellyFlix.slnx
dotnet build src/JellyFlix -c Release   # → src/JellyFlix/bin/Release/net10.0/JellyFlix.dll
```

Releasing: push a tag such as `v1.2.0`, or run the **Release** workflow from the Actions tab with version `1.2.0`. The
workflow tests, builds `jellyflix_1.2.0.0.zip`, attaches it to a GitHub release and adds it to `manifest.json`, which
Jellyfin's catalog reads.
