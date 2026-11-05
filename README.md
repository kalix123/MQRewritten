<div align="center">
    <img width="70%" src="LogoBanner.png?raw=true" alt="MQ Rewritten">
    <h3>The server and website behind MQ Rewritten, a fan revival of Monkey Quest.</h3>
    <p>
        <a href="https://mqrewritten.online"><strong>Play at mqrewritten.online</strong></a>
    </p>
</div>

> [!IMPORTANT]
> **This project is programmed with AI.** The game client, the website and our changes to this server were written
> with [Claude](https://claude.com/product/claude-code), an AI made by Anthropic, so expect some bugs. The
> [About page](https://mqrewritten.online/about) has more detail.

## Want to host your own server?

**Use [MQReawakened](https://github.com/FeroxFoxxo/MQReawakened), not this repo.**

MQ Rewritten is built around our own game client, written in Godot, which isn't part of this repo. Running this code by
itself won't give you a working game. MQReawakened is the original project this one is forked from. It works with the
original game client, and it has the setup guides and community support you'll need.

The setup guides in this repo (`SETUP.md`, `SETUP_LAUNCHER.md` and `ASSETLOADER.md`) come from MQReawakened and describe
how that project works. Follow the versions in [its repo](https://github.com/FeroxFoxxo/MQReawakened).

## Want to play?

Create an account and download the client at [mqrewritten.online](https://mqrewritten.online).

Found a bug? Report it on the [Help page](https://mqrewritten.online/en/report-a-bug).

## What's different from MQReawakened

The game server itself stays close to MQReawakened, so we can keep pulling in its updates. Our changes are mostly to the
website:

- A new look, with an About page and a changelog for the client.
- A Downloads page that serves our client.
- Password and username resets by email.
- The admin pages are hidden and blocked.
- `update-server.sh`, which updates our Linux server.

This source is public because the server is licensed under the [GNU AGPL v3](LICENSE.md), which requires it.

## Credits

This project wouldn't exist without [FeroxFoxxo](https://github.com/FeroxFoxxo) and everyone who built
[MQReawakened](https://github.com/FeroxFoxxo/MQReawakened). That includes [Victti](https://github.com/victti/), a concept
contributor and mentor on that project, and Z6mbie, who designed its logo.

The current MQ Rewritten logo is temporary. It was generated with code, not with a generative AI image model:
[`tools/make_logo.py`](tools/make_logo.py) draws it from shapes defined in the script. Claude wrote that script.

### Open source licenses

- [AssetRipper.IO.Endian](https://github.com/AssetRipper/AssetRipper/): binary read/write utility ([license](Server.Reawakened/Licences/LICENSE_ASSETRIPPER))
- [AssetStudio](https://github.com/Perfare/AssetStudio/): tool for exploring the original asset bundles ([license](Server.Reawakened/Licences/LICENSE_ASSETSTUDIO))
- [ServUO](https://github.com/ServUO/ServUO/): server emulator used as a base, written in C# .NET ([license](Server.Base/Licences/LICENSE_SERVUO))
- [Thrift](https://github.com/apache/thrift/): point-to-point RPC implementation ([license](Server.Reawakened/Licences/LICENSE_THRIFT))
- [Discord.Net](https://github.com/discord-net/Discord.Net/): reflective class initialization ([license](Server.Reawakened/Licences/LICENSE_DNET))

## Legal

This repo contains none of the original game's assets. MQ Rewritten is a fan project, and it isn't affiliated with or
sponsored by the original game's developers or publishers.
