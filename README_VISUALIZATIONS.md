# Workout Data Visualizations

Python script to generate interactive HTML visualizations from your cleaned workout data.

## Quick Start

### 1. Install Dependencies

```bash
pip install pandas plotly
```

### 2. Run the Script

```bash
# Use default path (output/sets_20251017_clean.csv)
python visualize.py

# Or specify a custom path
python visualize.py path/to/your/sets.csv
```

### 3. View Results

Open the generated HTML files in your browser from the `output/visualizations/` directory.

## Generated Visualizations

The script creates 6 interactive HTML charts:

1. **top_exercises_by_volume.html** - Bar chart of your top 10 exercises by total volume (reps × weight)
2. **workout_frequency.html** - Weekly workout frequency over time
3. **exercise_progression.html** - Line chart showing volume progression for your top 5 exercises
4. **sets_distribution.html** - Histogram showing distribution of sets per workout
5. **exercise_frequency.html** - Top 15 most frequently performed exercises
6. **summary_dashboard.html** - Comprehensive dashboard with multiple metrics

## Features

- **Interactive Charts**: Hover over data points for details, zoom, pan, and more
- **Automatic Calculations**: Volume, frequencies, and trends computed automatically
- **Smart Defaults**: Works with your cleaned CSV data out of the box
- **Date-Aware**: Automatically includes time-based visualizations if date data is available

## Data Requirements

The script expects a CSV file with these columns:
- `session_id` - Unique identifier for each workout
- `exercise_name` - Name of the exercise
- `reps` - Number of repetitions
- `weight` - Weight used
- `unit` - Unit of measurement (optional)

If a corresponding `workouts_*.csv` file is found with `session_date`, time-based visualizations will be included.

## Example Output

All charts are saved as standalone HTML files that can be:
- Opened directly in any web browser
- Shared with others
- Embedded in web pages
- Saved for future reference

## Troubleshooting

**Error: Sets file not found**
- Ensure the CSV file path is correct
- Check that the file exists in the `output/` directory

**Missing time-based charts**
- Ensure the corresponding `workouts_*.csv` file exists
- Verify it contains a `session_date` column

**Import errors**
- Run: `pip install --upgrade pandas plotly`
