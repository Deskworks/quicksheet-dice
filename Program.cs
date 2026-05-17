using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace QuickSheetDice;

static class Program
{
    static readonly Random _rng = new();

    static void Main()
    {
        // Flush immediately so QuickSheet reads each line
        Console.OutputEncoding = Encoding.UTF8;

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var msg = JsonNode.Parse(line);
                if (msg == null) continue;

                string type = msg["type"]?.GetValue<string>() ?? "";

                if (type == "init")
                {
                    // Register: handle roll: prefix, output up to 12 rows x 1 col
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        type = "register",
                        prefix = "roll:",
                        width = 1,
                        height = 12
                    }));
                }
                else if (type == "activate")
                {
                    var paramsNode = msg["params"] as JsonArray;
                    string param = paramsNode?.Count > 0
                        ? paramsNode[0]?.GetValue<string>() ?? ""
                        : (msg["params"]?.GetValue<string>() ?? "");

                    var cells = Roll(param.Trim());
                    foreach (var cell in cells)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(new
                        {
                            type = "write",
                            id = Guid.NewGuid().ToString("N")[..8],
                            r = cell.Row,
                            c = cell.Col,
                            v = cell.Value
                        }));
                    }
                }
            }
            catch
            {
                // Malformed input — ignore
            }
        }
    }

    record CellOutput(int Row, int Col, string Value);

    // Supported notations:
    //   XdY          — roll X dice with Y sides (e.g. 2d6)
    //   XdY+Z / XdY-Z — add modifier (e.g. 2d6+3)
    //   XdYkHN       — keep highest N (e.g. 4d6kh3)
    //   XdYkLN       — keep lowest  N (e.g. 4d6kl2)
    //   dY           — shorthand for 1dY
    //   d%           — percentile (d100)
    //   XdF          — Fudge/FATE dice (-1/0/+1)
    //   help / ?     — usage hint
    static List<CellOutput> Roll(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr) || expr == "help" || expr == "?")
            return UsageRows();

        // Normalise: lowercase, strip spaces
        string raw = expr.ToLowerInvariant().Replace(" ", "");

        // Pattern: (X)d(Y|F|%)(kh|kl)(N)(+/-)(Z)
        var m = Regex.Match(raw,
            @"^(?<count>\d+)?d(?<sides>\d+|f|%)(?:k(?<keep>[hl])(?<keepn>\d+))?(?:(?<sign>[+\-])(?<mod>\d+))?$");

        if (!m.Success)
            return ErrorRow($"Unknown: {expr}  (try: 2d6+3, d20, 4d6kh3)");

        int count = m.Groups["count"].Success ? int.Parse(m.Groups["count"].Value) : 1;
        bool isFate = m.Groups["sides"].Value == "f";
        bool isPct = m.Groups["sides"].Value == "%";
        int sides = isFate ? 3 : isPct ? 100 : int.Parse(m.Groups["sides"].Value);

        if (count < 1 || count > 100) return ErrorRow("Count must be 1–100");
        if (!isFate && sides < 2) return ErrorRow("Sides must be ≥ 2");
        if (sides > 10000) return ErrorRow("Sides must be ≤ 10000");

        bool keepHighest = m.Groups["keep"].Value == "h";
        bool keepLowest = m.Groups["keep"].Value == "l";
        int keepN = m.Groups["keepn"].Success ? int.Parse(m.Groups["keepn"].Value) : count;
        bool hasKeep = keepHighest || keepLowest;
        if (hasKeep && (keepN < 1 || keepN >= count))
            return ErrorRow($"Keep count must be 1–{count - 1}");

        int mod = 0;
        if (m.Groups["mod"].Success)
        {
            mod = int.Parse(m.Groups["mod"].Value);
            if (m.Groups["sign"].Value == "-") mod = -mod;
        }

        // Roll dice
        int[] rolls = new int[count];
        for (int i = 0; i < count; i++)
            rolls[i] = isFate ? _rng.Next(1, 4) - 2 : _rng.Next(1, sides + 1);

        // Determine which dice are kept
        bool[] kept = new bool[count];
        int[] sorted = (int[])rolls.Clone();
        Array.Sort(sorted);
        int[] keepValues;
        if (hasKeep)
        {
            keepValues = keepHighest
                ? sorted.TakeLast(keepN).ToArray()
                : sorted.Take(keepN).ToArray();
            // Mark kept dice (greedy match from sorted)
            var remaining = keepValues.ToList();
            for (int i = 0; i < count; i++)
            {
                int idx = remaining.IndexOf(rolls[i]);
                if (idx >= 0) { kept[i] = true; remaining.RemoveAt(idx); }
            }
        }
        else
        {
            for (int i = 0; i < count; i++) kept[i] = true;
        }

        int keptSum = rolls.Where((_, i) => kept[i]).Sum();
        int total = keptSum + mod;

        // Detect critical (d20 only, single die)
        bool isCrit = count == 1 && sides == 20 && rolls[0] == 20;
        bool isFumble = count == 1 && sides == 20 && rolls[0] == 1;

        var cells = new List<CellOutput>();
        int row = 0;

        // Header: expression
        cells.Add(new(row++, 0, $"🎲 {expr}"));

        // Individual dice
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            string val = isFate ? FateSymbol(rolls[i]) : rolls[i].ToString();
            if (hasKeep && !kept[i])
                sb.Append($"[{val}] ");
        else
                sb.Append($"{val} ");
        }
        if (count > 1)
            cells.Add(new(row++, 0, $"Dice: {sb.ToString().Trim()}"));

        // Keep annotation
        if (hasKeep)
        {
            string keptStr = string.Join(" ", rolls
                .Where((_, i) => kept[i])
                .Select(v => isFate ? FateSymbol(v) : v.ToString()));
            cells.Add(new(row++, 0,
                $"Keep {(keepHighest ? "highest" : "lowest")} {keepN}: {keptStr}"));
        }

        // Modifier
        if (mod != 0)
            cells.Add(new(row++, 0, $"Modifier: {(mod > 0 ? "+" : "")}{mod}"));

        // Total
        string prefix = isCrit ? "💥 CRITICAL! " : isFumble ? "💀 FUMBLE! " : "";
        cells.Add(new(row++, 0, $"Total: {prefix}{total}"));

        // Stats when rolling multiple dice
        if (count > 1 && !hasKeep)
        {
            cells.Add(new(row++, 0, $"Min: {rolls.Min()}  Max: {rolls.Max()}"));
        }

        return cells;
    }

    static string FateSymbol(int v) => v switch { -1 => "−", 0 => "○", _ => "+" };

    static List<CellOutput> ErrorRow(string msg) =>
        [new(0, 0, $"⚠ {msg}")];

    static List<CellOutput> UsageRows() =>
    [
        new(0, 0, "🎲 QuickSheet Dice Roller"),
        new(1, 0, "Usage: roll: <notation>"),
        new(2, 0, ""),
        new(3, 0, "Examples:"),
        new(4, 0, "  roll: d20       — one d20"),
        new(5, 0, "  roll: 2d6+3     — 2d6 plus 3"),
        new(6, 0, "  roll: 4d6kh3    — keep highest 3"),
        new(7, 0, "  roll: 2d8-1     — with negative mod"),
        new(8, 0, "  roll: 4dF       — Fudge/FATE dice"),
        new(9, 0, "  roll: d%        — percentile (d100)"),
    ];
}
