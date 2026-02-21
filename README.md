# BeerMoney Indicator

A NinjaTrader 8 indicator for order flow analysis with rolling window VWAP and volume profile, diagonal imbalances, EMA trend lines, per-bar value areas, divergent bar detection, and dual-timeframe order flow metrics. Includes a WebSocket dashboard for real-time market analysis.

## Requirements

- NinjaTrader 8
- **NinjaTrader OrderFlow+ package** (required for volumetric data access)
- Volumetric bars data feed (for order flow features)

![BeerMoney Indicator](screenshot.png)

## Features

### Multi-Timeframe VWAP Analysis

Two rolling VWAP lines calculated from configurable data series:

- **Bias VWAP** (Gold) - Slower VWAP with EMA smoothing for trend direction
- **Trigger VWAP** (Magenta) - Faster VWAP from volumetric bars for entry timing

The relationship between these VWAPs helps identify momentum and mean reversion opportunities.

### EMA Trend Lines

Two EMA lines plotted on the chart for trend identification:

- **Slow EMA** (DodgerBlue dashed) - Slower EMA (default period 9) for trend direction
- **Fast EMA** (LimeGreen dashed) - Faster EMA (default period 5) for entry timing

The spread between fast and slow EMAs, cross direction, and slope are computed as features and broadcast to the dashboard.

### Diagonal Imbalance Detection

Highlights aggressive buying and selling activity using the standard footprint chart diagonal imbalance formula:

- **Bullish Imbalance** (Green glow) - Ask volume at price significantly exceeds Bid volume one level below
- **Bearish Imbalance** (Red glow) - Bid volume at price significantly exceeds Ask volume one level above

**Volume-proportional scaling**: Both glow radius and opacity scale continuously based on actual volume at each level relative to the Reference Volume. Small imbalances get subtle glows while large imbalances get prominent ones, providing immediate visual feedback on institutional activity intensity.

**Real-time updates**: Imbalances are calculated on each tick, so you can see them form and unform as the current bar develops.

Configurable settings:

- `Imbalance Ratio` - Minimum ratio threshold (default: 3.0 = 300%)
- `Min Difference` - Minimum volume difference between levels
- `Reference Volume` - Volume level at which glows reach maximum size (continuous scaling)

### Divergent Bar Detection

Paints bars where price action diverges from order flow (delta):

- **Cyan bars** - Hidden accumulation: Positive delta but bearish price bar (buyers absorbing selling)
- **Magenta bars** - Hidden distribution: Negative delta but bullish price bar (sellers absorbing buying)

These divergences often precede reversals or continuation moves.

### Rolling Volume Profile

Displays a volume profile on the right side of the chart based on the rolling window of bias data:

- **POC** (Red line) - Point of Control, price with highest volume
- **VAH/VAL** (Yellow lines) - Value Area High/Low boundaries
- **Value Area** (Blue bars) - Price levels containing ~70% of volume
- **Outside VA** (Yellow bars) - Price levels outside the value area

The profile updates as new bars form, showing where volume is concentrated in the current market context.

### Per-Bar Value Area

Each completed trigger bar gets its own POC and value area overlay:

- **POC line** (Gold) - Point of Control for that individual bar
- **VA outline** (CornflowerBlue) - Value Area High/Low rectangle outline

This shows the volume distribution within each bar, helping identify where the most trading activity occurred at a micro level.

### Order Flow Metrics (Dual Timeframe)

Rolling-window metrics computed on both trigger (fast) and bias (slow) data series, giving dual-timeframe context. A single reusable `OrderFlowMetricsTracker` class is instantiated twice with a configurable lookback window (default 20 bars).

#### Rolling-Window Metrics (per timeframe)

| Group             | Metric                    | Description                                                                    |
| ----------------- | ------------------------- | ------------------------------------------------------------------------------ |
| **POC Migration** | `poc_migration`           | Current vs prior POC change / ATR                                              |
|                   | `poc_direction`           | +1 rising, -1 falling, 0 flat                                                  |
|                   | `poc_trend_strength`      | Consistency of direction over window (0-1)                                     |
| **Value Area**    | `va_overlap`              | Intersection/union of current vs prior bar VA (0-1)                            |
|                   | `va_migration`            | +1 migrating up, -1 down, 0 balanced                                           |
|                   | `va_width`                | Current VA width / ATR                                                         |
|                   | `is_compressing`          | True if VA widths narrowing over window                                        |
|                   | `compression_rate`        | Linear slope of VA widths (negative = compressing)                             |
| **Imbalance**     | `imbalance_polarity`      | (bull - bear) / (bull + bear) over window (-1 to +1)                           |
|                   | `is_polarized`            | True if \|polarity\| >= 0.4 (70/30 split)                                      |
|                   | `setup_density`           | Avg imbalance count per bar over window                                        |
| **VWAP**          | `vwap_slope`              | (current - oldest) / ATR                                                       |
|                   | `vwap_regime`             | +1 trending up, -1 down, 0 range                                               |
| **Delta**         | `rolling_delta`           | Sum of bar deltas over window / ATR                                            |
|                   | `rolling_delta_direction` | +1 net buying, -1 net selling, 0 neutral                                       |
|                   | `rolling_delta_momentum`  | Change in rolling delta vs prior bar                                           |
| **Volume**        | `volume_trend`            | Linear slope of volume over window (positive = increasing)                     |
| **Agreement**     | `poc_vwap_agreement`      | True if POC direction and VWAP regime match                                    |
| **Conviction**    | `conviction_score`        | Composite 0-6 (VWAP pos + POC dir + imbalance + VA migration + volume + delta) |
|                   | `conviction_direction`    | +1 bullish, -1 bearish, 0 mixed                                                |

