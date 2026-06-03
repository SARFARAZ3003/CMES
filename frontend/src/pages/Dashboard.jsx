import Sidebar from '../components/Sidebar'
import {
  dashboardKPIs,
  shiftData,
  hourlyData,
  dailyData,
  wipLocations,
  monthlyData,
} from '../data/mockData'
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
  Tooltip, Legend, ResponsiveContainer
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
  const [isCollapsed, setIsCollapsed] = useState(false)
  
  // Date selectors for Shift Summary, Shift Charts, and Hourly Production (all use the same date)
  const [selectedDate, setSelectedDate] = useState(() => {
    const today = new Date()
    return today.getDate()
  })
  const [selectedMonth, setSelectedMonth] = useState(() => {
    const today = new Date()
    return today.getMonth() + 1 // 1-12
  })
  const [selectedYear, setSelectedYear] = useState(2026)
  
  // Separate selectors for Daily Production (uses month + year)
  const [dailyMonth, setDailyMonth] = useState(() => {
    const today = new Date()
    return today.getMonth() + 1
  })
  const [dailyYear, setDailyYear] = useState(2026)
  
  // Separate selector for Monthly Production (uses only year)
  const [monthlyYear, setMonthlyYear] = useState(2026)
  
  const today = new Date().toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric'
  })
  
  // Generate date options (1-31)
  const dateOptions = Array.from({ length: 31 }, (_, i) => i + 1)
  
  // Month options
  const monthOptions = [
    { value: 1, label: 'JAN' },
    { value: 2, label: 'FEB' },
    { value: 3, label: 'MAR' },
    { value: 4, label: 'APR' },
    { value: 5, label: 'MAY' },
    { value: 6, label: 'JUN' },
    { value: 7, label: 'JUL' },
    { value: 8, label: 'AUG' },
    { value: 9, label: 'SEP' },
    { value: 10, label: 'OCT' },
    { value: 11, label: 'NOV' },
    { value: 12, label: 'DEC' },
  ]
  
  // Year options (2024-2030)
  const yearOptions = Array.from({ length: 7 }, (_, i) => 2024 + i)
  
  const getMonthLabel = (monthNum) => {
    return monthOptions.find(m => m.value === monthNum)?.label || ''
  }

  return (
    <div className="dash-root">
      <Sidebar isCollapsed={isCollapsed} setIsCollapsed={setIsCollapsed} />
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
          <div className="section-header">
            <div className="section-title">Shift Summary — {selectedDate}-{getMonthLabel(selectedMonth)}-{selectedYear}</div>
            <div className="date-selector">
              <label>Date:</label>
              <select value={selectedDate} onChange={(e) => setSelectedDate(Number(e.target.value))}>
                {dateOptions.map(d => (
                  <option key={d} value={d}>{d}</option>
                ))}
              </select>
              <select value={selectedMonth} onChange={(e) => setSelectedMonth(Number(e.target.value))}>
                {monthOptions.map(m => (
                  <option key={m.value} value={m.value}>{m.label}</option>
                ))}
              </select>
              <select value={selectedYear} onChange={(e) => setSelectedYear(Number(e.target.value))}>
                {yearOptions.map(y => (
                  <option key={y} value={y}>{y}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="shift-row-grid">
            <ShiftCard shift="A" data={shiftData.A} />
            <ShiftCard shift="B" data={shiftData.B} />
            <ShiftCard shift="C" data={shiftData.C} />
          </div>

          {/* Charts Section */}
          <div className="section-title" style={{ marginBottom: '16px', marginTop: '24px' }}>Production Charts (Date: {selectedDate}-{getMonthLabel(selectedMonth)}-{selectedYear})</div>

          {/* Shift Production Charts - 3 in a row */}
          <div className="shift-charts-grid">
            {['A', 'B', 'C'].map(shift => (
              <div key={shift} className="chart-box">
                <div className="chart-header">
                  <div className="chart-heading">Shift {shift} Production</div>
                </div>
                <ResponsiveContainer width="100%" height={200}>
                  <BarChart data={hourlyData.slice(0, 8)} margin={{ top: 5, right: 10, left: -10, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                    <XAxis 
                      dataKey="hour" 
                      tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 10 }} 
                      height={30}
                    />
                    <YAxis 
                      tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 10 }}
                      width={35}
                    />
                    <Tooltip 
                      contentStyle={{ 
                        background: '#1e1e1e', 
                        border: '1px solid rgba(255,255,255,0.15)', 
                        borderRadius: 8, 
                        color: '#fff',
                        fontSize: 10
                      }} 
                    />
                    <Legend 
                      wrapperStyle={{ fontSize: 8 }}
                      iconSize={8}
                    />
                    <Bar dataKey="oldLine" name="Old" fill="#2196F3" radius={[2,2,0,0]} />
                    <Bar dataKey="newLine" name="New" fill="#FF9800" radius={[2,2,0,0]} />
                    <Bar dataKey="testCycle" name="Test" fill="#8B0000" radius={[2,2,0,0]} />
                    <Bar dataKey="fes" name="FES" fill="#FF5252" radius={[2,2,0,0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            ))}
          </div>

          {/* Hourly Production Chart - Full width */}
          <div className="chart-box" style={{ marginTop: '16px' }}>
            <div className="chart-header">
              <div className="chart-heading">Hourly Production</div>
            </div>
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={hourlyData} margin={{ top: 10, right: 20, left: 5, bottom: 20 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                <XAxis 
                  dataKey="hour" 
                  tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 11 }}
                  angle={-45}
                  textAnchor="end"
                  height={60}
                />
                <YAxis tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 11 }} width={45} />
                <Tooltip contentStyle={{ background: '#1e1e1e', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#fff' }} />
                <Legend wrapperStyle={{ fontSize: 10, paddingTop: 10 }} iconSize={10} />
                <Bar dataKey="oldLine" name="Old Line" fill="#2196F3" radius={[3,3,0,0]} />
                <Bar dataKey="newLine" name="New Line" fill="#FF9800" radius={[3,3,0,0]} />
                <Bar dataKey="testCycle" name="Test Cycle" fill="#8B0000" radius={[3,3,0,0]} />
                <Bar dataKey="fes" name="FES" fill="#FF5252" radius={[3,3,0,0]} />
                <Bar dataKey="dispatched" name="Dispatched" fill="#FFC107" radius={[3,3,0,0]} />
                <Bar dataKey="testOK" name="Test OK" fill="#4CAF50" radius={[3,3,0,0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>

          {/* Daily and Monthly Charts - 2 in a row */}
          <div className="double-chart-grid">
            {/* Daily Production Chart */}
            <div className="chart-box">
              <div className="chart-header">
                <div className="chart-heading">Daily Production of Month: {getMonthLabel(dailyMonth)} {dailyYear}</div>
                <div className="date-selector-compact">
                  <select value={dailyMonth} onChange={(e) => setDailyMonth(Number(e.target.value))}>
                    {monthOptions.map(m => (
                      <option key={m.value} value={m.value}>{m.label}</option>
                    ))}
                  </select>
                  <select value={dailyYear} onChange={(e) => setDailyYear(Number(e.target.value))}>
                    {yearOptions.map(y => (
                      <option key={y} value={y}>{y}</option>
                    ))}
                  </select>
                </div>
              </div>
              <ResponsiveContainer width="100%" height={240}>
                <BarChart data={dailyData} margin={{ top: 10, right: 15, left: 0, bottom: 20 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                  <XAxis 
                    dataKey="day" 
                    tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 9 }}
                    angle={-45}
                    textAnchor="end"
                    height={60}
                  />
                  <YAxis tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 10 }} width={40} />
                  <Tooltip contentStyle={{ background: '#1e1e1e', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#fff', fontSize: 10 }} />
                  <Legend wrapperStyle={{ fontSize: 9, paddingTop: 10 }} iconSize={8} />
                  <Bar dataKey="oldLine" name="Old Line" fill="#2196F3" radius={[2,2,0,0]} />
                  <Bar dataKey="newLine" name="New Line" fill="#FF9800" radius={[2,2,0,0]} />
                  <Bar dataKey="testCycle" name="Test Cycle" fill="#8B0000" radius={[2,2,0,0]} />
                  <Bar dataKey="fes" name="FES" fill="#FF5252" radius={[2,2,0,0]} />
                  <Bar dataKey="dispatched" name="Dispatched" fill="#FFC107" radius={[2,2,0,0]} />
                  <Bar dataKey="testOK" name="Test OK" fill="#4CAF50" radius={[2,2,0,0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>

            {/* Monthly Production Chart */}
            <div className="chart-box">
              <div className="chart-header">
                <div className="chart-heading">Monthly Production of Year: {monthlyYear}</div>
                <div className="date-selector-compact">
                  <label>Year:</label>
                  <select value={monthlyYear} onChange={(e) => setMonthlyYear(Number(e.target.value))}>
                    {yearOptions.map(y => (
                      <option key={y} value={y}>{y}</option>
                    ))}
                  </select>
                </div>
              </div>
              <ResponsiveContainer width="100%" height={240}>
                <BarChart data={monthlyData} margin={{ top: 10, right: 15, left: 0, bottom: 20 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                  <XAxis 
                    dataKey="month" 
                    tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 10 }}
                    angle={-45}
                    textAnchor="end"
                    height={60}
                  />
                  <YAxis tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 10 }} width={40} />
                  <Tooltip contentStyle={{ background: '#1e1e1e', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#fff', fontSize: 10 }} />
                  <Legend wrapperStyle={{ fontSize: 9, paddingTop: 10 }} iconSize={8} />
                  <Bar dataKey="oldLine" name="Old Line Prod" fill="#2196F3" radius={[3,3,0,0]} />
                  <Bar dataKey="newLine" name="New Line Prod" fill="#FF9800" radius={[3,3,0,0]} />
                  <Bar dataKey="testCall" name="Test Call" fill="#8B0000" radius={[3,3,0,0]} />
                  <Bar dataKey="fes" name="FES" fill="#00BCD4" radius={[3,3,0,0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

        </div>
      </div>
    </div>
  )
}