# ProconRulz

A powerful rules engine plugin for Procon (Battlefield game server administration tool). ProconRulz allows server administrators to define custom rules triggered by in-game events with flexible conditions and actions.

## Features

- **Event-driven rules**: Trigger on spawn, kill, teamkill, suicide, player join/leave, chat, round start/end
- **Rich conditions**: Kit, weapon, specialization, damage type, headshot, range, ping, team, map, player counts, rates, text matching
- **Powerful actions**: Say, yell, kill, kick, ban, temp ban, PunkBuster ban/kick, execute server commands
- **Variables system**: Set, increment, decrement, and test variables with INI file persistence
- **Newline support in yell messages**: Use `\n` in yell text for multi-line messages
- **Supported games**: BFBC2, MoH, BF3, BF4

## Installation

Copy all `.cs` files from the `src/` directory into your Procon plugins folder.

## Documentation

See the included `proconrulz.pdf` or `proconrulz.md` for full documentation on rule syntax and usage.

## Example Rules

```
# Warn and kill snipers when team is small
On Spawn;Teamsize 8;Kit Recon;Say No snipers on small teams!;Kill

# Log joins and leaves
On Join;Say %p% has joined the server
On Leave;Say %p% has left the server

# Limit RPG kills
On Kill;Weapon RPG-7,SMAW;Count 3;Kick Too many RPG kills
```

## License

GPLv3 -- see [LICENSE](LICENSE) for details.

## Author

Originally by **bambam** (Ian Forster-Lewis), with contributions from LCARSx64 (newline escape in yell messages).
