# Notices

## Project status

NetEaseMusicCloudMatch is an independent, unofficial open-source project. It is not affiliated with, authorized by, endorsed by, or sponsored by NetEase Cloud Music or its operators.

The project name and documentation refer to NetEase Cloud Music only to identify the service with which the tool is designed to interoperate. NetEase, NetEase Cloud Music, 网易 and 网易云音乐, together with their logos and service content, are trademarks or other protected property of their respective owners. The project license grants no rights to those names, services, interfaces, music, artwork, metadata, or other third-party content.

## Implementation lineage

This repository is a new C#/.NET WPF implementation informed by the behavior of the following MIT-licensed projects:

- The original Windows project `wuhenge/NeteaseMusicCloudMatch`. The locally researched source snapshot was distributed with an MIT License carrying `Copyright (c) 2023 Healer`.
- The macOS reference implementation [`joeyee233/NetEaseMusicCloudMatch`](https://github.com/joeyee233/NetEaseMusicCloudMatch), distributed with an MIT License carrying `Copyright (c) 2025 zhioak`.

Their copyright notices are retained in this repository's [LICENSE](LICENSE). Attribution does not imply that the referenced authors maintain, endorse, or are responsible for this rewrite.

The legacy source snapshots used for research are intentionally not distributed in this repository. Research findings and independently implemented protocol behavior are documented under `docs/`.

## Direct dependencies

The application uses the following open-source packages. Their own licenses and copyright notices continue to apply:

| Package | Version | License |
| --- | ---: | --- |
| CommunityToolkit.Mvvm | 8.4.2 | MIT |
| Microsoft.Extensions.Hosting | 10.0.12 | MIT |
| Serilog | 4.4.0 | Apache-2.0 |
| Serilog.Extensions.Hosting | 10.0.0 | Apache-2.0 |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 |
| WPF-UI | 4.3.0 | MIT |

Transitive dependencies are restored from NuGet and remain subject to their respective licenses. Package metadata and license files in the restored packages are the authoritative notices for those components.
