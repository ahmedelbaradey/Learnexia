'use client';

/**
 * TrendLine — themed multi-series line chart wrapper for admin dashboards.
 *
 * Used by:
 *   - P7-11 ai-safety/SafetyTrend (per-day safety event buckets, 4 series)
 *   - P7-11 ai-safety/TutorUsage (daily cost trend, single series)
 *
 * Styled via admin CSS-variable tokens exclusively — no Recharts default colors.
 * Recharts must be ^2 (React 18.3.1 compatible — NOT v3).
 * Accessible: each chart includes a visually hidden <table> fallback for screen readers.
 */

import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from 'recharts';

export interface TrendLineSeries {
  /** The `dataKey` used in the data records. */
  key: string;
  /** Display name for the legend. */
  name: string;
  /** Line stroke color — use admin token-aligned hex values. */
  color: string;
}

export interface TrendLineProps {
  /** Array of data records. Must have a `date` key + one key per series. */
  data: Array<{ date: string; [seriesKey: string]: number | string }>;
  /** Series definitions — each maps a key to a name + color. */
  series: TrendLineSeries[];
  /** Chart height in px. Default 260. */
  height?: number;
  /** Formats the `date` label on the X axis. Default: slice to "MM-DD". */
  dateFormatter?: (d: string) => string;
  /** Label shown when data is empty. */
  emptyLabel?: string;
  /** data-testid on the wrapper div. */
  testId?: string;
  /** Caption text for the SR-only accessible table. */
  accessibleCaption?: string;
}

const emptyStyle: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  color: 'var(--lx-fg3)',
  fontSize: 13,
  fontFamily: 'inherit',
};

export function TrendLine({
  data,
  series,
  height = 260,
  dateFormatter,
  emptyLabel = 'No data',
  testId,
  accessibleCaption,
}: TrendLineProps) {
  if (data.length === 0) {
    return (
      <div data-testid={testId} style={{ ...emptyStyle, height }}>
        {emptyLabel}
      </div>
    );
  }

  const formatDate = dateFormatter ?? ((d: string) => d.slice(5)); // "MM-DD"

  return (
    <div data-testid={testId}>
      <ResponsiveContainer width="100%" height={height}>
        <LineChart data={data} margin={{ top: 8, right: 16, left: 0, bottom: 4 }}>
          <CartesianGrid
            stroke="var(--lx-border)"
            strokeDasharray="4 4"
          />
          <XAxis
            dataKey="date"
            tickFormatter={formatDate}
            tick={{ fill: 'var(--lx-fg3)', fontSize: 11, fontFamily: 'Poppins, sans-serif' }}
            axisLine={false}
            tickLine={false}
          />
          <YAxis
            tick={{ fill: 'var(--lx-fg3)', fontSize: 11, fontFamily: 'Poppins, sans-serif' }}
            axisLine={false}
            tickLine={false}
            allowDecimals={false}
            width={44}
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
          />
          <Legend
            wrapperStyle={{
              fontSize: 12,
              color: 'var(--lx-fg3)',
              fontFamily: 'Poppins, sans-serif',
            }}
          />
          {series.map((s) => (
            <Line
              key={s.key}
              dataKey={s.key}
              name={s.name}
              stroke={s.color}
              strokeWidth={2}
              dot={false}
              activeDot={{ r: 4, strokeWidth: 0 }}
            />
          ))}
        </LineChart>
      </ResponsiveContainer>

      {/* SR-only accessible table fallback */}
      <table className="sr-only">
        {accessibleCaption && (
          <caption className="sr-only">{accessibleCaption}</caption>
        )}
        <thead>
          <tr>
            <th scope="col">Date</th>
            {series.map((s) => (
              <th key={s.key} scope="col">{s.name}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.map((row) => (
            <tr key={row.date}>
              <td>{row.date}</td>
              {series.map((s) => (
                <td key={s.key}>{row[s.key]}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
