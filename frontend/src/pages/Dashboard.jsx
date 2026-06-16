import { useEffect, useState, useCallback } from 'react'
import Sidebar from '../components/Sidebar'
import Spinner from '../components/Spinner'
import api from '../api/client'
import {
  BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid,
  Tooltip, Legend, ResponsiveContainer
} from 'recharts'
import './Dashboard.css'

// Kal se compare badge (stocks board jaisa). pct: +ve up (green), -ve down (red), null = no data.
const ChangeBadge = ({ pct }) => {
  if (pct === null || pct === undefined) return <div className="kpi-change flat">—</div>
  const cls = pct > 0 ? 'up' : pct < 0 ? 'down' : 'flat'
  const arrow = pct > 0 ? '▲' : pct < 0 ? '▼' : '→'
  return (
    <div className={`kpi-change ${cls}`}>
      {arrow} {Math.abs(pct)}%
    </div>
  )
}

const KPICard = ({ label, value, sub, color, change }) => (
  <div className="kpi-card" style={{ borderTopColor: color }}>
    <div className="kpi-value" style={{ color }}>{value}</div>
    <div className="kpi-label">{label}</div>
    {sub && <div className="kpi-sub">{sub}</div>}
    <ChangeBadge pct={change} />
  </div>
)

const SHIFT_TIME = { A: '06:00 – 14:30', B: '14:30 – 22:30', C: '22:30 – 06:00' }
const EMPTY_SHIFT = { oldLine: 0, newLine: 0, testCell: 0, paintLine: 0, fes: 0, shipped: 0 }

const ShiftCard = ({ shift, data }) => (
  <div className="shift-card">
    <div className="shift-header">
      <span>Shift {shift}</span>
      <span className="shift-time">{SHIFT_TIME[shift]}</span>
    </div>
    <div className="shift-grid">
      <div className="shift-row"><span>Old Line</span><strong>{data.oldLine}</strong></div>
      <div className="shift-row"><span>New Line</span><strong>{data.newLine}</strong></div>
      <div className="shift-row"><span>Test Cell</span><strong>{data.testCell}</strong></div>
      <div className="shift-row"><span>Paint Line</span><strong>{data.paintLine}</strong></div>
      <div className="shift-row"><span>FES</span><strong>{data.fes}</strong></div>
      <div className="shift-row"><span>Shipped</span><strong>{data.shipped}</strong></div>
    </div>
  </div>
)

const fmtDate = (iso) =>
  iso ? new Date(iso + 'T00:00:00').toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' }) : ''

// yyyy-MM-dd mein n din add/subtract (timezone-safe - sirf local date parts,
// toISOString() UTC mein shift kar deta tha jisse IST +5:30 mein +-1 galat ho jaata).
const addDays = (iso, n) => {
  const [y, m, d] = iso.split('-').map(Number)
  const dt = new Date(y, m - 1, d + n)
  const mm = String(dt.getMonth() + 1).padStart(2, '0')
  const dd = String(dt.getDate()).padStart(2, '0')
  return `${dt.getFullYear()}-${mm}-${dd}`
}

// 5 series - colors KPI cards ke saath consistent.
const LINE_COLORS = {
  oldLine: '#4CAF50', newLine: '#2196F3', testCell: '#FF9800', paintLine: '#9C27B0', fes: '#F44336',
}
const SERIES = [
  { key: 'oldLine', name: 'Old Line', color: LINE_COLORS.oldLine },
  { key: 'newLine', name: 'New Line', color: LINE_COLORS.newLine },
  { key: 'testCell', name: 'Test Cell', color: LINE_COLORS.testCell },
  { key: 'paintLine', name: 'Paint Line', color: LINE_COLORS.paintLine },
  { key: 'fes', name: 'FES', color: LINE_COLORS.fes },
]

const avg = (data, key) =>
  data.length ? data.reduce((a, r) => a + (r[key] || 0), 0) / data.length : 0

