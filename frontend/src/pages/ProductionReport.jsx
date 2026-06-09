import { useEffect, useState } from 'react'
import Sidebar from '../components/Sidebar'
import api from '../api/client'
import './Dashboard.css'
import './Reports.css'

export default function ProductionReport() {
  const [summary, setSummary] = useState(null)
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const today = new Date().toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric'
  })

  useEffect(() => {
    Promise.all([
      api.get('/Production/summary'),
      api.get('/Production/report'),
    ])
      .then(([sumRes, repRes]) => {
        setSummary(sumRes.data)
        setRows(repRes.data)
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
            <span className="dash-page-title">Production Report</span>
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
              <div className="kpi-card" style={{ borderTopColor: '#4CAF50' }}>
                <div className="kpi-value" style={{ color: '#4CAF50' }}>{summary.productionToday}</div>
                <div className="kpi-label">Production Today</div>
              </div>
              <div className="kpi-card" style={{ borderTopColor: '#2196F3' }}>
                <div className="kpi-value" style={{ color: '#2196F3' }}>{summary.shiftA}</div>
                <div className="kpi-label">Shift A</div>
              </div>
              <div className="kpi-card" style={{ borderTopColor: '#FF9800' }}>
                <div className="kpi-value" style={{ color: '#FF9800' }}>{summary.fesCount}</div>
                <div className="kpi-label">FES Done</div>
              </div>
              <div className="kpi-card" style={{ borderTopColor: '#9C27B0' }}>
                <div className="kpi-value" style={{ color: '#9C27B0' }}>{summary.testOk}</div>
                <div className="kpi-label">Test OK</div>
              </div>
            </div>
          )}

          <div className="section-title">Shift-wise Production</div>
          <div className="report-table-box">
            {loading && <div className="report-state">Loading…</div>}
            {error && <div className="report-state error">{error}</div>}
            {!loading && !error && (
              <table className="report-table">
                <thead>
                  <tr>
                    <th>Date</th><th>Shift</th>
                    <th className="num">Old Line</th><th className="num">New Line</th>
                    <th className="num">Test Cycle</th><th className="num">FES</th>
                    <th className="num">Dispatched</th><th className="num">Test OK</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r, i) => (
                    <tr key={i}>
                      <td>{r.date}</td>
                      <td>Shift {r.shift}</td>
                      <td className="num">{r.oldLine}</td>
                      <td className="num">{r.newLine}</td>
                      <td className="num">{r.testCycle}</td>
                      <td className="num">{r.fes}</td>
                      <td className="num">{r.dispatched}</td>
                      <td className="num">{r.testOK}</td>
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
