using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

Console.WriteLine("Starting Warlord CCG DataParser...");

string csvPath = DataParser.LocateFilePath("WarlordCards.csv");
if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
{
    Console.WriteLine("ERROR: Could not find WarlordCards.csv in the DataParser folder or its parent directories (searched upward from the application base directory).");
    return;
}

Console.WriteLine($"Found CSV at: {csvPath}");

string dataParserDir = Path.GetDirectoryName(Path.GetFullPath(csvPath))!;
string outputJsonPath = Path.Combine(dataParserDir, "cards.json");

DataParser.ParseCSV(csvPath, outputJsonPath);

public class DataParser
{
    public static string LocateFilePath(string filename)
    {
        // 1. Search up from AppContext.BaseDirectory (build output -> project folder -> repo root).
        // The current working directory is intentionally not consulted so behaviour is identical
        // under `dotnet run`, IDE launchers and direct execution of the built binary.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string p1 = Path.Combine(dir.FullName, filename);
            if (File.Exists(p1)) return p1;

            string p2 = Path.Combine(dir.FullName, "DataParser", filename);
            if (File.Exists(p2)) return p2;

            dir = dir.Parent;
        }

        return filename;
    }

    public static void ParseCSV(string csvFilePath, string outputJsonPath)
    {
        if (!File.Exists(csvFilePath))
        {
            Console.WriteLine($"ERROR: Could not find {csvFilePath}");
            return;
        }

        string rawText = File.ReadAllText(csvFilePath, Encoding.UTF8);
        var rows = ParseCsvProperly(rawText);

        if (rows.Count < 2)
        {
            Console.WriteLine("ERROR: CSV contains no data rows.");
            return;
        }

        var headers = rows[0].Select(h => h.Trim()).ToList();
        var cardDefinitions = new List<Dictionary<string, object>>();

        int ColIdx(string colName) => headers.IndexOf(colName);
        string GetCol(string[] cols, string colName)
        {
            int idx = ColIdx(colName);
            return (idx >= 0 && idx < cols.Length) ? cols[idx].Trim() : string.Empty;
        }

        // Start at 1 to skip headers
        for (int i = 1; i < rows.Count; i++)
        {
            var columns = rows[i];
            if (columns.Length < headers.Count) continue;

            string name = GetCol(columns, "Name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Canonical card name adjustments for known CSV inconsistencies
            if (name.Equals("Adathyn Celestial Messenger", StringComparison.OrdinalIgnoreCase))
            {
                name = "Adathan, Celestial Messenger";
            }
            else if (name.Equals("Vactus Spirit's Champion", StringComparison.OrdinalIgnoreCase))
            {
                name = "Vactus, Spirit's Champion";
            }

            string type = GetCol(columns, "Type");
            string charType = GetCol(columns, "Character Type");
            string cardClass = GetCol(columns, "Class");
            string alignment = GetCol(columns, "Alignment");
            string rawRules = GetCol(columns, "Rules Text");
            string rulesText = rawRules.Replace("\r", "").Replace("\n", "\\n");

            bool isWarlord = charType.Equals("Warlord", StringComparison.OrdinalIgnoreCase);
            bool isCharacter = type.Equals("Character", StringComparison.OrdinalIgnoreCase) || isWarlord;

            var cardDef = new Dictionary<string, object>
            {
                { "DefinitionId", name.Replace(" ", "_").ToLowerInvariant() },
                { "Name", name },
                { "Type", type },
                { "Class", cardClass },
                { "Alignment", alignment },
                { "RulesText", rulesText }
            };

            // Parse Level for ALL cards (Characters, Actions, Items, Warlords)
            string rawLevel = GetCol(columns, "Level");
            if (!string.IsNullOrWhiteSpace(rawLevel) && rawLevel != "-")
            {
                cardDef["Level"] = ParseInt(rawLevel);
            }

            // Parse Stats
            if (isCharacter)
            {
                int ac = ParseInt(GetCol(columns, "Armor Class"));
                // Correct known errata if present
                if (name.Equals("Crownland Strategist", StringComparison.OrdinalIgnoreCase) && ac == 4)
                {
                    ac = 14;
                }

                cardDef["AC"] = ac;
                cardDef["Skill"] = ParseInt(GetCol(columns, "Skill"));
                cardDef["BaseHp"] = ParseInt(GetCol(columns, "Hit Points"));
                cardDef["Strikes"] = ParseStrikes(GetCol(columns, "Attack"));
            }
            else
            {
                // Item and Action cards: only export non-empty stats
                string rawAc = GetCol(columns, "Armor Class");
                if (!string.IsNullOrWhiteSpace(rawAc) && rawAc != "-")
                {
                    cardDef["AC"] = ParseInt(rawAc);
                }

                string rawSkill = GetCol(columns, "Skill");
                if (!string.IsNullOrWhiteSpace(rawSkill) && rawSkill != "-")
                {
                    cardDef["Skill"] = ParseInt(rawSkill);
                }

                string rawHp = GetCol(columns, "Hit Points");
                if (!string.IsNullOrWhiteSpace(rawHp) && rawHp != "-")
                {
                    cardDef["BaseHp"] = ParseInt(rawHp);
                }

                string rawAtk = GetCol(columns, "Attack");
                if (!string.IsNullOrWhiteSpace(rawAtk) && rawAtk != "-")
                {
                    var strikes = ParseStrikes(rawAtk);
                    if (strikes.Count > 0)
                    {
                        cardDef["Strikes"] = strikes;
                    }
                }
            }

            // Tags
            var tags = new List<string>();
            void AddTags(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw) || raw == "-") return;
                var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var p in parts)
                {
                    if (!string.IsNullOrWhiteSpace(p) && p != "-")
                    {
                        tags.Add(p);
                    }
                }
            }

            AddTags(GetCol(columns, "Keywords"));
            AddTags(GetCol(columns, "Traits"));
            AddTags(GetCol(columns, "Feats"));

            string faction = GetCol(columns, "Faction");
            if (!string.IsNullOrWhiteSpace(faction) && faction != "-")
            {
                tags.Add(faction);
            }

            // Include Character Type in tags (e.g. "Warlord", "Overlord", etc.)
            if (!string.IsNullOrWhiteSpace(charType) && charType != "-")
            {
                cardDef["CharacterType"] = charType;
                tags.Add(charType);
            }

            cardDef["Tags"] = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            cardDefinitions.Add(cardDef);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Preserves human-readable '+' and '''
        };

        File.WriteAllText(outputJsonPath, JsonSerializer.Serialize(cardDefinitions, options), Encoding.UTF8);
        Console.WriteLine($"Successfully exported {cardDefinitions.Count} cards to {outputJsonPath}");
    }

    private static List<string[]> ParseCsvProperly(string text)
    {
        var result = new List<string[]>();
        var currentLine = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                currentLine.Add(currentField.ToString());
                currentField.Clear();
            }
            else if ((c == '\r' || c == '\n') && !inQuotes)
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                currentLine.Add(currentField.ToString());
                currentField.Clear();
                result.Add(currentLine.ToArray());
                currentLine.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        if (currentField.Length > 0 || currentLine.Count > 0)
        {
            currentLine.Add(currentField.ToString());
            result.Add(currentLine.ToArray());
        }

        return result;
    }

    private static int ParseInt(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0;
        var cleaned = val.Trim().Replace("+", "");
        return int.TryParse(cleaned, out int result) ? result : 0;
    }

    private static List<Dictionary<string, int>> ParseStrikes(string attackStr)
    {
        var strikes = new List<Dictionary<string, int>>();
        if (string.IsNullOrWhiteSpace(attackStr) || attackStr.Trim() == "-") return strikes;

        var parts = attackStr.Split('/');
        foreach (var p in parts)
        {
            var cleaned = p.Replace("+", "").Trim();
            if (int.TryParse(cleaned, out int bonus))
            {
                strikes.Add(new Dictionary<string, int> { { "Bonus", bonus } });
            }
        }
        return strikes;
    }
}