// Reusable chart - sirf 'visible' series dikhata hain, line ya bar.
const ProdChart = ({ data, xKey, xLabel, type = 'bar', height = 300, visible }) => {
  const Chart = type === 'line' ? LineChart : BarChart
  const shown = SERIES.filter(s => visible[s.key])
  return (
    <ResponsiveContainer width="100%" height={height}>
      <Chart data={data} margin={{ top: 10, right: 20, left: 0, bottom: xLabel ? 6 : 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
        <XAxis
          dataKey={xKey}
          tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }}
          {...(xLabel ? { label: { value: xLabel, position: 'insideBottom', offset: -2, fill: 'rgba(255,255,255,0.3)', fontSize: 11 } } : {})}
        />
        <YAxis allowDecimals={false} tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} />
        <Tooltip contentStyle={{ background: '#1e1e1e', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#fff' }} />
        <Legend wrapperStyle={{ color: 'rgba(255,255,255,0.6)', fontSize: 12 }} />
        {shown.map(s => type === 'line'
          ? <Line key={s.key} type="monotone" dataKey={s.key} name={s.name} stroke={s.color} strokeWidth={2} dot={false} isAnimationActive={false} />
          : <Bar key={s.key} dataKey={s.key} name={s.name} fill={s.color} radius={[3, 3, 0, 0]} isAnimationActive={false} />
        )}
      </Chart>
    </ResponsiveContainer>
  )
}

// Chart + side averages panel (har visible series ka average).
const ChartWithAvg = ({ data, xKey, xLabel, type, height = 300, visible, avgUnit }) => (
  <div className="chart-with-avg">
    <div className="chart-area">
      <ProdChart data={data} xKey={xKey} xLabel={xLabel} type={type} height={height} visible={visible} />
    </div>
    <div className="avg-panel">
      <div className="avg-title">Average{avgUnit ? ` / ${avgUnit}` : ''}</div>
      {SERIES.filter(s => visible[s.key]).map(s => (
        <div key={s.key} className="avg-row">
          <span className="avg-dot" style={{ background: s.color }} />
          <span className="avg-name">{s.name}</span>
          <span className="avg-val">{avg(data, s.key).toFixed(1)}</span>
        </div>
      ))}
    </div>
  </div>
)

// Kaunse hours kis shift mein (IST). Per-shift hourly graph ke liye.
const SHIFT_HOURS = {
  A: [6, 7, 8, 9, 10, 11, 12, 13],
  B: [14, 15, 16, 17, 18, 19, 20, 21],
  C: [22, 23, 0, 1, 2, 3, 4, 5],
}

export default function Dashboard({ user, onLogout }) {
  const [activeTab, setActiveTab] = useState('hourly')
  const [chartType, setChartType] = useState('line') // 'line' | 'bar' - sab graphs pe
  // Kaunse series dikhane hain (checkbox se toggle). Default sab.
  const [visible, setVisible] = useState({
    oldLine: true, newLine: true, testCell: true, paintLine: true, fes: true,
  })
  const toggleSeries = (k) => setVisible(v => ({ ...v, [k]: !v[k] }))
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)   // initial load
  const [busy, setBusy] = useState(false)         // date change in-flight
  const [error, setError] = useState('')
  const [selectedDate, setSelectedDate] = useState(null) // null = latest day (server decides)
  const [trends, setTrends] = useState({ daily: [], monthly: [] })
  const [trendsLoading, setTrendsLoading] = useState(true)

  const load = useCallback((date) => {
    return api.get('/Dashboard/overview', { params: date ? { date } : {} })
      .then(res => { setData(res.data); setError('') })
      .catch(() => setError('Unable to load data. Please ensure the backend server is running on localhost:5000.'))
      .finally(() => { setLoading(false); setBusy(false) })
  }, [])

  // Initial load + jab date change ho.
  useEffect(() => { load(selectedDate) }, [selectedDate, load])

  // Live: har 30s pe refresh (sirf selected-din ka overview - fast).
  useEffect(() => {
    const id = setInterval(() => load(selectedDate), 30000)
    return () => clearInterval(id)
  }, [selectedDate, load])

  // Daily = last 30 din (saari history), Monthly = saara history. Ek baar fetch (cache 5 min backend pe).
  // trendsLoading already true se start hoti hain - effect me dobara set karne ki zaroorat nahi.
  useEffect(() => {
    api.get('/Dashboard/trends')
      .then(res => setTrends(res.data))
      .catch(() => {})
      .finally(() => setTrendsLoading(false))
  }, [])

  const currentDay = data?.productionDay
  const minDate = data?.minDate
  const maxDate = data?.maxDate
  // Navigation 'selectedDate' (turant intent) pe based hain - currentDay (server echo)
  // async aata hain to uspe depend karne se rapid clicks race kar jaate the (+2/stuck bug).
  // Pehli load pe selectedDate null -> currentDay (latest) se chalu.
  const effDate = selectedDate || currentDay
  const goTo = (d) => { if (d) { setBusy(true); setSelectedDate(d) } }
  const prevDisabled = !effDate || !minDate || effDate <= minDate
  const nextDisabled = !effDate || !maxDate || effDate >= maxDate
  const prev = () => !prevDisabled && goTo(addDays(effDate, -1))
  const next = () => !nextDisabled && goTo(addDays(effDate, 1))

  const dateLabel = fmtDate(effDate)
  const kpis = data?.kpis
  const cmp = data?.compare || {}   // kal se % change (stocks badge)
  const shifts = data?.shifts || { a: EMPTY_SHIFT, b: EMPTY_SHIFT, c: EMPTY_SHIFT }
  const hourly = data?.hourly || []
  const daily = trends?.daily || []
  const monthly = trends?.monthly || []
  // Per-shift hourly: hourly array ko us shift ke hours pe filter karo.
  const hourlyFor = (sh) => hourly.filter(h => SHIFT_HOURS[sh].includes(parseInt(h.hour, 10)))

  return (
    <div className="dash-root">
      <Sidebar user={user} onLogout={onLogout} />
      <div className="dash-main">

        {/* Top Bar */}
        <div className="dash-topbar">
          <div className="dash-topbar-left">
            <span className="dash-page-title">Production Dashboard</span>
            <span className="dash-date">{dateLabel}</span>
          </div>
          <div className="dash-topbar-right">
            {/* Date navigation - calendar (DB ke range tak) */}
            {effDate && (
              <div className="date-nav">
                <button className="date-arrow" onClick={prev} disabled={prevDisabled} title="Previous day">‹</button>
                <input
                  type="date"
                  className="date-select"
                  value={effDate}
                  min={minDate}
                  max={maxDate}
                  onChange={e => goTo(e.target.value)}
                />
                <button className="date-arrow" onClick={next} disabled={nextDisabled} title="Next day">›</button>
              </div>
            )}
            {busy && <Spinner small />}
            <span className="dash-live-dot" />
            <span className="dash-live-text">Live</span>
          </div>
        </div>

        <div className="dash-content">

          {loading && <Spinner label="Loading dashboard…" />}
          {error && <div className="section-title" style={{ color: '#EF7A70' }}>{error}</div>}

          {/* Selected date pe DB mein koi record nahi */}
          {!loading && !error && data && !data.hasData && (
            <div className="no-data-panel">
              <div className="no-data-icon">🗓️</div>
              <div className="no-data-title">No data available for this date</div>
              <div className="no-data-sub">No production records were found for {dateLabel}. Please select another date from the calendar.</div>
            </div>
          )}

          {!loading && !error && data?.hasData && kpis && (
            <>
              {/* KPI Cards */}
              <div className="kpi-row">
                <KPICard label="Old Line QTY"  value={kpis.oldLine}   sub="assembly"   color="#4CAF50" change={cmp.oldLine} />
                <KPICard label="New Line QTY"  value={kpis.newLine}   sub="assembly"   color="#2196F3" change={cmp.newLine} />
                <KPICard label="In Test Cell"  value={kpis.testCell}  sub="testing"    color="#FF9800" change={cmp.testCell} />
                <KPICard label="Paint Line"    value={kpis.paintLine} sub="upfitment"  color="#9C27B0" change={cmp.paintLine} />
                <KPICard label="FES"           value={kpis.fes}       sub="completion" color="#F44336" change={cmp.fes} />
              </div>

              {/* Shift Summary */}
              <div className="section-title">Shift Summary — {dateLabel} (Assembly)</div>
              <div className="shift-row-grid">
                <ShiftCard shift="A" data={shifts.a} />
                <ShiftCard shift="B" data={shifts.b} />
                <ShiftCard shift="C" data={shifts.c} />
              </div>

              {/* Charts - har chart mein O Line, N Line, T Line */}
              <div className="section-title">Production Charts</div>
              <div className="chart-tabs">
                <button className={`chart-tab ${activeTab === 'hourly' ? 'active' : ''}`} onClick={() => setActiveTab('hourly')}>Hourly</button>
                <button className={`chart-tab ${activeTab === 'shift' ? 'active' : ''}`} onClick={() => setActiveTab('shift')}>Shift</button>
                <button className={`chart-tab ${activeTab === 'daily' ? 'active' : ''}`} onClick={() => setActiveTab('daily')}>Daily</button>
                <button className={`chart-tab ${activeTab === 'monthly' ? 'active' : ''}`} onClick={() => setActiveTab('monthly')}>Monthly</button>

                {/* Line / Bar toggle - sab graphs pe lagta hain */}
                <div className="chart-type-toggle">
                  <button className={chartType === 'line' ? 'active' : ''} onClick={() => setChartType('line')}>Line</button>
                  <button className={chartType === 'bar' ? 'active' : ''} onClick={() => setChartType('bar')}>Bar</button>
                </div>
              </div>

              {/* Series select/unselect - jo select karoge wahi sab graphs mein dikhega */}
              <div className="series-toggle">
                {SERIES.map(s => (
                  <label key={s.key} className={`series-chk ${visible[s.key] ? 'on' : ''}`}>
                    <input type="checkbox" checked={visible[s.key]} onChange={() => toggleSeries(s.key)} />
                    <span className="series-dot" style={{ background: s.color }} />
                    {s.name}
                  </label>
                ))}
              </div>

              <div className="chart-box">
                {activeTab === 'hourly' && (
                  <>
                    <div className="chart-heading">Hourly Engines — {dateLabel} (06:00 to 06:00 IST)</div>
                    {hourly.length === 0
                      ? <div className="chart-empty">No hourly data available for this date.</div>
                      : <ChartWithAvg data={hourly} xKey="hour" xLabel="Hour (IST)" type={chartType} visible={visible} avgUnit="hour" />}
                  </>
                )}

                {/* Shift tab - har shift (A/B/C) ka alag graph (us shift ke hours) */}
                {activeTab === 'shift' && (
                  <>
                    <div className="chart-heading">Shift-wise Hourly Engines — {dateLabel}</div>
                    {['A', 'B', 'C'].map(sh => (
                      <div key={sh} className="shift-graph-block">
                        <div className="shift-graph-title">Shift {sh} <span>{SHIFT_TIME[sh]}</span></div>
                        {hourlyFor(sh).length === 0
                          ? <div className="chart-empty">No engines in Shift {sh}.</div>
                          : <ChartWithAvg data={hourlyFor(sh)} xKey="hour" type={chartType} height={210} visible={visible} avgUnit="hour" />}
                      </div>
                    ))}
                  </>
                )}

                {activeTab === 'daily' && (
                  <>
                    <div className="chart-heading">Daily Engines — last 30 days</div>
                    {trendsLoading
                      ? <Spinner label="Loading daily trend…" />
                      : daily.length === 0
                        ? <div className="chart-empty">No daily data available.</div>
                        : <ChartWithAvg data={daily} xKey="date" type={chartType} visible={visible} avgUnit="day" />}
                  </>
                )}

                {activeTab === 'monthly' && (
                  <>
                    <div className="chart-heading">Monthly Engines (all history)</div>
                    {trendsLoading
                      ? <Spinner label="Loading monthly trend…" />
                      : monthly.length === 0
                        ? <div className="chart-empty">No monthly data available.</div>
                        : <ChartWithAvg data={monthly} xKey="month" type={chartType} visible={visible} avgUnit="month" />}
                  </>
                )}
              </div>
            </>
          )}

        </div>
      </div>
    </div>
  )
}
