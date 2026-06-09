import { useEffect, useState } from 'react'
import Sidebar from '../components/Sidebar'
import api from '../api/client'
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer
} from 'recharts'
import './Dashboard.css'
import './Reports.css'

export default function WipReport() {
  const [summary, setSummary] = useState(null)
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const today = new Date().toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric'
  })

  useEffect(() => {
    Promise.all([
      api.get('/Wip/summary'),
      api.get('/Wip/locations'),
    ])
      .then(([sumRes, locRes]) => {
        setSummary(sumRes.data)
        setRows(locRes.data)
      })
      .catch(() => setError('Backend se data nahi mila. Kya server localhost:5000 pe chal raha hain?'))
      .finally(() => setLoading(false))
  }, [])

  return (
    <div className="dash-root">
      <Sidebar />
      <div className="dash-main">

        <div className="dash-topbar">
          <div className="dash-topbar-left">
            <span className="dash-page-title">WIP Report</span>
            <span className="dash-date">{today}</span>
          </div>
          <div className="dash-topbar-right">
            <span className="dash-live-dot" />
            <span className="dash-live-text">Live</span>
          </div>
        </div>

        <div className="dash-content">
          {summary && (
            <div className="kpi-row">
              <div className="kpi-card" style={{ borderTopColor: '#2196F3' }}>
                <div className="kpi-value" style={{ color: '#2196F3' }}>{summary.totalWip}</div>
                <div className="kpi-label">Total WIP</div>
                <div className="kpi-sub">in plant</div>
              </div>
              <div className="kpi-card" style={{ borderTopColor: '#00BCD4' }}>
                <div className="kpi-value" style={{ color: '#00BCD4' }}>{summary.locations}</div>
                <div className="kpi-label">Locations</div>
              </div>
              <div className="kpi-card" style={{ borderTopColor: '#FF9800' }}>
                <div className="kpi-value" style={{ color: '#FF9800' }}>{summary.oldestHours}h</div>
                <div className="kpi-label">Oldest WIP</div>
              </div>
            </div>
          )}

          <div className="section-title">WIP by Location</div>

          {!loading && !error && rows.length > 0 && (
            <div className="chart-box" style={{ marginBottom: 20 }}>
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={rows} layout="vertical" margin={{ top: 10, right: 30, left: 90, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
                  <XAxis type="number" tick={{ fill: 'rgba(255,255,255,0.5)', fontSize: 12 }} />
                  <YAxis dataKey="location" type="category" tick={{ fill: 'rgba(255,255,255,0.6)', fontSize: 11 }} width={88} />
                  <Tooltip contentStyle={{ background: '#1e1e1e', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#fff' }} />
                  <Bar dataKey="count" name="WIP Count" fill="#2196F3" radius={[0, 4, 4, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}

          <div className="report-table-box">
            {loading && <div className="report-state">Loading…</div>}
            {error && <div className="report-state error">{error}</div>}
            {!loading && !error && (
              <table className="report-table">
                <thead>
                  <tr><th>Location</th><th className="num">WIP Count</th></tr>
                </thead>
                <tbody>
                  {rows.map((r, i) => (
                    <tr key={i}>
                      <td>{r.location}</td>
                      <td className="num">{r.count}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
