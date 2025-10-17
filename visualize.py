#!/usr/bin/env python3
"""
Workout Data Visualizer

Generates interactive HTML visualizations from cleaned workout CSV data.

Usage:
    python visualize.py [sets_csv_path]

If no path is provided, uses: output/sets_20251017_clean.csv

Requirements:
    pip install pandas plotly
"""

import sys
import os
from pathlib import Path
from datetime import datetime
import pandas as pd
import plotly.express as px
import plotly.graph_objects as go
from plotly.subplots import make_subplots


def load_data(sets_path):
    """Load and prepare the workout data."""
    print(f"Loading data from {sets_path}...")

    sets_df = pd.read_csv(sets_path)
    print(f"Loaded {len(sets_df)} sets")

    # Try to load corresponding workouts file
    workouts_path = sets_path.replace("sets_", "workouts_").replace("_clean", "_20251017_113224")

    if os.path.exists(workouts_path):
        workouts_df = pd.read_csv(workouts_path)
        print(f"Loaded {len(workouts_df)} workouts")

        # Parse dates
        workouts_df['session_date'] = pd.to_datetime(workouts_df['session_date'])

        # Merge session dates into sets
        sets_df = sets_df.merge(
            workouts_df[['session_id', 'session_date']],
            on='session_id',
            how='left'
        )
    else:
        print(f"Warning: Workouts file not found at {workouts_path}")
        print("Time-based visualizations will be skipped.")
        workouts_df = None

    # Convert numeric columns
    sets_df['reps'] = pd.to_numeric(sets_df['reps'], errors='coerce').fillna(0)
    sets_df['weight'] = pd.to_numeric(sets_df['weight'], errors='coerce').fillna(0)

    # Calculate volume
    sets_df['volume'] = sets_df['reps'] * sets_df['weight']

    return sets_df, workouts_df


def generate_top_exercises_by_volume(sets_df, output_dir):
    """Generate bar chart of top 10 exercises by total volume."""
    print("\n1. Generating top exercises by volume chart...")

    exercise_volume = sets_df.groupby('exercise_name')['volume'].sum().sort_values(ascending=False)
    top_10 = exercise_volume.head(10)

    fig = go.Figure(data=[
        go.Bar(
            x=top_10.index,
            y=top_10.values,
            marker_color='indianred'
        )
    ])

    fig.update_layout(
        title='Top 10 Exercises by Total Volume (reps × weight)',
        xaxis_title='Exercise',
        yaxis_title='Total Volume',
        hovermode='x unified'
    )

    output_path = output_dir / 'top_exercises_by_volume.html'
    fig.write_html(output_path)
    print(f"   Saved: {output_path}")


def generate_workout_frequency(workouts_df, output_dir):
    """Generate chart showing workout frequency over time."""
    print("2. Generating workout frequency chart...")

    if workouts_df is None or 'session_date' not in workouts_df.columns:
        print("   Skipped: No date information available")
        return

    # Group by week
    workouts_df['week'] = workouts_df['session_date'].dt.to_period('W').astype(str)
    weekly_counts = workouts_df.groupby('week').size().reset_index(name='count')

    fig = go.Figure(data=[
        go.Bar(
            x=weekly_counts['week'],
            y=weekly_counts['count'],
            marker_color='lightseagreen'
        )
    ])

    fig.update_layout(
        title='Workouts Per Week',
        xaxis_title='Week',
        yaxis_title='Number of Workouts',
        hovermode='x unified'
    )

    output_path = output_dir / 'workout_frequency.html'
    fig.write_html(output_path)
    print(f"   Saved: {output_path}")


def generate_exercise_progression(sets_df, output_dir):
    """Generate line chart showing volume progression for top 5 exercises."""
    print("3. Generating exercise progression chart...")

    if 'session_date' not in sets_df.columns:
        print("   Skipped: No date information available")
        return

    # Find top 5 exercises by total volume
    top_exercises = sets_df.groupby('exercise_name')['volume'].sum().nlargest(5).index

    # Filter to top exercises
    top_df = sets_df[sets_df['exercise_name'].isin(top_exercises)].copy()

    # Group by exercise and date
    progression = top_df.groupby(['session_date', 'exercise_name'])['volume'].sum().reset_index()

    fig = px.line(
        progression,
        x='session_date',
        y='volume',
        color='exercise_name',
        title='Exercise Volume Progression Over Time (Top 5 Exercises)',
        labels={
            'session_date': 'Date',
            'volume': 'Volume (reps × weight)',
            'exercise_name': 'Exercise'
        }
    )

    fig.update_layout(hovermode='x unified')

    output_path = output_dir / 'exercise_progression.html'
    fig.write_html(output_path)
    print(f"   Saved: {output_path}")


def generate_sets_distribution(sets_df, output_dir):
    """Generate histogram showing distribution of sets per workout."""
    print("4. Generating sets distribution chart...")

    sets_per_workout = sets_df.groupby('session_id').size()

    fig = go.Figure(data=[
        go.Histogram(
            x=sets_per_workout.values,
            marker_color='mediumpurple',
            nbinsx=20
        )
    ])

    fig.update_layout(
        title='Distribution of Sets Per Workout',
        xaxis_title='Number of Sets',
        yaxis_title='Number of Workouts',
        showlegend=False
    )

    output_path = output_dir / 'sets_distribution.html'
    fig.write_html(output_path)
    print(f"   Saved: {output_path}")


