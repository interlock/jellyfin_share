# Project Plan

## Current Status
- [x] Initialize project with multi-agent documentation structure
- [x] Define project goals and scope
- [x] Design Jellyfin Media Share plugin
- [ ] Build and verify plugin compiles
- [ ] Test on Jellyfin instance

## Project: Jellyfin Media Share Plugin

Cross-server library sharing via invite links. Based on `.strm` + `.nfo` federation — no server-to-server trust required.

### Architecture
- Owner side: Admin dashboard to create/revoke invite links, select shared libraries
- Guest side: Accepts invite URL, syncs media catalog, surfaces shared media as local library items via `.strm` files
- Stream proxy: shared media streams through owner's server via plugin's stream endpoint

### Key Files
```
Jellyfin.Plugin.MediaShare/
├── Jellyfin.Plugin.MediaShare.csproj
├── Plugin.cs                          # Plugin entry (BasePlugin + IHasWebPages)
├── Configuration/
│   ├── PluginConfiguration.cs          # Serialized plugin config
│   └── configPage.html                 # Admin dashboard UI
├── Models/
│   ├── ShareLink.cs
│   ├── SharedLibrary.cs
│   └── MediaItem.cs
├── Data/
│   └── ShareDbContext.cs               # LiteDB store
├── Services/
│   ├── ShareLinkService.cs             # Link CRUD
│   ├── FederationService.cs            # Peer sync + .strm generation
│   └── SyncScheduler.cs                # Periodic background sync
└── Controllers/
    ├── ShareLinkController.cs          # /mediashare endpoints
    └── StreamController.cs             # Stream proxy
```

### API Endpoints
- `POST /mediashare/invite/{code}` — accept incoming share invite
- `GET /mediashare/share/{linkId}/catalog` — peer catalog exchange
- `GET /mediashare/stream/{linkId}/{fileId}` — proxied media stream
- `POST /mediashare/admin/links` — create share link
- `DELETE /mediashare/admin/links/{id}` — revoke link
- `GET /mediashare/admin/links` — list active links

### TODO
- Add proper catalog generation using ILibraryManager
- Register SyncScheduler as IScheduledTask
- Handle range requests properly in StreamController
- Add server URL auto-detection (don't hardcode localhost:8096)
- Add plugin versioning + manifest
- Address security review issues