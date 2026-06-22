import { useEffect, useState } from 'react'
import Sidebar from '../components/Sidebar'
import api from '../api/client'
import './Dashboard.css'
import './Reports.css'

const PAGE_SIZE = 50

const statusPill = (status) => {
  const map = { 1: 'pill-wip', 2: 'pill-testok', 6: 'pill-fes' }
  return `pill ${map[status] || 'pill-wip'}`
}

const fmtDate = (s) => {
  if (!s) return '—'
  const d = new Date(s)
  return isNaN(d) ? s : d.toLocaleString('en-IN', {
    day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit'
  })
}

// Column definitions for the model-wise summary table — matches legacy CMES layout
const MODEL_SUMMARY_COLS = [
  { key: 'fes',          label: 'FES'            },
  { key: 'wip',          label: 'WIP'            },
  { key: 'qualityDock',  label: 'Quality Dock'   },
  { key: 'paintLine',    label: 'Paint Line'     },
  { key: 'testCellLine', label: 'Test Cell Line' },
  { key: 'paintRepair',  label: 'Paint Repair'   },
  { key: 'testRework',   label: 'Test Rework'    },
  { key: 'shortBuild',   label: 'Short Build'    },
  { key: 'eqaAudit',     label: 'EQA Audit'      },
  { key: 'mra',          label: 'MRA'            },
  { key: 'pe',           label: 'PE'             },
  { key: 'unknown',      label: 'Unknown'        },
]

