/**
 * ModelTracking.jsx
 *
 * Model Tracking page — mirrors the legacy CMES Model Tracking view.
 *
 * ── Data-access layer ────────────────────────────────────────────────────────
 * Replace the two async stubs (fetchSummary / fetchDetails) with real fetch()
 * calls when the backend is live. Nothing else in the file changes.
 *
 * Endpoints:
 *   GET /api/modeltracking/summary?modelNo=<filter>&page=<n>&pageSize=50
 *   GET /api/modeltracking/details?modelNo=<filter>&page=<n>&pageSize=100
 *
 * Both return: { page, pageSize, totalCount, totalPages, items: [...] }
 * ─────────────────────────────────────────────────────────────────────────────
 */

import { useState, useEffect, useCallback } from 'react'
import Sidebar from '../components/Sidebar'
import './PageShell.css'
import './Reports.css'
import './ModelTracking.css'

// ═══════════════════════════════════════════════════════════════════
// MOCK DATA — remove when real API is connected
// ═══════════════════════════════════════════════════════════════════

const MODELS = ['SO64815', 'SO64819', 'SO64929', 'SO60341', 'SO65210', 'SO63001']

const MOCK_SUMMARY = MODELS.map((m, i) => ({
  modelNo:      m,
  wip:          Math.floor(Math.random() * 40) + 5,
  fes:          Math.floor(Math.random() * 15),
  qualityDock:  Math.floor(Math.random() * 10),
  paintLine:    Math.floor(Math.random() * 8),
  testCellLine: Math.floor(Math.random() * 12),
  paintRepair:  Math.floor(Math.random() * 3),
  testRework:   Math.floor(Math.random() * 2),
  shortBuild:   Math.floor(Math.random() * 2),
  eqaAudit:     Math.floor(Math.random() * 5),
  mra:          Math.floor(Math.random() * 3),
  pe:           Math.floor(Math.random() * 4),
  unknown:      0,
}))

const LOCATIONS = ['LINESET', 'OLD LINE', 'NEW LINE', 'TEST CELL LINE', 'PAINT LINE', 'QUALITY DOCK', 'MRA']
const STATUSES  = ['IN-PROD', 'ISSUE', 'IN REPAIR']

const MOCK_DETAILS = Array.from({ length: 47 }, (_, i) => ({
  serialNo:      `G459${4500 + i}`,
  modelNo:       MODELS[i % MODELS.length],
  blockLoadTime: new Date(2026, 4, 26, 6 + (i % 12), 10 + (i % 50)).toISOString(),
  workOrderNo:   `318${8771 + i}-${(i % 5) + 1}`,
  workstation:   String(34000 + (i % 8) * 1000),
  status:        STATUSES[i % 3],
  location:      LOCATIONS[i % LOCATIONS.length],
  lastUpdatedOn: new Date(2026, 4, 26, 10 + (i % 8), i % 59).toISOString(),
}))

// ═══════════════════════════════════════════════════════════════════
// DATA-ACCESS STUBS — only these two functions change when APIs land
// ═══════════════════════════════════════════════════════════════════

/**
 * TODO: Replace with:
 *   const res = await fetch(`/api/modeltracking/summary?modelNo=${encodeURIComponent(modelNo ?? '')}&page=${page}&pageSize=${pageSize}`)
 *   return res.json()
 */
async function fetchSummary(modelNo, page, pageSize) {
  await delay(400)
  const filtered = modelNo
    ? MOCK_SUMMARY.filter(r => r.modelNo.toLowerCase().includes(modelNo.toLowerCase()))
    : MOCK_SUMMARY
  const start = (page - 1) * pageSize
  return {
    items:      filtered.slice(start, start + pageSize),
    totalCount: filtered.length,
    page,
    pageSize,
    totalPages: Math.ceil(filtered.length / pageSize),
  }
}

/**
 * TODO: Replace with:
 *   const res = await fetch(`/api/modeltracking/details?modelNo=${encodeURIComponent(modelNo ?? '')}&page=${page}&pageSize=${pageSize}`)
 *   return res.json()
 */
async function fetchDetails(modelNo, page, pageSize) {
  await delay(500)
  const filtered = modelNo
    ? MOCK_DETAILS.filter(r => r.modelNo.toLowerCase().includes(modelNo.toLowerCase()))
    : MOCK_DETAILS
  const start = (page - 1) * pageSize
  return {
    items:      filtered.slice(start, start + pageSize),
    totalCount: filtered.length,
    page,
    pageSize,
    totalPages: Math.ceil(filtered.length / pageSize),
  }
}

const delay = ms => new Promise(r => setTimeout(r, ms))

