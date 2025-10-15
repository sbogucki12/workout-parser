# workout-parser

Parse a mixed AI/user workout chat log into clean, analysis-ready CSVs you can drop into Power BI / Tableau / pandas. Built in C# to refresh core skills while prepping data for BI.

## Why
I had a long, messy conversation where the AI suggests workouts and I reply with what I actually did. I want the **completed workouts only**, in a tidy schema that supports analytics and visualization.

## What it does
- **Chunks** the transcript into message-sized blocks.
- **Filters** out AI "plans" and keeps **completed workout logs**.
- **Parses** exercises + sets with advanced regex patterns supporting:
  - Standard notation: `3 x 12 @ 100 lbs`
  - Dumbbell shorthand: `100s` or `100s 3 x 6`
  - Reps only: `12 reps`
  - AMRAP sets
  - Inline weights: `Rope pushdowns (75lbs) x 15`
- **Normalizes** oddities: bullets, qualifiers ("Wide/Low/Neutral"), lines split by parentheses, sets-on-next-line.
- **Standardizes exercise names** for consistent grouping:
  - Expands abbreviations: `DB` → `Dumbbell`, `BB` → `Barbell`
  - Unifies capitalization: Title Case
  - Maps variants to canonical names: `Incline DB press` → `Incline Dumbbell Press`
  - Removes parenthetical modifiers for cleaner grouping
- **Filters junk data**: Removes AI artifacts, "Unknown Exercise" entries, and rows without meaningful data.
- **Post-processes labels** so "Exercise 1 / Set 1 / Round 1" get replaced with the last real exercise name.
- **Outputs timestamped CSVs** to an `output/` folder:
  - `workouts_YYYYMMDD_HHMMSS.csv`
  - `sets_YYYYMMDD_HHMMSS.csv`
- Includes a `_debug_names_*.txt` so you can quickly audit naming quality.

## Recent improvements (Oct 2025)
- ✅ **Fixed "3 s" parsing bug**: No longer treats "3 sets" as weight=3, unit="s"
- ✅ **Exercise name normalization**: Reduced unique exercise names by 30% (388 → 273) through intelligent consolidation
- ✅ **Data quality filtering**: Removed 42% junk data (AI artifacts, invalid entries)
- ✅ **Improved data completeness**: 93% increase in rows with both reps AND weight (6.9% → 13.3%)
- ✅ **Enhanced pattern recognition**: Better handling of "100s 3 x 6" dumbbell notation

## Output schema

**workouts.csv**

session_id,session_date,start_time,end_time,title,source_note


**sets.csv**

session_id,exercise_order,exercise_name,set_order,reps,weight,unit,amrap,notes

## Running it (Visual Studio or CLI)
- **VS 2022 / .NET 8:** set the command-line arg to the full path of your log (e.g., `C:\Users\<you>\workoutlog_oct2025.txt`) and press F5.  
- **CLI:**
```bash
dotnet build
dotnet run -- "C:\full\path\to\workoutlog_oct2025.txt"
```

## File placement

Your log: anywhere. The program accepts a full path.

Outputs: output/ (created next to your input file) with timestamped filenames.

Key design choices

Heuristics over brittle parsing: readable regex + a small state machine; easy to tune as logs evolve.

Education-first: uses C# records, Regex, LINQ, and small refactors you can unit-test later.

Post-processors: pragmatic sweep to fix labels that slip through.

## Data quality metrics

After the October 2025 improvements, parsing a typical workout log yields:
- **271 workout sessions** identified
- **1,879 sets** with usable data (after filtering)
- **273 unique exercises** (consolidated from 388+ variants)
- **Top exercises tracked**: Pec Deck (201 sets), Dumbbell Shrugs (199 sets), Weighted Dips (59 sets), Incline Dumbbell Press (57 sets)

**Data completeness breakdown:**
- 13.3% of rows have both reps + weight (ideal for progress tracking)
- 66.3% have reps only (useful for volume analysis)
- 12.5% have weight only
- 8.0% metadata/notes only

**Visualization readiness:** ✅ Ready for Power BI/Tableau with proper exercise grouping and trend analysis capability.

## Roadmap / ideas

- [ ] Carry-forward missing values (e.g., copy weight from first set to subsequent sets)
- [ ] Parse notes field to extract structured data when primary parsing misses it
- [ ] Recognize superset/giant set blocks and explode child exercises
- [ ] Calculate session volume Σ(reps × weight) per exercise/day
- [ ] Add exercise variant column (preserve modifiers like "30 degree incline" as metadata)
- [ ] Add unit tests with sample blocks
- [ ] Optional Power BI starter report or a small web UI to browse sessions

## Requirements

.NET 8 SDK

Windows/Mac/Linux

## Disclaimer

Heuristics may need tuning for your exact transcript style. Adjust vocabulary/qualifiers as your logging evolves.