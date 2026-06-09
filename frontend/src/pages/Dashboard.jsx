import { useEffect, useState } from 'react'
import Sidebar from '../components/Sidebar'
import api from '../api/client'
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
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

const ShiftCard = ({ shift, data }) => (
  <div className="shift-card">
    <div className="shift-header">Shift {shift}</div>
    <div className="shift-grid">
      <div className="shift-row"><span>WS 33200</span><strong>{data.ws33200}</strong></div>
      <div className="shift-row"><span>WS 23800</span><strong>{data.ws23800}</strong></div>
      <div className="shift-row"><span>Test Cell</span><strong>{data.testCell}</strong></div>
      <div className="shift-row"><span>Rework</span><strong>{data.rework}</strong></div>
      <div className="shift-row"><span>Engines</span><strong>{data.engines}</strong></div>
      <div className="shift-row"><span>Events</span><strong>{data.events}</strong></div>
    </div>
  </div>
)

const EMPTY_SHIFT = { ws33200: 0, ws23800: 0, testCell: 0, rework: 0, engines: 0, events: 0 }

export default function Dashboard() {
  const [activeTab, setActiveTab] = useState('hourly')
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    api.get('/Dashboard/overview')
      .then(res => setData(res.data))
      .catch(() => setError('Backend se data nahi mila. Kya server localhost:5000 pe chal raha hain?'))
      .finally(() => setLoading(false))
  }, [])

  const dateLabel = data?.date
    ? new Date(data.date).toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })
    : ''

  const kpis = data?.kpis
  const shifts = data?.shifts || { a: EMPTY_SHIFT, b: EMPTY_SHIFT, c: EMPTY_SHIFT }
  const hourly = data?.hourly || []
  const daily = data?.daily || []

  return (
    <div className="dash-root">
      <Sidebar />
      <div className="dash-main">

        {/* Top Bar */}
        <div className="dash-topbar">
          <div className="dash-topbar-left">
            <span className="dash-page-title">Production Dashboard</span>
            <span className="dash-date">{dateLabel}</span>
          </div>
          <div className="dash-topbar-right">
            <span className="dash-live-dot" />
            <span className="dash-live-text">Live</span>
          </div>
        </div>

        <div className="dash-content">

          {loading && <div className="section-title">Loading…</div>}
          {error && <div className="section-title" style={{ color: '#EF7A70' }}>{error}</div>}

          {!loading && !error && kpis && (
            <>
              {/* KPI Cards */}
              <div className="kpi-row">
                <KPICard label="Engines Today"  value={kpis.enginesToday}  sub="assembly"   color="#4CAF50" />
                <KPICard label="Events Today"   value={kpis.eventsToday}    sub="scans"      color="#2196F3" />
                <KPICard label="In Test Cell"   value={kpis.inTestCell}     sub="today"      color="#FF9800" />
                <KPICard label="Unique Serials" value={kpis.uniqueSerialsAll} sub="all time" color="#9C27B0" />
                <KPICard label="Workstations"   value={kpis.workstations}   sub="active"     color="#F44336" />
                <KPICard label="Total Records"  value={kpis.totalRecords.toLocaleString('en-IN')} sub="in DB" color="#00BCD4" />
              </div>

              {/* Shift Summary */}
              <div className="section-title">Shift Summary — {dateLabel} (Assembly)</div>
              <div className="shift-row-grid">
                <ShiftCard shift="A" data={shifts.a} />
                <ShiftCard shift="B" data={shifts.b} />
                <ShiftCard shift="C" data={shifts.c} />
              </div>

              {/* Charts */}
              <div className="section-title">Production Charts</div>
              <div className="chart-tabs">
                <button className={`chart-tab ${activeTab === 'hourly' ? 'active' : ''}`} onClick={() => setActiveTab('hourly')}>Hourly</button>
                <button className={`chart-tab ${activeTab === 'daily' ? 'active' : ''}`} onClick={() => setActiveTab('daily')}>Daily</button>
              </div>

              <div className="chart-box">
                {activeTab === 'hourly' && (
                  <>
                    <div className="chart-heading">Hourly Engines — {dateLabel}</div>
                    <ResponsiveContainer width="100%" height={280}>
                      <BarChart data={hourly} margin={{ top: 10, right: 20, left: 0, bottom: 0 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                        <XAxis dataKey="hour" tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} label={{ value: 'Hour', position: 'insideBottom', offset: -2, fill: 'rgba(255,255,255,0.3)', fontSize: 11 }} />
                        <YAxis tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} />
                        <Tooltip contentStyle={{ background: '#1e1e1e', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#fff' }} />
                        <Legend wrapperStyle={{ color: 'rgba(255,255,255,0.6)', fontSize: 12 }} />
                        <Bar dataKey="ws33200" name="WS 33200" fill="#2196F3" radius={[4,4,0,0]} />
                        <Bar dataKey="ws23800" name="WS 23800" fill="#FF9800" radius={[4,4,0,0]} />
                      </BarChart>
                    </ResponsiveContainer>
                  </>
                )}

                {activeTab === 'daily' && (
                  <>
                    <div className="chart-heading">Daily Engines Produced</div>
                    <ResponsiveContainer width="100%" height={280}>
                      <BarChart data={daily} margin={{ top: 10, right: 20, left: 0, bottom: 0 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                        <XAxis dataKey="date" tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} />
                        <YAxis tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} />
                        <Tooltip contentStyle={{ background: '#1e1e1e', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#fff' }} />
                        <Bar dataKey="engines" name="Engines" fill="#4CAF50" radius={[4,4,0,0]} />
                      </BarChart>
                    </ResponsiveContainer>
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