// ═══════════════════════════════════════════════════════════════════
// HELPERS
// ═══════════════════════════════════════════════════════════════════

function fmt(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (isNaN(d)) return iso
  const pad = n => String(n).padStart(2, '0')
  return `${pad(d.getDate())}-${pad(d.getMonth() + 1)}-${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

const STATUS_COLORS = {
  'IN-PROD':   '#4CAF50',
  'ISSUE':     '#FF9800',
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
  { key: 'fes',          label: 'FES'           },
  { key: 'wip',          label: 'WIP'           },
  { key: 'qualityDock',  label: 'Quality Dock'  },
  { key: 'paintLine',    label: 'Paint Line'    },
  { key: 'testCellLine', label: 'Test Cell Line'},
  { key: 'paintRepair',  label: 'Paint Repair'  },
  { key: 'testRework',   label: 'Test Rework'   },
  { key: 'shortBuild',   label: 'Short Build'   },
  { key: 'eqaAudit',     label: 'EQA Audit'     },
  { key: 'mra',          label: 'MRA'           },
  { key: 'pe',           label: 'PE'            },
  { key: 'unknown',      label: 'Unknown'       },
]

function SummaryView({ queriedModel }) {
  const PAGE_SIZE = 50
  const [rows,       setRows]       = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [page,       setPage]       = useState(1)
  const [loading,    setLoading]    = useState(true)

  useEffect(() => {
    setLoading(true)
    setPage(1)
    fetchSummary(queriedModel, 1, PAGE_SIZE)
      .then(r => { setRows(r.items); setTotalCount(r.totalCount); setTotalPages(r.totalPages) })
      .finally(() => setLoading(false))
  }, [queriedModel])

  const loadPage = useCallback(p => {
    setLoading(true)
    fetchSummary(queriedModel, p, PAGE_SIZE)
      .then(r => { setRows(r.items); setTotalCount(r.totalCount); setTotalPages(r.totalPages); setPage(p) })
      .finally(() => setLoading(false))
  }, [queriedModel])

  // Compute totals for the TOTAL row
  const totals = SUMMARY_COLS.reduce((acc, col) => {
    acc[col.key] = rows.reduce((s, r) => s + (r[col.key] ?? 0), 0)
    return acc
  }, {})

  if (loading) return <div className="report-state">Loading…</div>

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
                        {row[col.key] > 0
                          ? <span className="mt-nonzero">{row[col.key]}</span>
                          : <span className="mt-zero">0</span>
                        }
                      </td>
                    ))}
                  </tr>
                ))
              }
              {/* TOTAL row — mirrors the legacy application */}
              {rows.length > 0 && (
                <tr className="mt-total-row">
                  <td><strong>TOTAL</strong></td>
                  {SUMMARY_COLS.map(col => (
                    <td key={col.key} className="num">
                      <strong>{totals[col.key]}</strong>
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

  useEffect(() => {
    setLoading(true)
    setPage(1)
    fetchDetails(queriedModel, 1, PAGE_SIZE)
      .then(r => { setRows(r.items); setTotalCount(r.totalCount); setTotalPages(r.totalPages) })
      .finally(() => setLoading(false))
  }, [queriedModel])

  const loadPage = useCallback(p => {
    setLoading(true)
    fetchDetails(queriedModel, p, PAGE_SIZE)
      .then(r => { setRows(r.items); setTotalCount(r.totalCount); setTotalPages(r.totalPages); setPage(p) })
      .finally(() => setLoading(false))
  }, [queriedModel])

  if (loading) return <div className="report-state">Loading…</div>

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
                          background:  `${STATUS_COLORS[row.status] ?? '#555'}22`,
                          color:        STATUS_COLORS[row.status] ?? '#aaa',
                          borderColor: `${STATUS_COLORS[row.status] ?? '#555'}55`,
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
  const [queriedModel, setQueriedModel] = useState('')  // committed on Search click
  const [activeTab,    setActiveTab]    = useState('summary')  // 'summary' | 'details'

  const todayLabel = new Date().toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric',
  })

  const handleSearch = () => setQueriedModel(inputModel.trim())
  const handleKeyDown = e => { if (e.key === 'Enter') handleSearch() }

  return (
    <div className="dash-root">
      <Sidebar />
      <div className="dash-main">

        {/* ── Top bar ── */}
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

          {/* ── Tab content ── */}
          {activeTab === 'summary' && <SummaryView queriedModel={queriedModel} />}
          {activeTab === 'details' && <DetailsView queriedModel={queriedModel} />}

        </div>
      </div>
    </div>
  )
}