def generate_exercise_frequency(sets_df, output_dir):
    """Generate bar chart showing top 15 most frequent exercises."""
    print("5. Generating exercise frequency chart...")

    # Count unique workouts per exercise
    exercise_frequency = sets_df.groupby('exercise_name')['session_id'].nunique().sort_values(ascending=False)
    top_15 = exercise_frequency.head(15)

    fig = go.Figure(data=[
        go.Bar(
            x=top_15.index,
            y=top_15.values,
            marker_color='coral'
        )
    ])

    fig.update_layout(
        title='Top 15 Most Frequent Exercises (by # of workouts)',
        xaxis_title='Exercise',
        yaxis_title='Number of Workouts',
        hovermode='x unified'
    )

    output_path = output_dir / 'exercise_frequency.html'
    fig.write_html(output_path)
    print(f"   Saved: {output_path}")


def generate_summary_stats(sets_df, workouts_df, output_dir):
    """Generate a summary dashboard with key statistics."""
    print("6. Generating summary dashboard...")

    # Calculate statistics
    total_workouts = sets_df['session_id'].nunique()
    total_sets = len(sets_df)
    total_exercises = sets_df['exercise_name'].nunique()
    total_volume = sets_df['volume'].sum()
    avg_sets_per_workout = total_sets / total_workouts if total_workouts > 0 else 0

    # Create subplots
    fig = make_subplots(
        rows=2, cols=2,
        subplot_titles=(
            'Total Volume by Exercise Type',
            'Average Reps by Exercise',
            'Weight Distribution',
            'Sets Over Time'
        ),
        specs=[[{'type': 'pie'}, {'type': 'bar'}],
               [{'type': 'histogram'}, {'type': 'scatter'}]]
    )

    # 1. Volume by exercise (pie chart - top 10)
    exercise_volume = sets_df.groupby('exercise_name')['volume'].sum().nlargest(10)
    fig.add_trace(
        go.Pie(labels=exercise_volume.index, values=exercise_volume.values, name='Volume'),
        row=1, col=1
    )

    # 2. Average reps by exercise (bar chart - top 10)
    avg_reps = sets_df.groupby('exercise_name')['reps'].mean().nlargest(10)
    fig.add_trace(
        go.Bar(x=avg_reps.index, y=avg_reps.values, name='Avg Reps', marker_color='lightblue'),
        row=1, col=2
    )

    # 3. Weight distribution (histogram)
    weights = sets_df[sets_df['weight'] > 0]['weight']
    fig.add_trace(
        go.Histogram(x=weights, name='Weight', marker_color='lightgreen'),
        row=2, col=1
    )

    # 4. Sets over time (if date available)
    if 'session_date' in sets_df.columns:
        daily_sets = sets_df.groupby('session_date').size().reset_index(name='count')
        fig.add_trace(
            go.Scatter(x=daily_sets['session_date'], y=daily_sets['count'],
                      mode='lines+markers', name='Sets', marker_color='orange'),
            row=2, col=2
        )

    fig.update_layout(
        title_text=f'Workout Summary Dashboard<br><sub>Total Workouts: {total_workouts} | Total Sets: {total_sets} | '
                   f'Unique Exercises: {total_exercises} | Avg Sets/Workout: {avg_sets_per_workout:.1f}</sub>',
        showlegend=False,
        height=800
    )

    output_path = output_dir / 'summary_dashboard.html'
    fig.write_html(output_path)
    print(f"   Saved: {output_path}")


def main():
    # Determine input file path
    if len(sys.argv) > 1:
        sets_path = Path(sys.argv[1])
    else:
        # Default path
        sets_path = Path(__file__).parent / 'output' / 'sets_20251017_clean.csv'

    if not sets_path.exists():
        print(f"Error: Sets file not found: {sets_path}")
        print(f"\nUsage: python {Path(__file__).name} [path/to/sets.csv]")
        sys.exit(1)

    # Create output directory
    output_dir = sets_path.parent / 'visualizations'
    output_dir.mkdir(exist_ok=True)

    # Load data
    sets_df, workouts_df = load_data(str(sets_path))

    # Generate visualizations
    print(f"\nGenerating visualizations in: {output_dir}")
    print("=" * 60)

    generate_top_exercises_by_volume(sets_df, output_dir)
    generate_workout_frequency(workouts_df, output_dir)
    generate_exercise_progression(sets_df, output_dir)
    generate_sets_distribution(sets_df, output_dir)
    generate_exercise_frequency(sets_df, output_dir)
    generate_summary_stats(sets_df, workouts_df, output_dir)

    print("\n" + "=" * 60)
    print(f"✓ All visualizations saved to: {output_dir}")
    print("\nOpen the .html files in your browser to view interactive charts!")
    print("\nGenerated files:")
    for html_file in sorted(output_dir.glob('*.html')):
        print(f"  - {html_file.name}")


if __name__ == '__main__':
    main()
