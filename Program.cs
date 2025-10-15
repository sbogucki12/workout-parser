using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace WorkoutParserApp
{
    internal class Program
    {
        // ---------------- Patterns ----------------
        static readonly Regex WorkoutHeaderHint = new(
            @"^\s*(here\s+(is|was)\s+(my|the)\s+workout|here\s+is\s+what\s+i\s+did|my\s+most\s+recent\s+workout|log\s+for\s+future\s+reference|please\s+log|Push\s*[""A-Za-z0-9]*|Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|August|September|October)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex TimeRange = new(
            @"(?<t1>\d{1,2}:\d{2})\s*(a\.m\.|p\.m\.)?\s*[-–]\s*(?<t2>\d{1,2}:\d{2})\s*(a\.m\.|p\.m\.)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex DateCue = new(
            @"\b(?<m>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<d>\d{1,2})\b|\bToday\s+is\s+(?<tm>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<td>\d{1,2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex ExerciseLine = new(
            @"^\s*(\d+[\).\s]+)?(?<name>[A-Za-z][A-Za-z0-9/\-\s&\(\)]+?)(?:\s*[:\-–])\s*(?<sets>.+)$",
            RegexOptions.Compiled);

        static readonly Regex SetToken = new(
            @"(?:(?<sets>\d+)\s*[xX]\s*(?<reps>\d+)\s*(?:@|\bat\b)\s*(?<weight>\d+(?:\.\d+)?)\s*(?<unit>lbs?|s|kg)|(?<onlyreps>\d+)\s*(?:reps?)|\bAMRAP\b|(?:(?<reps2>\d+)\s*[xX]\s*(?<amrap>AMRAP))|@?\s*(?<wonly>\d+(?:\.\d+)?)\s*(?<uonly>lbs?|s|kg))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex PlanCue = new(
            @"\b(provide|recommend|suggest|tonight'?s\s+workout|plan\s+tonight|what\s+should\s+i\s+do|please\s+plan|it\s+doesn'?t\s+have\s+to|avoid\s+legs)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex YouSaid = new(@"^\s*You said:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ---------------- Entry ----------------
        static void Main(string[] args)
        {
            // Resolve the input path (allow default file when no args)
            string path;
            if (args.Length == 0)
            {
                // Change the default location/filename if you keep your log elsewhere
                var candidate = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "workoutlog_oct2025.txt"
                );

                if (!File.Exists(candidate))
                {
                    Console.Error.WriteLine("Usage: dotnet run -- \"C:\\full\\path\\to\\workoutlog_oct2025.txt\"");
                    Console.Error.WriteLine($"(Tried default: {candidate})");
                    return;
                }

                path = candidate;
            }
            else
            {
                path = args[0];
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"File not found: {path}");
                    return;
                }
            }

            // Load lines once using the resolved 'path'
            var lines = File.ReadAllLines(path);

            var blocks = ChunkBlocks(lines);
            var workoutBlocks = blocks.Where(IsCompletedWorkoutBlock).ToList();

            var sessions = new List<WorkoutSession>();
            var setRows = new List<SetRow>();

            int sessionCounter = 1;
            foreach (var block in workoutBlocks)
            {
                var sessionId = $"W{sessionCounter:0000}";
                var (date, start, end, title) = ParseHeader(block);
                var exerciseRows = ParseExercises(block);

                sessions.Add(new WorkoutSession(
                    sessionId,
                    date,
                    start,
                    end,
                    string.IsNullOrWhiteSpace(title) ? "Workout" : title.Trim(),
                    Truncate(block, 200)));

                int exOrd = 0;
                foreach (var er in exerciseRows)
                {
                    exOrd++;
                    int setOrd = 0;

                    if (er.Sets.Count == 0)
                    {
                        setRows.Add(new SetRow(sessionId, exOrd, er.Name, ++setOrd, null, null, "", false, er.Notes));
                        continue;
                    }

                    foreach (var s in er.Sets)
                    {
                        setRows.Add(new SetRow(sessionId, exOrd, er.Name, ++setOrd, s.Reps, s.Weight, s.Unit, s.IsAmrap, s.Notes));
                    }
                }

                sessionCounter++;
            }

            var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;

            // Put outputs in an "output" subfolder next to the input file
            var outDir = Path.Combine(dir, "output");
            Directory.CreateDirectory(outDir);

            // Timestamped filenames
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var workoutsPath = GetUniquePath(Path.Combine(outDir, $"workouts_{stamp}.csv"));
            var setsPath = GetUniquePath(Path.Combine(outDir, $"sets_{stamp}.csv"));

            WriteWorkoutsCsv(workoutsPath, sessions);
            WriteSetsCsv(setsPath, setRows);
            // -- Post-process labels to eliminate "Exercise N", "Round 1", "Low", "Chest" as standalone names
            PostProcessExerciseNames(setRows);

            var previewPath = Path.Combine(outDir, $"_debug_names_{stamp}.txt");
            File.WriteAllLines(previewPath, setRows.Select(r => r.ExerciseName));
            Console.WriteLine($"  (debug) {previewPath}");



            Console.WriteLine($"Parsed {sessions.Count} workout sessions.");
            Console.WriteLine("Output written:");
            Console.WriteLine($"  {workoutsPath}");
            Console.WriteLine($"  {setsPath}");


            Console.WriteLine($"Parsed {sessions.Count} workout sessions.");
            Console.WriteLine($"Output written:\n  {Path.Combine(dir, "workouts.csv")}\n  {Path.Combine(dir, "sets.csv")}");
        }

        // ---------------- Models ----------------
        record WorkoutSession(
            string SessionId,
            DateOnly? SessionDate,
            TimeOnly? StartTime,
            TimeOnly? EndTime,
            string Title,
            string SourceNote
        );

        record SetRow(
            string SessionId,
            int ExerciseOrder,
            string ExerciseName,
            int SetOrder,
            int? Reps,
            double? Weight,
            string Unit,
            bool IsAmrap,
            string Notes
        );

        // ---------------- Chunking / Filters ----------------
        static List<string> ChunkBlocks(string[] lines)
        {
            var blocks = new List<string>();
            var sb = new StringBuilder();

            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();

                if (string.IsNullOrWhiteSpace(line) || YouSaid.IsMatch(line))
                {
                    if (sb.Length > 0)
                    {
                        blocks.Add(sb.ToString().Trim());
                        sb.Clear();
                    }
                    if (YouSaid.IsMatch(line))
                    {
                        var stripped = YouSaid.Replace(line, "");
                        if (!string.IsNullOrWhiteSpace(stripped))
                            sb.AppendLine(stripped);
                    }
                    continue;
                }

                sb.AppendLine(line);
            }
            if (sb.Length > 0) blocks.Add(sb.ToString().Trim());
            return blocks;
        }

        static bool IsCompletedWorkoutBlock(string block)
        {
            var hasHeaderHint = WorkoutHeaderHint.IsMatch(block);
            var hasTimeRange = TimeRange.IsMatch(block);
            var looksLikeExercises = block.Split('\n').Count(l =>
                ExerciseLine.IsMatch(l) || Regex.IsMatch(l, @"\b\d+\s*[xX]\s*\d+|\bAMRAP\b|\b(lbs?|kg|@)\b")) >= 2;

            var isPlan = PlanCue.IsMatch(block) && !Regex.IsMatch(block, @"\bhere\s+(is|was)\b.*\bworkout\b", RegexOptions.IgnoreCase);

            return (looksLikeExercises || hasTimeRange || hasHeaderHint) && !isPlan;
        }

        // ---------------- Header Parsing ----------------
        static (DateOnly? date, TimeOnly? start, TimeOnly? end, string title) ParseHeader(string block)
        {
            DateOnly? date = null;
            TimeOnly? t1 = null;
            TimeOnly? t2 = null;
            string title = "";

            var dmatch = DateCue.Match(block);
            if (dmatch.Success)
            {
                int year = DateTime.Now.Year;
                if (dmatch.Groups["m"].Success)
                {
                    date = TryMakeDate(dmatch.Groups["m"].Value, dmatch.Groups["d"].Value, year);
                }
                else if (dmatch.Groups["tm"].Success)
                {
                    date = TryMakeDate(dmatch.Groups["tm"].Value, dmatch.Groups["td"].Value, year);
                }
            }

            var tmatch = TimeRange.Match(block);
            if (tmatch.Success)
            {
                t1 = TryParseTime(tmatch.Groups["t1"].Value);
                t2 = TryParseTime(tmatch.Groups["t2"].Value);
            }

            var firstLine = block.Split('\n').Select(s => s.Trim()).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
            title = Regex.Replace(firstLine, @"^(Here\s+(is|was).*)", "Workout", RegexOptions.IgnoreCase);

            return (date, t1, t2, title);
        }

        static DateOnly? TryMakeDate(string monthName, string dayStr, int year)
        {
            if (!int.TryParse(dayStr, out var d)) return null;
            try
            {
                var month = DateTime.ParseExact(monthName, "MMMM", System.Globalization.CultureInfo.InvariantCulture).Month;
                return new DateOnly(year, month, d);
            }
            catch { return null; }
        }

        static TimeOnly? TryParseTime(string hhmm)
        {
            if (TimeOnly.TryParse(hhmm, out var t)) return t;
            return null;
        }

        // ---------------- Exercise + Set parsing ----------------
        class ExerciseParsed
        {
            public string Name { get; }
            public List<SetParsed> Sets { get; }
            public string Notes { get; }

            public ExerciseParsed(string name, List<SetParsed> sets, string notes)
            {
                Name = name;
                Sets = sets;
                Notes = notes;
            }
        }

        class SetParsed
        {
            public int? Reps { get; }
            public double? Weight { get; }
            public string Unit { get; }
            public bool IsAmrap { get; }
            public string Notes { get; }

            public SetParsed(int? reps, double? weight, string unit, bool isAmrap, string notes)
            {
                Reps = reps;
                Weight = weight;
                Unit = unit;
                IsAmrap = isAmrap;
                Notes = notes;
            }
        }

        static List<ExerciseParsed> ParseExercises(string block)
        {
            // First normalize raw lines (bullets removed, name+qualifiers glued, parens closed when split)
            var lines = CreateNormalizedLines(block.Split('\n'));

            var exercises = new List<ExerciseParsed>();
            string? currentName = null;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || IsJunkLine(line))
                    continue;

                // Case 1: "Name: sets" on the same line
                var m = ExerciseLine.Match(line);
                if (m.Success)
                {
                    var name = m.Groups["name"].Value.Trim();
                    var setsBlob = m.Groups["sets"].Value;

                    currentName = name; // latch the name

                    var (sets, notes) = ParseSetsBlob(setsBlob);
                    if (sets.Count == 0 && !string.IsNullOrWhiteSpace(setsBlob)) notes = (notes + " " + setsBlob).Trim();

                    exercises.Add(new ExerciseParsed(currentName, sets, notes));
                    continue;
                }

                // Case 2: Name-only line (no sets yet) -> just update currentName, wait for sets
                if (LooksLikeNameOnly(line))
                {
                    // fold pure qualifier words into the currentName if we already have one
                    if (IsPureQualifier(line) && !string.IsNullOrEmpty(currentName))
                        currentName = MergeQualifierInto(currentName!, line);
                    else
                        currentName = line;

                    continue;
                }

                // Case 3: Sets-only line -> attach to most recent currentName
                if (LooksLikeSets(line))
                {
                    // If we somehow don't have a currentName yet, keep line as notes with a generic name,
                    // but prefer not to emit "Exercise 1". Instead, skip or attach to a placeholder that we try to fix later.
                    var name = string.IsNullOrEmpty(currentName) ? "Unknown Exercise" : currentName;

                    var (sets, notes) = ParseSetsBlob(line);
                    exercises.Add(new ExerciseParsed(name, sets, notes));
                    continue;
                }

                // Otherwise ignore commentary
            }

            return exercises;
        }

        // parse tokens in a "sets blob" and return a list of sets and any leftover notes
        static (List<SetParsed> sets, string notes) ParseSetsBlob(string setsBlob)
        {
            var sets = new List<SetParsed>();
            var notes = new StringBuilder();

            foreach (Match sm in SetToken.Matches(setsBlob))
            {
                if (sm.Value.Equals("AMRAP", StringComparison.OrdinalIgnoreCase))
                {
                    sets.Add(new SetParsed(null, null, "", true, ""));
                    continue;
                }

                int? reps = null;
                double? weight = null;
                string unit = "";
                bool isAmrap = false;

                if (int.TryParse(sm.Groups["reps"].Value, out var r1)) reps = r1;
                if (int.TryParse(sm.Groups["onlyreps"].Value, out var rOnly)) reps = rOnly;
                if (int.TryParse(sm.Groups["reps2"].Value, out var r2)) reps = r2;

                if (double.TryParse(sm.Groups["weight"].Value, out var w1)) weight = w1;
                if (double.TryParse(sm.Groups["wonly"].Value, out var w2)) weight = w2;

                unit = FirstNonEmpty(sm.Groups["unit"].Value, sm.Groups["uonly"].Value);
                if (sm.Groups["amrap"].Success) isAmrap = true;

                if (reps.HasValue || weight.HasValue || isAmrap)
                    sets.Add(new SetParsed(reps, weight, unit, isAmrap, ""));
                else
                    notes.Append(sm.Value).Append(' ');
            }

            return (sets, notes.ToString().Trim());
        }



        // Common exercise keywords so a line like "Pullups" is treated as a valid name
        static readonly HashSet<string> ExerciseVocab = new(StringComparer.OrdinalIgnoreCase)
{
    "Pullup","Pullups","Pulldown","Pulldowns","Lat Pulldown","Rows","Row","Seal Rows","Pendlay",
    "Press","Shoulder Press","Overhead Press","OHP","Incline","Bench","Dips","Fly","Flys","Flyes",
    "Pec Deck","Facepull","Facepulls","Lateral Raise","Lateral Raises","Rear Delt","Shrugs","Trap Raises",
    "Curls","Farmer","Farmer's Carries","Farmers Carries","Ab Roller","Arnold Press","Cable Row","Low Cable Row"
};

        static bool LooksLikeSets(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            var s = line.Trim();

            // throw away obvious non-sets that fooled us
            if (Regex.IsMatch(s, @"^\s*sets?\s*$", RegexOptions.IgnoreCase)) return false;

            return
                // 3 x 12 / 3x12 patterns
                Regex.IsMatch(s, @"\b\d+\s*[xX]\s*\d+\b") ||
                // weights or units
                Regex.IsMatch(s, @"@\s*\d+(?:\.\d+)?\s*(lbs?|kg|s)\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(s, @"\b\d+(?:\.\d+)?\s*(lbs?|kg|s)\b", RegexOptions.IgnoreCase) ||
                // simple reps lists: 11, 8, 6
                Regex.IsMatch(s, @"\b\d+\s*,\s*\d+(?:\s*,\s*\d+)+\b") ||
                // AMRAP tokens
                Regex.IsMatch(s, @"\bAMRAP\b", RegexOptions.IgnoreCase);
        }

        static bool LooksLikeNameOnly(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            var stripped = Regex.Replace(line, @"^[\-\u2022•]+\s*", ""); // remove bullets

            // junk headers we don't want as names
            if (IsJunkLine(stripped)) return false;

            // must start with a letter; not look like sets
            if (!Regex.IsMatch(stripped, @"^[A-Za-z][A-Za-z0-9\s/\-&\(\)']+$")) return false;
            if (LooksLikeSets(stripped)) return false;

            // looks like an exercise phrase
            return ExerciseVocab.Any(k => stripped.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                   || Regex.IsMatch(stripped, @"\b(press|row|pulldown|pullup|curl|raise|fly|dips?|shrugs?|roller|deck|crunch|knee|upright|rows?)\b", RegexOptions.IgnoreCase);
        }

        static bool IsJunkLine(string line)
        {
            var s = (line ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return true;

            // obvious non-exercise chatter
            if (Regex.IsMatch(s, @"^here\s+is\s+what\s+i\s+did", RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(s, @"^worked\s+out\s+from\b", RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(s, @"^today\s+is\b|^here\s+is\s+what\s+i\s+did\s+on\b", RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(s, @"^\s*sets?\s*$", RegexOptions.IgnoreCase)) return true;

            // pure qualifier headers (Chest / Low / High / Wide / Neutral / Single / One / Straight / Behind)
            if (IsPureQualifier(s)) return true;

            return false;
        }


        // Normalize raw lines so "Name" on one line + "sets" on the next become "Name: sets" on one line.
        // Normalize raw lines into "Name: sets" where possible, glue qualifiers/continuations
        static List<string> CreateNormalizedLines(IEnumerable<string> rawLines)
        {
            // Trim and drop empties; strip leading bullets
            var lines = rawLines
                .Select(s => Regex.Replace(s.Trim(), @"^[\-\u2022•]+\s*", ""))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var normalized = new List<string>();
            string? pendingName = null;

            int i = 0;
            while (i < lines.Count)
            {
                var cur = lines[i];

                // If we have a pending name with unclosed '(' → keep appending lines until closed
                if (!string.IsNullOrEmpty(pendingName) && ParensUnclosed(pendingName))
                {
                    pendingName = pendingName + " " + cur;
                    i++;
                    continue;
                }

                // Name-only line?
                if (LooksLikeNameOnly(cur))
                {
                    // start/replace pending name
                    pendingName = string.IsNullOrEmpty(pendingName) ? cur : pendingName + " " + cur;

                    // Merge following qualifier lines into the name
                    int j = i + 1;
                    while (j < lines.Count)
                    {
                        var peek = lines[j];
                        if (IsPureQualifier(peek))
                        {
                            pendingName = $"{pendingName} ({peek})";
                            j++;
                            continue;
                        }
                        // If parens are unclosed, append lines until they close
                        if (ParensUnclosed(pendingName))
                        {
                            pendingName = pendingName + " " + peek;
                            j++;
                            continue;
                        }
                        break;
                    }

                    // If the next line after qualifiers looks like sets, merge to "Name: sets"
                    if (j < lines.Count && LooksLikeSets(lines[j]))
                    {
                        normalized.Add($"{pendingName}: {lines[j].Trim()}");
                        pendingName = null;
                        i = j + 1;
                        continue;
                    }
                    else
                    {
                        // No immediate sets; keep the name pending in case sets come later
                        i = j;
                        continue;
                    }
                }

                // Sets-only line? Attach to pending name if available
                if (LooksLikeSets(cur))
                {
                    if (!string.IsNullOrEmpty(pendingName))
                    {
                        normalized.Add($"{pendingName}: {cur}");
                        pendingName = null;
                    }
                    else
                    {
                        // No name in scope — keep as-is; parser will fallback (rare)
                        normalized.Add(cur);
                    }
                    i++;
                    continue;
                }

                // Pure qualifier while no pending name → ignore (section headers like "Chest", "Low", etc.)
                if (IsPureQualifier(cur))
                {
                    i++;
                    continue;
                }

                // If we reach here, keep line verbatim (could be commentary; parser may ignore)
                normalized.Add(cur);
                i++;
            }

            // If a name remained pending without sets, keep it as a note-only exercise line
            if (!string.IsNullOrEmpty(pendingName))
                normalized.Add(pendingName);

            return normalized;
        }


        // Words that are usually *modifiers* of the previous exercise, not exercises themselves
        static readonly HashSet<string> Qualifiers = new(StringComparer.OrdinalIgnoreCase)
{
    "low","high","wide","narrow","neutral","single","one","straight","behind","chest",
    "round 1","round 2","round 3","round 4","set 1","set 2","set 3","set 4","superset","giant set","finisher"
};

        static readonly Regex PlaceholderNameRx = new(@"^Exercise\s+\d+$", RegexOptions.IgnoreCase);
        static readonly Regex RoundSetHeaderRx = new(@"^(round|set)\s*\d+$", RegexOptions.IgnoreCase);

        static bool IsPlaceholderOrQualifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;

            // Plain placeholders like "Exercise 1"
            if (PlaceholderNameRx.IsMatch(name)) return true;

            // Round/Set headers like "Round 1"
            if (RoundSetHeaderRx.IsMatch(name)) return true;

            // Common single-word/section qualifiers that shouldn't stand alone
            if (Qualifiers.Contains(name.Trim())) return true;

            // Very short fragments that are almost surely qualifiers
            if (!name.Contains(' ') && name.Length <= 8 &&
                Regex.IsMatch(name, @"^(low|high|wide|narrow|neutral|single|one|straight|behind|chest)$", RegexOptions.IgnoreCase))
                return true;

            return false;
        }

        // Optional: turn “Wide”, “Neutral”, etc. into a parenthetical modifier
        static string MergeQualifierInto(string baseName, string qualifier)
        {
            if (string.IsNullOrWhiteSpace(qualifier)) return baseName;
            qualifier = qualifier.Trim();
            // Avoid duplicating if already present
            if (baseName.IndexOf(qualifier, StringComparison.OrdinalIgnoreCase) >= 0) return baseName;
            // Add as a paren suffix
            return $"{baseName} ({qualifier})";
        }


        static bool IsPureQualifier(string line)
        {
            var s = line.Trim();
            if (string.IsNullOrEmpty(s)) return false;

            // tolerate short fragments and common qualifiers
            if (Qualifiers.Contains(s)) return true;

            // Allow “Round 1”, “Set 2” variants
            if (Regex.IsMatch(s, @"^(round|set)\s*\d+$", RegexOptions.IgnoreCase)) return true;

            // Single-word short fragments like "Low", "Wide", "Neutral", "Single", "Straight", "Behind"
            if (!s.Contains(' ') && s.Length <= 8 &&
                Regex.IsMatch(s, @"^(low|high|wide|narrow|neutral|single|one|straight|behind|chest)$", RegexOptions.IgnoreCase))
                return true;

            return false;
        }

        static bool ParensUnclosed(string text)
        {
            int bal = 0;
            foreach (var c in text)
            {
                if (c == '(') bal++;
                if (c == ')') bal--;
            }
            return bal > 0;
        }

        static bool LooksExerciseish(string line)
    => Regex.IsMatch(line, @"^\s*(\d+[\).\s]+)?[A-Za-z].*")
       && (
            Regex.IsMatch(line, @"\b\d+\s*[xX]\s*\d+\b") ||
            Regex.IsMatch(line, @"\b(lbs?|kg|@)\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(line, @"\bAMRAP\b", RegexOptions.IgnoreCase) ||
            // explicit name: sets pattern or name with modifiers
            Regex.IsMatch(line, @"^[A-Za-z].+[:\-–]\s*.+\d") ||
            ExerciseVocab.Any(k => line.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
          );



        static string FallbackName(string line, int seq) => $"Exercise {seq}";
        static string FirstNonEmpty(params string[] xs) => xs.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";

        // Safely shorten long strings for CSV fields (adds an ellipsis)
        static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }


        // ---------------- CSV writers ----------------

        // If a file already exists, add (_1), (_2), … before the extension
        static string GetUniquePath(string basePath)
        {
            if (!File.Exists(basePath)) return basePath;

            string dir = Path.GetDirectoryName(basePath)!;
            string file = Path.GetFileNameWithoutExtension(basePath);
            string ext = Path.GetExtension(basePath);
            int i = 1;

            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{file}_{i}{ext}");
                i++;
            } while (File.Exists(candidate));

            return candidate;
        }
        // Sweep through set rows and replace placeholder/qualifier names with last real name.
        // Also folds simple qualifiers into the previous name.
        static void PostProcessExerciseNames(List<SetRow> rows)
        {
            // Process in-session order so “last real name” makes sense
            rows.Sort((a, b) =>
            {
                int s = string.Compare(a.SessionId, b.SessionId, StringComparison.Ordinal);
                if (s != 0) return s;
                int e = a.ExerciseOrder.CompareTo(b.ExerciseOrder);
                if (e != 0) return e;
                return a.SetOrder.CompareTo(b.SetOrder);
            });

            string? lastSession = null;
            string? lastRealName = null;

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];

                // New session → reset tracker
                if (r.SessionId != lastSession)
                {
                    lastSession = r.SessionId;
                    lastRealName = null;
                }

                var name = r.ExerciseName?.Trim() ?? "";

                // If placeholder/qualifier/unknown, adopt the last real name if available
                if (string.IsNullOrEmpty(name)
                    || IsPlaceholderOrQualifier(name)
                    || name.Equals("Unknown Exercise", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(lastRealName))
                    {
                        rows[i] = r with { ExerciseName = lastRealName };
                    }
                    // else: leave as-is; indicates a sets-first oddity at the very start of a session
                }
                else
                {
                    // Real exercise name → update tracker
                    lastRealName = name;
                }
            }
        }




        static void WriteWorkoutsCsv(string path, List<WorkoutSession> sessions)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("session_id,session_date,start_time,end_time,title,source_note");
            foreach (var s in sessions)
            {
                w.WriteLine(string.Join(",",
                    Csv(s.SessionId),
                    Csv(s.SessionDate?.ToString("yyyy-MM-dd") ?? ""),
                    Csv(s.StartTime?.ToString("HH:mm") ?? ""),
                    Csv(s.EndTime?.ToString("HH:mm") ?? ""),
                    Csv(s.Title),
                    Csv(s.SourceNote)));
            }
        }

        static void WriteSetsCsv(string path, List<SetRow> rows)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("session_id,exercise_order,exercise_name,set_order,reps,weight,unit,amrap,notes");
            foreach (var r in rows)
            {
                w.WriteLine(string.Join(",",
                    Csv(r.SessionId),
                    r.ExerciseOrder,
                    Csv(r.ExerciseName),
                    r.SetOrder,
                    r.Reps?.ToString() ?? "",
                    r.Weight?.ToString() ?? "",
                    Csv(r.Unit),
                    r.IsAmrap ? "true" : "false",
                    Csv(r.Notes)));
            }
        }

        static string Csv(string s)
        {
            s ??= "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }

    }
}
