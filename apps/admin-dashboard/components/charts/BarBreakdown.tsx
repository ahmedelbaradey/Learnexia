'use client';

/**
 * BarBreakdown — themed bar chart wrapper for admin dashboards.
 *
 * Used by:
 *   - P7-10 analytics/KpiBreakdownCharts (subject/grade/language)
 *   - P7-11 ai-safety/SafetySignals (action/reasonCode/modelId/taskKind)
 *   - P7-11 ai-safety/TutorUsage (byModel/byTaskKind)
 *
 * Styled via admin CSS-variable tokens exclusively — no Recharts default colors.
 * Recharts must be ^2 (React 18.3.1 compatible — NOT v3).
 * Accessible: each chart includes a visually hidden <table> fallback for screen readers.
 */

import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';

export interface BarBreakdownDataPoint {
  name: string;
  value: number;
}

export interface BarBreakdownProps {
  data: BarBreakdownDataPoint[];
  /** Chart height in px. Default 220. */
  height?: number;
  /** Bar fill color. Default `var(--lx-primary)` (#4F46E5). */
  barColor?: string;
  /** Label shown when data is empty or all values are 0. */
  emptyLabel?: string;
  /** data-testid on the wrapper div. */
  testId?: string;
  /** Caption text for the SR-only accessible table. */
  accessibleCaption?: string;
}

const shimmerStyle: React.CSSProperties = {
  height: 220,
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  color: 'var(--lx-fg3)',
  fontSize: 13,
  fontFamily: 'inherit',
};

/**
 * Checks whether the data set is meaningfully empty (no items or all zero values).
 */
function isDataEmpty(data: BarBreakdownDataPoint[]): boolean {
  if (data.length === 0) return true;
  return data.every((d) => d.value === 0);
}

export function BarBreakdown({
  data,
  height = 220,
  barColor = 'var(--lx-primary)',
  emptyLabel = 'No data',
  testId,
  accessibleCaption,
}: BarBreakdownProps) {
  if (isDataEmpty(data)) {
    return (
      <div
        data-testid={testId}
        style={{ ...shimmerStyle, height }}
      >
        {emptyLabel}
      </div>
    );
  }

  return (
    <div data-testid={testId}>
      <ResponsiveContainer width="100%" height={height}>
        <BarChart data={data} margin={{ top: 4, right: 8, left: 0, bottom: 4 }}>
          <CartesianGrid
            vertical={false}
            stroke="var(--lx-border)"
            strokeDasharray="4 4"
          />
          <XAxis
            dataKey="name"
            tick={{ fill: 'var(--lx-fg3)', fontSize: 12, fontFamily: 'Poppins, sans-serif' }}
            axisLine={false}
            tickLine={false}
          />
          <YAxis
            tick={{ fill: 'var(--lx-fg3)', fontSize: 12, fontFamily: 'Poppins, sans-serif' }}
            axisLine={false}
            tickLine={false}
            allowDecimals={false}
            width={50}
          />
          <Tooltip
            contentStyle={{
              backgroundColor: 'var(--lx-card)',
              border: '1px solid var(--lx-border)',
              borderRadius: 8,
              fontSize: 13,
              fontFamily: 'Poppins, sans-serif',
              color: 'var(--lx-fg1)',
            }}
            cursor={{ fill: 'rgba(79,70,229,0.08)' }}
          />
          <Bar
            dataKey="value"
            fill={barColor}
            radius={[4, 4, 0, 0]}
            maxBarSize={48}
          />
        </BarChart>
      </ResponsiveContainer>

      {/* SR-only accessible table fallback */}
      <table className="sr-only">
        {accessibleCaption && (
          <caption className="sr-only">{accessibleCaption}</caption>
        )}
        <thead>
          <tr>
            <th scope="col">Name</th>
            <th scope="col">Value</th>
          </tr>
        </thead>
        <tbody>
          {data.map((row) => (
            <tr key={row.name}>
              <td>{row.name}</td>
              <td>{row.value}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