#### Single-Bar Flags

| Flag                   | Description                                                               |
| ---------------------- | ------------------------------------------------------------------------- |
| `volume_skew`          | POC at extreme (top/bottom 20%) with one-sided imbalance volume dominance |
| `divergence_confirmed` | Bar is divergent AND imbalances stacked against trend at extreme          |

All metrics are broadcast via the dashboard WebSocket in the `metrics` section of each payload.

### Dashboard (WebSocket)

When enabled, the indicator starts a WebSocket server that broadcasts comprehensive JSON payloads on each completed trigger bar. The payload includes:

- Raw OHLC and volumetric bar data
- All F\_ features (EMA distances, spread, slope, cross, VWAP distances, imbalances)
- T* and B* enriched features (trigger rolling metrics, bias bar metrics, cluster support/resistance)
- Derived features (session progress, signed spread/slope/delta)
- Reference levels (VWAPs, EMAs, ATR, POC/VAH/VAL)
- Order flow metrics (dual-timeframe trigger/bias metrics + single-bar flags)
- Session context (day, time, progress)

Connect a dashboard client to `ws://127.0.0.1:{port}/ws/` to receive real-time data. New clients receive buffered history on connect.

## Data Series

The indicator uses 4 data series with configurable bar types (Tick, Minute, Second, Range, or Volume):

1. **Primary** - Your chart's bar type
2. **Base** - For ATR calculation (configurable bar type and period)
3. **Bias** (volumetric) - For slow VWAP, volume profile, and imbalance clusters (configurable bar type and period)
4. **Trigger** (volumetric) - For fast VWAP, imbalance detection, and per-bar value area (configurable bar type and period)

## Configuration

### Data Series Settings

| Property             | Default | Description                                          |
| -------------------- | ------- | ---------------------------------------------------- |
| Base Bars Type       | Tick    | Bar type for base series (Tick, Minute, Second, etc) |
| Base Period Size     | 1000    | Period size for base data series                     |
| Volumetric Bars Type | Tick    | Bar type for volumetric series (Bias and Trigger)    |
| Bias Period Size     | 2500    | Period size for bias VWAP and volume profile         |
| Trigger Period Size  | 500     | Period size for trigger VWAP and imbalances          |
| Ticks Per Level      | 4       | Volumetric aggregation (must match chart)            |
| Period               | 14      | Lookback period for calculations                     |
| Bias Smoothing       | 5       | EMA smoothing for bias VWAP                          |
| Cluster Lookback     | 5       | Rolling window for imbalance cluster tracking        |
| Cluster Bucket Size  | 2.0     | Price bucket size for cluster aggregation            |

### EMA Settings

| Property        | Default | Description             |
| --------------- | ------- | ----------------------- |
| Fast EMA Period | 5       | Period for fast EMA     |
| Slow EMA Period | 9       | Period for slow EMA     |
| Show EMA Lines  | true    | Plot EMA lines on chart |

### Imbalance Settings

| Property         | Default | Description                                                         |
| ---------------- | ------- | ------------------------------------------------------------------- |
| Show Imbalances  | true    | Enable diagonal imbalance glows                                     |
| Imbalance Ratio  | 3.0     | Minimum ratio (3.0 = 300%)                                          |
| Min Difference   | 10      | Minimum volume difference                                           |
| Opacity          | 0.6     | Glow transparency                                                   |
| Bullish Color    | Green   | Bullish imbalance glow color                                        |
| Bearish Color    | Red     | Bearish imbalance glow color                                        |
| Reference Volume | 150     | Volume level at which glows reach maximum size (continuous scaling) |

### Volume Profile Settings

| Property            | Default        | Description                   |
| ------------------- | -------------- | ----------------------------- |
| Show Volume Profile | true           | Enable volume profile display |
| Profile Width       | 150            | Width in pixels               |
| Value Area %        | 70             | Percentage for value area     |
| Profile Opacity     | 0.6            | Bar transparency              |
| Profile Color       | Yellow         | Bars outside value area       |
| Value Area Color    | CornflowerBlue | Bars inside value area        |
| POC Color           | Red            | Point of control line         |

### Bar Value Area Settings

