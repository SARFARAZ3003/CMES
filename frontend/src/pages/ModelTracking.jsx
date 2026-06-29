/**
 * ModelTracking.jsx
 *
 * Model Tracking page.
 * All data comes from the backend — no mock data, no fallback arrays.
 *
 * Endpoints:
 *   GET /api/modeltracking/summary?modelNo=<exact>&page=<n>&pageSize=50
 *   GET /api/modeltracking/details?modelNo=<exact>&page=<n>&pageSize=100
 *
 * Both return: { page, pageSize, totalCount, totalPages, items: [...] }
 * Summary also returns: { total: { modelNo:'TOTAL', fes, wip, ... } }
 */

import { useState, useEffect, useCallback } from 'react'
import Sidebar from '../components/Sidebar'
import './PageShell.css'
import './Reports.css'
import './ModelTracking.css'

// ═══════════════════════════════════════════════════════════════════
// DATA-ACCESS — replace body only when endpoint URL changes
// ═══════════════════════════════════════════════════════════════════

async function fetchSummary(modelNo, page, pageSize) {
  const params = new URLSearchParams({ page, pageSize })
  if (modelNo) params.set('modelNo', modelNo)
  const res = await fetch(`/api/modeltracking/summary?${params}`)
  if (!res.ok) throw new Error(`Summary fetch failed: ${res.status}`)
  return res.json()
  // Returns: { page, pageSize, totalCount, totalPages, items, total }
}

async function fetchDetails(modelNo, page, pageSize) {
  const params = new URLSearchParams({ page, pageSize })
  if (modelNo) params.set('modelNo', modelNo)
  const res = await fetch(`/api/modeltracking/details?${params}`)
  if (!res.ok) throw new Error(`Details fetch failed: ${res.status}`)
  return res.json()
  // Returns: { page, pageSize, totalCount, totalPages, items }
}

// ═══════════════════════════════════════════════════════════════════
// HELPERS
// ═══════════════════════════════════════════════════════════════════

