import { useEffect, useState, useCallback } from 'react'
import Sidebar from '../components/Sidebar'
import api from '../api/client'
import {
  BarChart, Bar, LineChart, Line, XAxis, YAxis, CartesianGrid,
  Tooltip, Legend, ResponsiveContainer
} from 'recharts'
import './Dashboard.css'

const KPICard = ({ label, value, sub, color }) => (
  <div className="kpi-card" style={{ borderTopColor: color }}>
    <div className="kpi-value" style={{ color }}>{value}</div>
    <div className="kpi-label">{label}</div>
    {sub && <div className="kpi-sub">{sub}</div>}
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

// Series colors - KPI cards ke saath consistent (Old=green, New=blue, Test=orange)
const LINE_COLORS = { oldLine: '#4CAF50', newLine: '#2196F3', testCell: '#FF9800' }

const SERIES = [
  { key: 'oldLine', name: 'Old Line', color: LINE_COLORS.oldLine },
  { key: 'newLine', name: 'New Line', color: LINE_COLORS.newLine },
  { key: 'testCell', name: 'Test Cell', color: LINE_COLORS.testCell },
]

// Reusable chart - 3 series (O/N/T). type='line' ya 'bar' (toggle se).
const ProdChart = ({ data, xKey, xLabel, type = 'bar', height = 300 }) => {
  const Chart = type === 'line' ? LineChart : BarChart
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
        {SERIES.map(s => type === 'line'
          ? <Line key={s.key} type="monotone" dataKey={s.key} name={s.name} stroke={s.color} strokeWidth={2} dot={false} isAnimationActive={false} />
          : <Bar key={s.key} dataKey={s.key} name={s.name} fill={s.color} radius={[3, 3, 0, 0]} isAnimationActive={false} />
        )}
      </Chart>
    </ResponsiveContainer>
  )
}

// Kaunse hours kis shift mein (IST). Per-shift hourly graph ke liye.
const SHIFT_HOURS = {
  A: [6, 7, 8, 9, 10, 11, 12, 13],
  B: [14, 15, 16, 17, 18, 19, 20, 21],
  C: [22, 23, 0, 1, 2, 3, 4, 5],
}

export default function Dashboard({ user, onLogout }) {
  const [activeTab, setActiveTab] = useState('hourly')
  const [chartType, setChartType] = useState('line') // 'line' | 'bar' - sab graphs pe
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [selectedDate, setSelectedDate] = useState(null) // null = latest day (server decides)
  const [trends, setTrends] = useState({ daily: [], monthly: [] })

  const load = useCallback((date) => {
    return api.get('/Dashboard/overview', { params: date ? { date } : {} })
      .then(res => { setData(res.data); setError('') })
      .catch(() => setError('Unable to load data. Please ensure the backend server is running on localhost:5000.'))
      .finally(() => setLoading(false))
  }, [])

  // Initial load + jab date change ho.
  useEffect(() => { load(selectedDate) }, [selectedDate, load])

  // Live: har 30s pe refresh (sirf selected-din ka overview - fast).
  useEffect(() => {
    const id = setInterval(() => load(selectedDate), 30000)
    return () => clearInterval(id)
  }, [selectedDate, load])

  // Trends (daily + monthly) heavy hain (poori history) - sirf ek baar page load pe,
  // live 30s refresh pe nahi. Isse 5-crore data pe bhi dashboard fast rehta hain.
  useEffect(() => {
    api.get('/Dashboard/trends').then(res => setTrends(res.data)).catch(() => {})
  }, [])

  const currentDay = data?.productionDay
  const minDate = data?.minDate
  const maxDate = data?.maxDate
  // Navigation 'selectedDate' (turant intent) pe based hain - currentDay (server echo)
  // async aata hain to uspe depend karne se rapid clicks race kar jaate the (+2/stuck bug).
  // Pehli load pe selectedDate null -> currentDay (latest) se chalu.
  const effDate = selectedDate || currentDay
  const goTo = (d) => { if (d) setSelectedDate(d) }
  const prevDisabled = !effDate || !minDate || effDate <= minDate
  const nextDisabled = !effDate || !maxDate || effDate >= maxDate
  const prev = () => !prevDisabled && goTo(addDays(effDate, -1))
  const next = () => !nextDisabled && goTo(addDays(effDate, 1))

  const dateLabel = fmtDate(effDate)
  const kpis = data?.kpis
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
            <span className="dash-live-dot" />
            <span className="dash-live-text">Live</span>
          </div>
        </div>

        <div className="dash-content">

          {loading && <div className="section-title">Loading…</div>}
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
                <KPICard label="Old Line QTY"  value={kpis.oldLine}   sub="assembly"   color="#4CAF50" />
                <KPICard label="New Line QTY"  value={kpis.newLine}   sub="assembly"   color="#2196F3" />
                <KPICard label="In Test Cell"  value={kpis.testCell}  sub="testing"    color="#FF9800" />
                <KPICard label="Paint Line"    value={kpis.paintLine} sub="upfitment"  color="#9C27B0" />
                <KPICard label="FES"           value={kpis.fes}       sub="completion" color="#F44336" />
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

              <div className="chart-box">
                {activeTab === 'hourly' && (
                  <>
                    <div className="chart-heading">Hourly Engines — {dateLabel} (06:00 to 06:00 IST)</div>
                    {hourly.length === 0
                      ? <div className="chart-empty">No hourly data available for this date.</div>
                      : <ProdChart data={hourly} xKey="hour" xLabel="Hour (IST)" type={chartType} />}
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
                          : <ProdChart data={hourlyFor(sh)} xKey="hour" type={chartType} height={210} />}
                      </div>
                    ))}
                  </>
                )}

                {activeTab === 'daily' && (
                  <>
                    <div className="chart-heading">Daily Engines (per production day)</div>
                    {daily.length === 0
                      ? <div className="chart-empty">No daily data available.</div>
                      : <ProdChart data={daily} xKey="date" type={chartType} />}
                  </>
                )}

                {activeTab === 'monthly' && (
                  <>
                    <div className="chart-heading">Monthly Engines (per month)</div>
                    {monthly.length === 0
                      ? <div className="chart-empty">No monthly data available.</div>
                      : <ProdChart data={monthly} xKey="month" type={chartType} />}
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
