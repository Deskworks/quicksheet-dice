# quicksheet-dice 🎲

A dice roller extension for [QuickSheet](https://github.com/cemheren/QuickSheet) — roll any dice notation right inside your terminal spreadsheet or desktop wallpaper.

## Features

- Standard dice notation: `2d6`, `d20`, `4d8+5`
- Keep highest / lowest: `4d6kh3` (drop lowest), `3d6kl2`
- Negative modifiers: `2d10-2`
- **Fudge/FATE dice**: `4dF` — returns `−` / `○` / `+`
- **Percentile**: `d%` (alias for d100)
- **Critical hit / fumble** detection on d20 (💥 / 💀)
- Roll up to 100 dice at once

## Install

```
ext: github:cemheren/quicksheet-dice
```

Type that into any cell in QuickSheet. The extension is downloaded and compiled automatically on first use.

## Usage

After install, type in any cell:

```
roll: 2d6+3
```

Output (next rows below the cell):

```
🎲 2d6+3
Dice: 4 5
Total: 12
```

### More examples

| Cell value      | What it does                        |
|-----------------|-------------------------------------|
| `roll: d20`     | Single d20 — shows 💥 on nat 20     |
| `roll: 2d6+3`   | Two d6 plus 3 modifier              |
| `roll: 4d6kh3`  | Roll 4d6, keep highest 3 (DnD stat) |
| `roll: 4d6kl2`  | Roll 4d6, keep lowest 2             |
| `roll: 2d8-1`   | Two d8 minus 1                      |
| `roll: 4dF`     | Four Fudge/FATE dice                |
| `roll: d%`      | Percentile roll (d100)              |
| `roll: help`    | Show usage hint                     |

### DnD character stats

Put six `roll: 4d6kh3` cells in a column — each shows a stat block roll.

### Tip: Re-roll anytime

Press **F3** on the cell (or delete and retype) to re-roll. Each activation generates a new random result.

## Requirements

- [QuickSheet](https://github.com/cemheren/QuickSheet)
- .NET 9 SDK (for first-time compilation)

## Protocol

Uses the standard QuickSheet extension JSON-lines protocol. Prefix: `roll:`. See [extension protocol docs](https://github.com/cemheren/QuickSheet/blob/main/docs/extension-protocol.md).

## License

MIT