function fmt(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (isNaN(d)) return iso
  const pad = n => String(n).padStart(2, '0')
  return `${pad(d.getDate())}-${pad(d.getMonth() + 1)}-${d.getFullYear()} ` +
         `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

const STATUS_COLORS = {
  'IN-PROD':   '#4CAF50',
  'ISSUE':     '#FF9800',
  'FES':       '#9C27B0',
  'IN REPAIR': '#2196F3',
  'UNKNOWN':   '#9E9E9E',
}

// ═══════════════════════════════════════════════════════════════════
// SHARED COMPONENTS
// ═══════════════════════════════════════════════════════════════════

function Pager({ page, totalPages, onPrev, onNext }) {
  if (totalPages <= 1) return null
  return (
    <div className="report-pager">
      <button className="pager-btn" onClick={onPrev} disabled={page <= 1}>‹ Prev</button>
      <span className="pager-info">Page {page} of {totalPages}</span>
      <button className="pager-btn" onClick={onNext} disabled={page >= totalPages}>Next ›</button>
    </div>
  )
}

function ScrollTable({ children, minWidth = 700 }) {
  return (
    <div className="mt-scroll-wrap">
      <div style={{ minWidth }}>
        {children}
      </div>
    </div>
  )
}

// ═══════════════════════════════════════════════════════════════════
// MODEL WISE SUMMARY VIEW
// ═══════════════════════════════════════════════════════════════════

const SUMMARY_COLS = [
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

function SummaryView({ queriedModel }) {
  const PAGE_SIZE = 50
  const [rows,       setRows]       = useState([])
  const [totalRow,   setTotalRow]   = useState(null)
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [page,       setPage]       = useState(1)
  const [loading,    setLoading]    = useState(true)
  const [error,      setError]      = useState(null)

  useEffect(() => {
    setLoading(true)
    setError(null)
    setPage(1)
    fetchSummary(queriedModel, 1, PAGE_SIZE)
      .then(r => {
        setRows(r.items ?? [])
        setTotalRow(r.total ?? null)
        setTotalCount(r.totalCount ?? 0)
        setTotalPages(r.totalPages ?? 1)
      })
      .catch(err => setError(err.message))
      .finally(() => setLoading(false))
  }, [queriedModel])

  const loadPage = useCallback(p => {
    setLoading(true)
    setError(null)
    fetchSummary(queriedModel, p, PAGE_SIZE)
      .then(r => {
        setRows(r.items ?? [])
        setTotalRow(r.total ?? null)
        setTotalCount(r.totalCount ?? 0)
        setTotalPages(r.totalPages ?? 1)
        setPage(p)
      })
      .catch(err => setError(err.message))
      .finally(() => setLoading(false))
  }, [queriedModel])

  if (loading) return <div className="report-table-box"><div className="report-state">Loading…</div></div>
  if (error)   return <div className="report-table-box"><div className="report-state error">{error}</div></div>

  return (
    <>
      <div className="mt-count-label">{totalCount} model{totalCount !== 1 ? 's' : ''}</div>
      <div className="report-table-box">
        <ScrollTable minWidth={1100}>
          <table className="report-table">
            <thead>
              <tr>
                <th>Model No</th>
                {SUMMARY_COLS.map(c => (
                  <th key={c.key} className="num">{c.label}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.length === 0
                ? <tr><td colSpan={SUMMARY_COLS.length + 1} className="mt-empty">No records found</td></tr>
                : rows.map(row => (
                  <tr key={row.modelNo}>
                    <td className="mt-model-cell">{row.modelNo}</td>
                    {SUMMARY_COLS.map(col => (
                      <td key={col.key} className="num">
                        {(row[col.key] ?? 0) > 0
                          ? <span className="mt-nonzero">{row[col.key]}</span>
                          : <span className="mt-zero">0</span>
                        }
                      </td>
                    ))}
                  </tr>
                ))
              }
              {/* TOTAL row — provided by the backend, no client-side recomputation */}
              {totalRow && rows.length > 0 && (
                <tr className="mt-total-row">
                  <td><strong>TOTAL</strong></td>
                  {SUMMARY_COLS.map(col => (
                    <td key={col.key} className="num">
                      <strong>{totalRow[col.key] ?? 0}</strong>
                    </td>
                  ))}
                </tr>
              )}
            </tbody>
          </table>
        </ScrollTable>
      </div>
      <Pager page={page} totalPages={totalPages}
        onPrev={() => loadPage(Math.max(1, page - 1))}
        onNext={() => loadPage(Math.min(totalPages, page + 1))} />
    </>
  )
}

// ═══════════════════════════════════════════════════════════════════
// MODEL DETAILS VIEW
// ═══════════════════════════════════════════════════════════════════

function DetailsView({ queriedModel }) {
  const PAGE_SIZE = 100
  const [rows,       setRows]       = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [page,       setPage]       = useState(1)
  const [loading,    setLoading]    = useState(true)
  const [error,      setError]      = useState(null)

  useEffect(() => {
    setLoading(true)
    setError(null)
    setPage(1)
    fetchDetails(queriedModel, 1, PAGE_SIZE)
      .then(r => {
        setRows(r.items ?? [])
        setTotalCount(r.totalCount ?? 0)
        setTotalPages(r.totalPages ?? 1)
      })
      .catch(err => setError(err.message))
      .finally(() => setLoading(false))
  }, [queriedModel])

  const loadPage = useCallback(p => {
    setLoading(true)
    setError(null)
    fetchDetails(queriedModel, p, PAGE_SIZE)
      .then(r => {
        setRows(r.items ?? [])
        setTotalCount(r.totalCount ?? 0)
        setTotalPages(r.totalPages ?? 1)
        setPage(p)
      })
      .catch(err => setError(err.message))
      .finally(() => setLoading(false))
  }, [queriedModel])

  if (loading) return <div className="report-table-box"><div className="report-state">Loading…</div></div>
  if (error)   return <div className="report-table-box"><div className="report-state error">{error}</div></div>

  return (
    <>
      <div className="mt-count-label">{totalCount} record{totalCount !== 1 ? 's' : ''}</div>
      <div className="report-table-box">
        <ScrollTable minWidth={980}>
          <table className="report-table">
            <thead>
              <tr>
                <th>ESN</th>
                <th>Model No</th>
                <th>Block Load Time</th>
                <th>Job Order No</th>
                <th>Workstation</th>
                <th>Status</th>
                <th>Location</th>
                <th>Last Updated On</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0
                ? <tr><td colSpan={8} className="mt-empty">No records found</td></tr>
                : rows.map((row, i) => (
                  <tr key={i}>
                    <td className="mt-mono">{row.serialNo}</td>
                    <td className="mt-model-cell">{row.modelNo}</td>
                    <td className="mt-mono">{fmt(row.blockLoadTime)}</td>
                    <td className="mt-mono">{row.workOrderNo}</td>
                    <td>{row.workstation}</td>
                    <td>
                      <span
                        className="mt-status-badge"
                        style={{
                          background:  `${STATUS_COLORS[row.status] ?? '#888'}22`,
                          color:        STATUS_COLORS[row.status] ?? '#555',
                          borderColor: `${STATUS_COLORS[row.status] ?? '#888'}55`,
                        }}
                      >
                        {row.status}
                      </span>
                    </td>
                    <td>{row.location}</td>
                    <td className="mt-mono">{fmt(row.lastUpdatedOn)}</td>
                  </tr>
                ))
              }
            </tbody>
          </table>
        </ScrollTable>
      </div>
      <Pager page={page} totalPages={totalPages}
        onPrev={() => loadPage(Math.max(1, page - 1))}
        onNext={() => loadPage(Math.min(totalPages, page + 1))} />
    </>
  )
}

// ═══════════════════════════════════════════════════════════════════
// PAGE ROOT
// ═══════════════════════════════════════════════════════════════════

export default function ModelTracking() {
  const [inputModel,   setInputModel]   = useState('')
  const [queriedModel, setQueriedModel] = useState('')
  const [activeTab,    setActiveTab]    = useState('summary')

  const todayLabel = new Date().toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric',
  })

  const handleSearch  = () => setQueriedModel(inputModel.trim())
  const handleKeyDown = e  => { if (e.key === 'Enter') handleSearch() }

  return (
    <div className="dash-root">
      <Sidebar />
      <div className="dash-main">

        <div className="dash-topbar">
          <div className="dash-topbar-left">
            <span className="dash-page-title">Model Tracking</span>
            <span className="dash-date">{todayLabel}</span>
          </div>
        </div>

        <div className="dash-content">

          {/* ── Search controls ── */}
          <div className="mt-search-bar">
            <label className="mt-search-label">Model No</label>
            <input
              className="report-search mt-search-input"
              placeholder="e.g. SO64815"
              value={inputModel}
              onChange={e => setInputModel(e.target.value)}
              onKeyDown={handleKeyDown}
            />
            <button className="mt-search-btn" onClick={handleSearch}>
              Search
            </button>
            {queriedModel && (
              <button
                className="mt-clear-btn"
                onClick={() => { setInputModel(''); setQueriedModel('') }}
              >
                Clear ✕
              </button>
            )}
            {queriedModel && (
              <span className="mt-filter-chip">
                Filtered: <strong>{queriedModel}</strong>
              </span>
            )}
          </div>

          {/* ── Tab switcher ── */}
          <div className="mt-tabs">
            <button
              className={`mt-tab${activeTab === 'summary' ? ' mt-tab--active' : ''}`}
              onClick={() => setActiveTab('summary')}
            >
              Model Wise Summary
            </button>
            <button
              className={`mt-tab${activeTab === 'details' ? ' mt-tab--active' : ''}`}
              onClick={() => setActiveTab('details')}
            >
              Model Details
            </button>
          </div>

          {activeTab === 'summary' && <SummaryView queriedModel={queriedModel} />}
          {activeTab === 'details' && <DetailsView queriedModel={queriedModel} />}

        </div>
      </div>
    </div>
  )
}
