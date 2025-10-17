using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.Analysis;
using Plotly.NET;
using Plotly.NET.LayoutObjects;
using Plotly.NET.TraceObjects;

namespace WorkoutParserApp
{
    public class Visualizer
    {
        public static void GenerateVisualizations(string setsPath, string workoutsPath, string outputDir)
        {
            Console.WriteLine("Loading data...");
            var setsDF = DataFrame.LoadCsv(setsPath);
            var workoutsDF = DataFrame.LoadCsv(workoutsPath);

            Console.WriteLine($"Loaded {setsDF.Rows.Count} sets from {workoutsDF.Rows.Count} workouts");

            Directory.CreateDirectory(outputDir);

            // 1. Top 10 exercises by total volume (sets × reps × weight)
            Console.WriteLine("\n1. Generating top exercises by volume chart...");
            GenerateTopExercisesByVolume(setsDF, outputDir);

            // 2. Workout frequency over time
            Console.WriteLine("2. Generating workout frequency chart...");
            GenerateWorkoutFrequency(workoutsDF, outputDir);

            // 3. Volume by exercise over time (for top 5 exercises)
            Console.WriteLine("3. Generating exercise progression chart...");
            GenerateExerciseProgression(setsDF, workoutsDF, outputDir);

            // 4. Sets per workout distribution
            Console.WriteLine("4. Generating sets distribution chart...");
            GenerateSetsDistribution(setsDF, outputDir);

            // 5. Top exercises by frequency (how many workouts they appear in)
            Console.WriteLine("5. Generating exercise frequency chart...");
            GenerateExerciseFrequency(setsDF, outputDir);

            Console.WriteLine($"\nAll visualizations saved to: {outputDir}");
            Console.WriteLine("Open the .html files in your browser to view interactive charts!");
        }

        static void GenerateTopExercisesByVolume(DataFrame setsDF, string outputDir)
        {
            var exerciseVolume = new Dictionary<string, double>();

            for (long i = 0; i < setsDF.Rows.Count; i++)
            {
                var exerciseName = setsDF["exercise_name"][i]?.ToString() ?? "";
                var reps = ParseDouble(setsDF["reps"][i]);
                var weight = ParseDouble(setsDF["weight"][i]);

                if (string.IsNullOrEmpty(exerciseName)) continue;

                var volume = reps * weight;
                if (!exerciseVolume.ContainsKey(exerciseName))
                    exerciseVolume[exerciseName] = 0;
                exerciseVolume[exerciseName] += volume;
            }

            var topExercises = exerciseVolume
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList();

            var exercises = topExercises.Select(x => x.Key).ToArray();
            var volumes = topExercises.Select(x => x.Value).ToArray();

            var chart = Chart2D.Chart.Bar<string, double, string>(
                keys: exercises,
                values: volumes
            );

            chart = chart.WithTitle("Top 10 Exercises by Total Volume (reps × weight)")
                        .WithXAxisStyle(Title.init("Exercise"))
                        .WithYAxisStyle(Title.init("Total Volume"));

            var path = Path.Combine(outputDir, "top_exercises_by_volume.html");
            GenericChart.toChartHTML(chart).SaveHtml(path);
        }

        static void GenerateWorkoutFrequency(DataFrame workoutsDF, string outputDir)
        {
            var dateCounts = new Dictionary<string, int>();

            for (long i = 0; i < workoutsDF.Rows.Count; i++)
            {
                var dateStr = workoutsDF["session_date"][i]?.ToString();
                if (string.IsNullOrEmpty(dateStr)) continue;

                if (DateTime.TryParse(dateStr, out var date))
                {
                    var weekKey = GetWeekKey(date);
                    if (!dateCounts.ContainsKey(weekKey))
                        dateCounts[weekKey] = 0;
                    dateCounts[weekKey]++;
                }
            }

            var sortedDates = dateCounts.OrderBy(x => x.Key).ToList();
            var weeks = sortedDates.Select(x => x.Key).ToArray();
            var counts = sortedDates.Select(x => x.Value).ToArray();

            var chart = Chart2D.Chart.Column<string, int, string>(
                keys: weeks,
                values: counts
            );

            chart = chart.WithTitle("Workouts Per Week")
                        .WithXAxisStyle(Title.init("Week"))
                        .WithYAxisStyle(Title.init("Number of Workouts"));

            var path = Path.Combine(outputDir, "workout_frequency.html");
            GenericChart.toChartHTML(chart).SaveHtml(path);
        }

