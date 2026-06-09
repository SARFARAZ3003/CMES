import { useEffect, useMemo, useState } from 'react'
import Sidebar from '../components/Sidebar'
import api from '../api/client'
import './Dashboard.css'
import './Reports.css'

const statusPill = (status) => {
  const map = { OK: 'pill-ok', Low: 'pill-low', Out: 'pill-out' }
  return `pill ${map[status] || 'pill-ok'}`
}

export default function Inventory() {
  const [summary, setSummary] = useState(null)
  const [items, setItems] = useState([])
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const today = new Date().toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric'
  })

  useEffect(() => {
    Promise.all([
      api.get('/Inventory/summary'),
      api.get('/Inventory/items'),
    ])
      .then(([sumRes, itemRes]) => {
        setSummary(sumRes.data)
        setItems(itemRes.data)
      })
      .catch(() => setError('Backend se data nahi mila. Kya server localhost:5000 pe chal raha hain?'))
      .finally(() => setLoading(false))
  }, [])

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return items
    return items.filter(it =>
      it.partNo.toLowerCase().includes(q) ||
      it.partName.toLowerCase().includes(q) ||
      it.category.toLowerCase().includes(q)
    )
  }, [items, query])

  return (
    <div className="dash-root">
      <Sidebar />
      <div className="dash-main">

        <div className="dash-topbar">
          <div className="dash-topbar-left">
            <span className="dash-page-title">Inventory</span>
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
                <div className="kpi-value" style={{ color: '#4CAF50' }}>{summary.totalItems}</div>
                <div className="kpi-label">Total Items</div>
              </div>
              <div className="kpi-card" style={{ borderTopColor: '#FF9800' }}>
                <div className="kpi-value" style={{ color: '#FF9800' }}>{summary.lowStock}</div>
                <div className="kpi-label">Low Stock</div>
              </div>
              <div className="kpi-card" style={{ borderTopColor: '#F44336' }}>
                <div className="kpi-value" style={{ color: '#F44336' }}>{summary.outOfStock}</div>
                <div className="kpi-label">Out of Stock</div>
              </div>
            </div>
          )}

          <div className="report-toolbar">
            <input
              className="report-search"
              placeholder="Search part no, name, category…"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
            <span className="dash-date">{filtered.length} items</span>
          </div>

          <div className="report-table-box">
            {loading && <div className="report-state">Loading…</div>}
            {error && <div className="report-state error">{error}</div>}
            {!loading && !error && filtered.length === 0 && (
              <div className="report-state">Koi item nahi mila.</div>
            )}
            {!loading && !error && filtered.length > 0 && (
              <table className="report-table">
                <thead>
                  <tr>
                    <th>Part No</th><th>Part Name</th><th>Category</th>
                    <th className="num">In Stock</th><th className="num">Min Level</th>
                    <th>Unit</th><th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((it, i) => (
                    <tr key={i}>
                      <td>{it.partNo}</td>
                      <td>{it.partName}</td>
                      <td>{it.category}</td>
                      <td className="num">{it.inStock}</td>
                      <td className="num">{it.minLevel}</td>
                      <td>{it.unit}</td>
                      <td><span className={statusPill(it.status)}>{it.status}</span></td>
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