| Property            | Default        | Description                               |
| ------------------- | -------------- | ----------------------------------------- |
| Show Bar Value Area | true           | Enable per-bar POC and VA overlay         |
| Value Area Opacity  | 0.8            | Opacity of the VA rectangle outline       |
| Width Padding       | 4              | Extra pixels on each side of POC/VA lines |
| Value Area Color    | CornflowerBlue | VA outline color                          |
| POC Color           | Gold           | Per-bar POC line color                    |

### Bar Colors

| Property          | Default | Description                   |
| ----------------- | ------- | ----------------------------- |
| Show Divergent    | true    | Enable divergent bar painting |
| Divergent Bullish | Cyan    | Hidden accumulation bars      |
| Divergent Bearish | Magenta | Hidden distribution bars      |

### Dashboard Settings

| Property         | Default | Description                          |
| ---------------- | ------- | ------------------------------------ |
| Enable Dashboard | true    | Start WebSocket server for dashboard |
| Dashboard Port   | 8422    | WebSocket port for dashboard         |

## Usage Tips

### Understanding the Components

1. **Bias VWAP (Gold)** - Represents the longer timeframe directional bias. This isn't a signal to only trade one direction, but context for understanding where the "fair value" sits on a slower timeframe.

2. **VWAP Diff** - The spread between Trigger and Bias VWAP. Consider trading when VWAPs are **further apart** rather than close together. A larger spread indicates stronger momentum and conviction. When VWAPs are compressed, the market is often in consolidation.

3. **EMA Spread** - The spread between Fast and Slow EMAs. A wider spread indicates a trending market; a compressed spread indicates consolidation or potential reversal.

4. **Base ATR** - Use as a conservative profit target. Since ATR represents average range, targeting slightly below ATR (e.g., 70-80%) increases the probability of hitting your target before a reversal.

5. **Volume Profile Levels** - POC, VAH, and VAL act as key reference points:
   - **POC** - Price magnet; often acts as equilibrium
   - **VAH** - Potential resistance when approaching from below
   - **VAL** - Potential support when approaching from above

6. **Imbalances** - Visible as volume-proportional glows. Larger glows indicate higher volume imbalances where institutional players are aggressively entering. Clusters of imbalances at a price level show strong support or resistance zones.

7. **Divergent Bars** - Hidden accumulation (cyan) or distribution (magenta) potentially precedes reversals.

8. **Per-Bar Value Area** - Shows where volume concentrated within each bar. A narrow VA with POC near the close suggests conviction; a wide VA suggests indecision.

9. **Order Flow Metrics** - Dual-timeframe rolling metrics provide context:
   - **Conviction Score** (0-6) - Higher scores indicate stronger directional agreement across all factors (VWAP, POC, delta, imbalances, VA migration, volume)
   - **VA Compression** - Narrowing value areas often precede breakouts
   - **Imbalance Polarity** - Persistent one-sided imbalances indicate institutional positioning
   - **POC-VWAP Agreement** - When both point the same direction, trend is healthy
   - **Volume Skew** - POC at bar extreme suggests the market may return to finish exploring those prices
   - Compare **trigger** (fast) vs **bias** (slow) metrics to gauge timeframe alignment

### Combining Signals

The real power comes from combining multiple signals:

#### Bullish Setup Example

- Price at or near **VAL** or below **POC**
- Large bullish imbalance glows appearing (bigger = more volume)
- Multiple **cyan bars** (hidden accumulation) - buyers absorbing selling pressure
- EMA spread starting to widen with fast EMA above slow EMA
- VWAP diff starting to spread positive

This combination suggests larger buying is occurring despite price weakness - a potential long opportunity.

#### Bearish Setup Example

- Price at or near **VAH** or above **POC**
- Large bearish imbalance glows appearing
- Multiple **magenta bars** (hidden distribution) - sellers absorbing buying pressure
- EMA spread widening with fast EMA below slow EMA
- VWAP diff spreading negative

#### Confluence Checklist

Before entering a trade, look for multiple confirming signals:

- [ ] Price at a volume profile level (POC/VAH/VAL)?
- [ ] Imbalances supporting your direction?
- [ ] Divergent bars showing hidden activity?
- [ ] VWAP diff spread (not compressed)?
- [ ] EMA spread confirming trend direction?
- [ ] Conviction score >= 4 with matching direction?
- [ ] Trigger and bias metrics in agreement?

The more boxes checked, the higher the probability setup.

### Target Setting

Use ATR as a guide for realistic targets:

- **Conservative**: 50-70% of ATR
- **Standard**: 80-100% of ATR
- **Aggressive**: 100%+ of ATR (only with strong confluence)

Consider volume profile levels as intermediate targets - if POC or VA boundary is closer than your ATR target, that may be a more realistic exit point.

## Installation

1. Download the latest release from https://github.com/WaleeTheRobot/beer-money/releases
2. Import the indicator into NinjaTrader
3. Add to chart
4. Configure Ticks Per Level to match your chart's volumetric settings
5. Adjust thresholds based on the instrument's typical volume

## Development

Copy the BeerMoney into the NinjaTrader AddOns and compile