        static void GenerateExerciseProgression(DataFrame setsDF, DataFrame workoutsDF, string outputDir)
        {
            // Build a session_id -> date lookup
            var sessionDates = new Dictionary<string, DateTime>();
            for (long i = 0; i < workoutsDF.Rows.Count; i++)
            {
                var sessionId = workoutsDF["session_id"][i]?.ToString() ?? "";
                var dateStr = workoutsDF["session_date"][i]?.ToString();
                if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
                {
                    sessionDates[sessionId] = date;
                }
            }

            // Calculate volume per exercise per session
            var exerciseSessionVolume = new Dictionary<string, Dictionary<string, double>>();

            for (long i = 0; i < setsDF.Rows.Count; i++)
            {
                var sessionId = setsDF["session_id"][i]?.ToString() ?? "";
                var exerciseName = setsDF["exercise_name"][i]?.ToString() ?? "";
                var reps = ParseDouble(setsDF["reps"][i]);
                var weight = ParseDouble(setsDF["weight"][i]);

                if (string.IsNullOrEmpty(exerciseName) || !sessionDates.ContainsKey(sessionId)) continue;

                var volume = reps * weight;

                if (!exerciseSessionVolume.ContainsKey(exerciseName))
                    exerciseSessionVolume[exerciseName] = new Dictionary<string, double>();

                if (!exerciseSessionVolume[exerciseName].ContainsKey(sessionId))
                    exerciseSessionVolume[exerciseName][sessionId] = 0;

                exerciseSessionVolume[exerciseName][sessionId] += volume;
            }

            // Find top 5 exercises by total volume
            var topExercises = exerciseSessionVolume
                .Select(x => new { Exercise = x.Key, TotalVolume = x.Value.Values.Sum() })
                .OrderByDescending(x => x.TotalVolume)
                .Take(5)
                .Select(x => x.Exercise)
                .ToList();

            // Create a line for each exercise
            var traces = new List<GenericChart>();

            foreach (var exercise in topExercises)
            {
                var sessionVolumes = exerciseSessionVolume[exercise]
                    .Where(x => sessionDates.ContainsKey(x.Key))
                    .Select(x => new { Date = sessionDates[x.Key], Volume = x.Value })
                    .OrderBy(x => x.Date)
                    .ToList();

                var dates = sessionVolumes.Select(x => x.Date).ToArray();
                var volumes = sessionVolumes.Select(x => x.Volume).ToArray();

                var trace = Chart2D.Chart.Line<DateTime, double, string>(
                    x: dates,
                    y: volumes,
                    Name: exercise
                );

                traces.Add(trace);
            }

            var chart = Chart.Combine(traces);
            chart = chart.WithTitle("Exercise Volume Progression Over Time (Top 5 Exercises)")
                        .WithXAxisStyle(Title.init("Date"))
                        .WithYAxisStyle(Title.init("Volume (reps × weight)"));

            var path = Path.Combine(outputDir, "exercise_progression.html");
            GenericChart.toChartHTML(chart).SaveHtml(path);
        }

        static void GenerateSetsDistribution(DataFrame setsDF, string outputDir)
        {
            var setsPerWorkout = new Dictionary<string, int>();

            for (long i = 0; i < setsDF.Rows.Count; i++)
            {
                var sessionId = setsDF["session_id"][i]?.ToString() ?? "";
                if (string.IsNullOrEmpty(sessionId)) continue;

                if (!setsPerWorkout.ContainsKey(sessionId))
                    setsPerWorkout[sessionId] = 0;
                setsPerWorkout[sessionId]++;
            }

            var setCounts = setsPerWorkout.Values.ToArray();

            var chart = Chart2D.Chart.Histogram<int, int, int>(
                X: setCounts
            );

            chart = chart.WithTitle("Distribution of Sets Per Workout")
                        .WithXAxisStyle(Title.init("Number of Sets"))
                        .WithYAxisStyle(Title.init("Number of Workouts"));

            var path = Path.Combine(outputDir, "sets_distribution.html");
            GenericChart.toChartHTML(chart).SaveHtml(path);
        }

        static void GenerateExerciseFrequency(DataFrame setsDF, string outputDir)
        {
            var exerciseWorkouts = new Dictionary<string, HashSet<string>>();

            for (long i = 0; i < setsDF.Rows.Count; i++)
            {
                var sessionId = setsDF["session_id"][i]?.ToString() ?? "";
                var exerciseName = setsDF["exercise_name"][i]?.ToString() ?? "";

                if (string.IsNullOrEmpty(exerciseName)) continue;

                if (!exerciseWorkouts.ContainsKey(exerciseName))
                    exerciseWorkouts[exerciseName] = new HashSet<string>();
                exerciseWorkouts[exerciseName].Add(sessionId);
            }

            var topExercises = exerciseWorkouts
                .Select(x => new { Exercise = x.Key, Count = x.Value.Count })
                .OrderByDescending(x => x.Count)
                .Take(15)
                .ToList();

            var exercises = topExercises.Select(x => x.Exercise).ToArray();
            var counts = topExercises.Select(x => x.Count).ToArray();

            var chart = Chart2D.Chart.Bar<string, int, string>(
                keys: exercises,
                values: counts
            );

            chart = chart.WithTitle("Top 15 Most Frequent Exercises (by # of workouts)")
                        .WithXAxisStyle(Title.init("Exercise"))
                        .WithYAxisStyle(Title.init("Number of Workouts"));

            var path = Path.Combine(outputDir, "exercise_frequency.html");
            GenericChart.toChartHTML(chart).SaveHtml(path);
        }

        static double ParseDouble(object? value)
        {
            if (value == null) return 0;
            var str = value.ToString();
            if (string.IsNullOrWhiteSpace(str)) return 0;
            if (double.TryParse(str, out var result)) return result;
            return 0;
        }

        static string GetWeekKey(DateTime date)
        {
            var startOfYear = new DateTime(date.Year, 1, 1);
            var weekNumber = (date.DayOfYear - 1) / 7 + 1;
            return $"{date.Year}-W{weekNumber:D2}";
        }
    }
}
