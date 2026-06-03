import Sidebar from '../components/Sidebar'
import {
  dashboardKPIs,
  shiftData,
  hourlyData,
  wipLocations,
  monthlyData,
} from '../data/mockData'
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
  Tooltip, Legend, ResponsiveContainer, LineChart, Line
} from 'recharts'
import { useState } from 'react'
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
      <div className="shift-row"><span>Old Line</span><strong>{data.oldLine}</strong></div>
      <div className="shift-row"><span>New Line</span><strong>{data.newLine}</strong></div>
      <div className="shift-row"><span>Test Cycle</span><strong>{data.testCycle}</strong></div>
      <div className="shift-row"><span>FES</span><strong>{data.fes}</strong></div>
      <div className="shift-row"><span>Dispatched</span><strong>{data.dispatched}</strong></div>
      <div className="shift-row"><span>Test OK</span><strong>{data.testOK}</strong></div>
    </div>
  </div>
)

export default function Dashboard() {
  const [activeTab, setActiveTab] = useState('hourly')
  const today = new Date().toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric'
  })

  return (
    <div className="dash-root">
      <Sidebar />
      <div className="dash-main">

        {/* Top Bar */}
        <div className="dash-topbar">
          <div className="dash-topbar-left">
            <span className="dash-page-title">Production Dashboard</span>
            <span className="dash-date">{today}</span>
          </div>
          <div className="dash-topbar-right">
            <span className="dash-live-dot" />
            <span className="dash-live-text">Live</span>
          </div>
        </div>

        <div className="dash-content">

          {/* KPI Cards */}
          <div className="kpi-row">
            <KPICard label="Production Today"  value={dashboardKPIs.productionToday} sub="engines"  color="#4CAF50" />
            <KPICard label="WIP Count"         value={dashboardKPIs.wipCount}        sub="in plant" color="#2196F3" />
            <KPICard label="FES Done"          value={dashboardKPIs.fesCount}        sub="today"    color="#FF9800" />
            <KPICard label="Test OK"           value={dashboardKPIs.testOK}          sub="cleared"  color="#9C27B0" />
            <KPICard label="Dispatched"        value={dashboardKPIs.dispatched}      sub="today"    color="#F44336" />
            <KPICard label="Active Models"     value={dashboardKPIs.activeModels}    sub="variants" color="#00BCD4" />
          </div>

          {/* Shift Summary Cards */}
          <div className="section-title">Shift Summary — {today}</div>
          <div className="shift-row-grid">
            <ShiftCard shift="A" data={shiftData.A} />
            <ShiftCard shift="B" data={shiftData.B} />
            <ShiftCard shift="C" data={shiftData.C} />
          </div>

          {/* Charts Section */}
          <div className="section-title">Production Charts</div>
          <div className="chart-tabs">
            {['hourly', 'monthly', 'wip'].map(tab => (
              <button
                key={tab}
                className={`chart-tab ${activeTab === tab ? 'active' : ''}`}
                onClick={() => setActiveTab(tab)}
              >
                {tab === 'hourly'   ? 'Hourly'         : ''}
                {tab === 'monthly'  ? 'Monthly'        : ''}
                {tab === 'wip'      ? 'WIP Locations'  : ''}
              </button>
            ))}
          </div>

          <div className="chart-box">
            {activeTab === 'hourly' && (
              <>
                <div className="chart-heading">Hourly Production — Today</div>
                <ResponsiveContainer width="100%" height={280}>
                  <BarChart data={hourlyData} margin={{ top: 10, right: 20, left: 0, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                    <XAxis dataKey="hour" tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} label={{ value: 'Hour', position: 'insideBottom', offset: -2, fill: 'rgba(255,255,255,0.3)', fontSize: 11 }} />
                    <YAxis tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} />
                    <Tooltip contentStyle={{ background: '#1e1e1e', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#fff' }} />
                    <Legend wrapperStyle={{ color: 'rgba(255,255,255,0.6)', fontSize: 12 }} />
                    <Bar dataKey="oldLine" name="Old Line" fill="#2196F3" radius={[4,4,0,0]} />
                    <Bar dataKey="newLine" name="New Line" fill="#FF9800" radius={[4,4,0,0]} />
                  </BarChart>
                </ResponsiveContainer>
              </>
            )}

            {activeTab === 'monthly' && (
              <>
                <div className="chart-heading">Monthly Production — 2026</div>
                <ResponsiveContainer width="100%" height={280}>
                  <BarChart data={monthlyData} margin={{ top: 10, right: 20, left: 0, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                    <XAxis dataKey="month" tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} />
                    <YAxis tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} />
                    <Tooltip contentStyle={{ background: '#1e1e1e', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#fff' }} />
                    <Legend wrapperStyle={{ color: 'rgba(255,255,255,0.6)', fontSize: 12 }} />
                    <Bar dataKey="production" name="Total Production" fill="#4CAF50" radius={[4,4,0,0]} />
                  </BarChart>
                </ResponsiveContainer>
              </>
            )}

            {activeTab === 'wip' && (
              <>
                <div className="chart-heading">WIP by Location — Current</div>
                <ResponsiveContainer width="100%" height={280}>
                  <BarChart
                    data={wipLocations}
                    layout="vertical"
                    margin={{ top: 10, right: 30, left: 90, bottom: 0 }}
                  >
                    <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                    <XAxis type="number" tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} />
                    <YAxis dataKey="location" type="category" tick={{ fill: 'rgba(255,255,255,0.6)', fontSize: 11 }} width={88} />
                    <Tooltip contentStyle={{ background: '#1e1e1e', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#fff' }} />
                    <Bar dataKey="count" name="WIP Count" fill="#2196F3" radius={[0,4,4,0]} />
                  </BarChart>
                </ResponsiveContainer>
              </>
            )}
          </div>

        </div>
      </div>
    </div>
  )
}