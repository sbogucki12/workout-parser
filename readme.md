# workout-parser

Parse a mixed AI/user workout chat log into clean, analysis-ready CSVs you can drop into Power BI / Tableau / pandas. Built in C# to refresh core skills while prepping data for BI.

## Why
I had a long, messy conversation where the AI suggests workouts and I reply with what I actually did. I want the **completed workouts only**, in a tidy schema that supports analytics and visualization.

## What it does
- **Chunks** the transcript into message-sized blocks.
- **Filters** out AI “plans” and keeps **completed workout logs**.
- **Parses** exercises + sets (reps, weight, units, AMRAP, notes).
- **Normalizes** oddities: bullets, qualifiers (“Wide/Low/Neutral”), lines split by parentheses, sets-on-next-line.
- **Post-processes labels** so “Exercise 1 / Set 1 / Round 1” get replaced with the last real exercise name.
- **Outputs timestamped CSVs** to an `output/` folder:
  - `workouts_YYYYMMDD_HHMMSS.csv`
  - `sets_YYYYMMDD_HHMMSS.csv`
- Includes a `_debug_names_*.txt` so you can quickly audit naming quality.

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

## Roadmap / ideas

Recognize superset/giant set blocks and explode child exercises.

Calculate session volume Σ(reps × weight) per exercise/day.

Add unit tests with sample blocks.

Optional Power BI starter report or a small web UI to browse sessions.

## Requirements

.NET 8 SDK

Windows/Mac/Linux

## Disclaimer

Heuristics may need tuning for your exact transcript style. Adjust vocabulary/qualifiers as your logging evolves.