export default function ModelTracking() {
  const [summary, setSummary]             = useState(null)
  const [activeView, setActiveView]       = useState('summary')  // 'summary' | 'details'
  const [modelSummary, setModelSummary]   = useState([])
  const [modelSumLoading, setModelSumLoading] = useState(true)
  const [modelSumError, setModelSumError] = useState('')
  const [rows, setRows]                   = useState([])
  const [total, setTotal]                 = useState(0)
  const [totalPages, setTotalPages]       = useState(1)
  const [page, setPage]                   = useState(1)
  const [search, setSearch]               = useState('')
  const [loading, setLoading]             = useState(true)
  const [error, setError]                 = useState('')

  const today = new Date().toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric'
  })

  // KPI summary
  useEffect(() => {
    api.get('/SerialHistory/summary')
      .then(res => setSummary(res.data))
      .catch(() => {})
  }, [])

  // Model-wise summary table — GET /api/SerialHistory/model-summary
  useEffect(() => {
    setModelSumLoading(true)
    setModelSumError('')
    api.get('/SerialHistory/model-summary')
      .then(res => setModelSummary(res.data))
      .catch(() => setModelSumError('Model summary could not be loaded.'))
      .finally(() => setModelSumLoading(false))
  }, [])

  // Debounce search → reset to page 1
  useEffect(() => {
    const t = setTimeout(() => setPage(1), 400)
    return () => clearTimeout(t)
  }, [search])

  // Detail rows
  useEffect(() => {
    setLoading(true)
    setError('')
    const t = setTimeout(() => {
      api.get('/SerialHistory', { params: { page, pageSize: PAGE_SIZE, search } })
        .then(res => {
          setRows(res.data.rows)
          setTotal(res.data.total)
          setTotalPages(res.data.totalPages || 1)
        })
        .catch(() => setError('Backend se data nahi mila. Kya server localhost:5000 pe chal raha hain?'))
        .finally(() => setLoading(false))
    }, 250)
    return () => clearTimeout(t)
  }, [page, search])

  return (
    <div className="dash-root">
      <Sidebar />
      <div className="dash-main">

        <div className="dash-topbar">
          <div className="dash-topbar-left">
            <span className="dash-page-title">Model Tracking</span>
            <span className="dash-date">{today}</span>
          </div>
          <div className="dash-topbar-right">
            <span className="dash-live-dot" />
            <span className="dash-live-text">Live</span>
          </div>
        </div>

        <div className="dash-content">

          {/* ── KPI Cards ── */}
          {summary && (
            <div className="kpi-row" style={{ gridTemplateColumns: 'repeat(3, 1fr)', marginBottom: 20 }}>
              <div className="kpi-card" style={{ borderTopColor: '#4CAF50' }}>
                <div className="kpi-value" style={{ color: '#4CAF50' }}>{summary.totalRecords.toLocaleString('en-IN')}</div>
                <div className="kpi-label">Total Records</div>
              </div>
              <div className="kpi-card" style={{ borderTopColor: '#2196F3' }}>
                <div className="kpi-value" style={{ color: '#2196F3' }}>{summary.uniqueSerials.toLocaleString('en-IN')}</div>
                <div className="kpi-label">Unique Serials</div>
              </div>
              <div className="kpi-card" style={{ borderTopColor: '#00BCD4' }}>
                <div className="kpi-value" style={{ color: '#00BCD4' }}>{summary.workstations}</div>
                <div className="kpi-label">Workstations</div>
              </div>
            </div>
          )}

          {/* ── View toggle ── */}
          <div className="mt-view-toggle">
            <button
              className={`mt-toggle-btn ${activeView === 'summary' ? 'mt-toggle-active' : ''}`}
              onClick={() => setActiveView('summary')}
            >
              Summary View
            </button>
            <button
              className={`mt-toggle-btn ${activeView === 'details' ? 'mt-toggle-active' : ''}`}
              onClick={() => setActiveView('details')}
            >
              Details View
            </button>
          </div>

          {/* ── Model-wise WIP Summary Table (legacy layout) ── */}
          {activeView === 'summary' && (
            <>
              <div className="section-title" style={{ marginBottom: 8 }}>Model Wise WIP Summary</div>
              <div className="report-table-box" style={{ marginBottom: 24 }}>
                {modelSumLoading && <div className="report-state">Loading…</div>}
                {modelSumError && <div className="report-state error">{modelSumError}</div>}
                {!modelSumLoading && !modelSumError && modelSummary.length === 0 && (
                  <div className="report-state">No active WIP models found.</div>
                )}
                {!modelSumLoading && !modelSumError && modelSummary.length > 0 && (
                  <table className="report-table">
                    <thead>
                      <tr>
                        <th>Model No</th>
                        {MODEL_SUMMARY_COLS.map(c => (
                          <th key={c.key} style={{ textAlign: 'center' }}>{c.label}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {modelSummary.map((row, i) => (
                        <tr key={i}>
                          <td>{row.modelNo}</td>
                          {MODEL_SUMMARY_COLS.map(c => (
                            <td key={c.key} style={{ textAlign: 'center' }}>
                              {row[c.key] > 0
                                ? <span style={{ color: '#FF9800', fontWeight: 600 }}>{row[c.key]}</span>
                                : <span style={{ color: 'rgba(255,255,255,0.25)' }}>0</span>
                              }
                            </td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </>
          )}

          {/* ── Detail Table (existing, unchanged) ── */}
          {activeView === 'details' && (
            <>
              <div className="report-toolbar">
                <input
                  className="report-search"
                  placeholder="Search serial no, work order, workstation, location…"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
                <span className="dash-date">{total.toLocaleString('en-IN')} records</span>
              </div>

              <div className="report-table-box">
                {loading && <div className="report-state">Loading…</div>}
                {error && <div className="report-state error">{error}</div>}
                {!loading && !error && rows.length === 0 && (
                  <div className="report-state">Koi record nahi mila.</div>
                )}
                {!loading && !error && rows.length > 0 && (
                  <table className="report-table">
                    <thead>
                      <tr>
                        <th>Serial No</th><th>Product ID</th><th>Work Order</th>
                        <th>Workstation</th><th>Status</th><th>Location</th>
                        <th>Appl.</th><th>Created On</th><th>By</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rows.map((r, i) => (
                        <tr key={i}>
                          <td>{r.serialNo || '—'}</td>
                          <td>{r.productId ?? '—'}</td>
                          <td>{r.workOrderNo || '—'}</td>
                          <td>{r.workstation || '—'}</td>
                          <td><span className={statusPill(r.status)}>{r.status ?? '—'}</span></td>
                          <td>{r.location || '—'}</td>
                          <td>{r.application || '—'}</td>
                          <td>{fmtDate(r.createdOn)}</td>
                          <td>{r.createdBy || '—'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>

              {/* ── Pagination ── */}
              {!loading && !error && rows.length > 0 && (
                <div className="report-pager">
                  <button
                    className="pager-btn"
                    disabled={page <= 1}
                    onClick={() => setPage(p => Math.max(1, p - 1))}
                  >‹ Prev</button>
                  <span className="pager-info">Page {page} / {totalPages}</span>
                  <button
                    className="pager-btn"
                    disabled={page >= totalPages}
                    onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                  >Next ›</button>
                </div>
              )}
            </>
          )}

        </div>
      </div>
    </div>
  )
}
