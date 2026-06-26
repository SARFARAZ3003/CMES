/**
 * ProductionReport.jsx
 *
 * Production Report page — connected to the backend API.
 *
 * The three data-access functions (fetchKpis, fetchFesRows, fetchEngineHistory)
 * are the only integration seam. When a new endpoint is added or renamed,
 * only those functions change — all components, state, and layout stay the same.
 */

import { useState, useCallback, useEffect } from 'react'
import Sidebar from '../components/Sidebar'
import './PageShell.css'
import './Reports.css'
import './ProductionReport.css'

// ═══════════════════════════════════════════════════════════════════
// DATA-ACCESS FUNCTIONS
// Replace the bodies below with real API calls when the backend is ready.
// These are the only functions that change — nothing else in the file.
// ═══════════════════════════════════════════════════════════════════

/**
 * Fetch KPI summary for a given date.
 * Calls GET /api/productionreport/kpis?date=<date>
 * Returns: { shiftA, shiftB, shiftC, total } each { quant: number, fes: number }
 */
async function fetchKpis(date) {
  const res = await fetch(`/api/productionreport/kpis?date=${encodeURIComponent(date)}`)
  if (!res.ok) throw new Error(`KPI fetch failed: ${res.status}`)
  return res.json()
}

/**
 * Fetch paginated FES report rows for a given date.
 * Calls GET /api/productionreport/fes?date=<date>&page=<n>&pageSize=<n>
 * Returns: { items: FesRow[], totalCount: number }
 */
async function fetchFesRows(date, page, pageSize) {
  const res = await fetch(
    `/api/productionreport/fes?date=${encodeURIComponent(date)}&page=${page}&pageSize=${pageSize}`
  )
  if (!res.ok) throw new Error(`FES fetch failed: ${res.status}`)
  return res.json()
}

/**
 * Fetch engine transaction history for a given serial number.
 * Calls GET /api/productionreport/engine-history?esn=<esn>&page=<n>&pageSize=<n>
 * Returns: {
 *   engineInfo:   { modelNo, jobNo, currentLocation },
 *   transactions: { items: TxnRow[], totalCount: number },
 *   erpRows:      ErpRow[],
 * }
 */
async function fetchEngineHistory(esn, page, pageSize) {
  const res = await fetch(
    `/api/productionreport/engine-history?esn=${encodeURIComponent(esn)}&page=${page}&pageSize=${pageSize}`
  )
  if (!res.ok) throw new Error(`Engine history fetch failed: ${res.status}`)
  return res.json()
}

// ═══════════════════════════════════════════════════════════════════
// SUB-COMPONENTS
// ═══════════════════════════════════════════════════════════════════

/** Four KPI cards: Shift A / B / C / Total */
function KpiStrip({ kpis, loading, error }) {
  const cards = [
    { label: 'Shift A', color: '#4CAF50', data: kpis?.shiftA },
    { label: 'Shift B', color: '#2196F3', data: kpis?.shiftB },
    { label: 'Shift C', color: '#FF9800', data: kpis?.shiftC },
    { label: 'Total',   color: '#9C27B0', data: kpis?.total  },
  ]

  return (
    <div className="pr-kpi-row">
      {cards.map(({ label, color, data }) => (
        <div key={label} className="pr-kpi-card" style={{ borderTopColor: color }}>
          <div className="pr-kpi-label">{label}</div>
          <div className="pr-kpi-body">
            <div className="pr-kpi-metric">
              <span className="pr-kpi-val" style={{ color }}>
                {loading ? '—' : error ? '!' : (data?.quant ?? '—')}
              </span>
              <span className="pr-kpi-sub">Quant</span>
            </div>
            <div className="pr-kpi-divider" />
            <div className="pr-kpi-metric">
              <span className="pr-kpi-val" style={{ color }}>
                {loading ? '—' : error ? '!' : (data?.fes ?? '—')}
              </span>
              <span className="pr-kpi-sub">FES</span>
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}

/** Generic paginator */
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

/** Sticky-header scrollable table wrapper */
function ScrollTable({ children, minWidth }) {
  return (
    <div className="pr-scroll-wrap">
      <div style={{ minWidth: minWidth ?? 600 }}>
        {children}
      </div>
    </div>
  )
}

// ── FES REPORT ─────────────────────────────────────────────────────
function FesReport({ date }) {
  const PAGE_SIZE = 20

  const [rows,    setRows]    = useState([])
  const [total,   setTotal]   = useState(0)
  const [page,    setPage]    = useState(1)
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState(null)

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))
  const visible    = rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)

  // Reload whenever the queried date changes
  useEffect(() => {
    setLoading(true)
    setError(null)
    setPage(1)
    fetchFesRows(date, 1, PAGE_SIZE)
      .then(({ items, totalCount }) => {
        setRows(items)
        setTotal(totalCount)
      })
      .catch(err => setError(err.message ?? 'Failed to load FES records.'))
      .finally(() => setLoading(false))
  }, [date])

  if (loading) {
    return (
      <div className="report-table-box">
        <div className="report-state">Loading…</div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="report-table-box">
        <div className="report-state error">{error}</div>
      </div>
    )
  }

  return (
    <>
      <div className="report-table-box">
        <ScrollTable minWidth={720}>
          <table className="report-table">
            <thead>
              <tr>
                <th className="num" style={{ width: 56 }}>S.No</th>
                <th>ESN</th>
                <th>Model No</th>
                <th>Job Order No</th>
                <th>FES Date</th>
              </tr>
            </thead>
            <tbody>
              {visible.length === 0
                ? (
                  <tr>
                    <td colSpan={5} style={{ textAlign: 'center', padding: 32, color: 'rgba(255,255,255,0.35)' }}>
                      No FES records found for this date.
                    </td>
                  </tr>
                )
                : visible.map(r => (
                  <tr key={r.sno}>
                    <td className="num mono">{r.sno}</td>
                    <td className="mono">{r.esn}</td>
                    <td>{r.modelNo}</td>
                    <td className="mono">{r.jobOrderNo}</td>
                    <td className="mono">{r.fesDate}</td>
                  </tr>
                ))
              }
            </tbody>
          </table>
        </ScrollTable>
      </div>
      <Pager
        page={page}
        totalPages={totalPages}
        onPrev={() => setPage(p => Math.max(1, p - 1))}
        onNext={() => setPage(p => Math.min(totalPages, p + 1))}
      />
    </>
  )
}

// ── TCL ENGINE TRANSACTION HISTORY ─────────────────────────────────
function EngineHistory() {
  const PAGE_SIZE = 15

  const [esn,        setEsn]        = useState('')
  const [queried,    setQueried]     = useState(false)
  const [loading,    setLoading]     = useState(false)
  const [error,      setError]       = useState(null)
  const [engineInfo, setInfo]        = useState(null)
  const [txnRows,    setTxnRows]     = useState([])
  const [txnTotal,   setTxnTotal]    = useState(0)
  const [erpRows,    setErpRows]     = useState([])
  const [page,       setPage]        = useState(1)

  const totalPages = Math.max(1, Math.ceil(txnTotal / PAGE_SIZE))
  const visible    = txnRows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)

  const handleQuery = useCallback(() => {
    const trimmed = esn.trim()
    if (!trimmed) return

    setLoading(true)
    setQueried(true)
    setError(null)
    setPage(1)

    fetchEngineHistory(trimmed, 1, PAGE_SIZE)
      .then(({ engineInfo: info, transactions, erpRows: erp }) => {
        setInfo(info)
        setTxnRows(transactions.items)
        setTxnTotal(transactions.totalCount)
        setErpRows(erp)
      })
      .catch(err => {
        setError(err.message ?? 'Failed to load engine history.')
        setInfo(null)
        setTxnRows([])
        setTxnTotal(0)
        setErpRows([])
      })
      .finally(() => setLoading(false))
  }, [esn])

  const handleKeyDown = e => { if (e.key === 'Enter') handleQuery() }

  return (
    <>
      {/* Search bar */}
      <div className="report-toolbar">
        <input
          className="report-search"
          placeholder="Engine Serial Number…"
          value={esn}
          onChange={e => setEsn(e.target.value)}
          onKeyDown={handleKeyDown}
        />
        <button className="pr-query-btn" onClick={handleQuery} disabled={!esn.trim()}>
          Query
        </button>
      </div>

      {/* Prompt before first query */}
      {!queried && (
        <div className="report-state" style={{ marginTop: 40 }}>
          Enter an Engine Serial Number and click Query to load history.
        </div>
      )}

      {/* Error state */}
      {queried && error && (
        <div className="report-table-box" style={{ marginBottom: 16 }}>
          <div className="report-state error">{error}</div>
        </div>
      )}

      {/* Loading state */}
      {queried && loading && (
        <div className="report-table-box" style={{ marginBottom: 16 }}>
          <div className="report-state">Loading…</div>
        </div>
      )}

      {/* Results — only shown after a successful query */}
      {queried && !loading && !error && (
        <>
          {/* Engine info panel */}
          {engineInfo ? (
            <div className="pr-engine-info">
              <div className="pr-engine-info-item">
                <span className="pr-info-label">Model No</span>
                <span className="pr-info-value">{engineInfo.modelNo}</span>
              </div>
              <div className="pr-engine-info-item">
                <span className="pr-info-label">Job No</span>
                <span className="pr-info-value mono">{engineInfo.jobNo}</span>
              </div>
              <div className="pr-engine-info-item">
                <span className="pr-info-label">Current Location</span>
                <span className="pr-info-value">{engineInfo.currentLocation}</span>
              </div>
            </div>
          ) : (
            <div className="report-state" style={{ marginTop: 8, marginBottom: 16 }}>
              No engine found for this serial number.
            </div>
          )}

          {/* Transaction history table */}
          <div className="section-title" style={{ marginBottom: 8 }}>Transaction History</div>
          <div className="report-table-box" style={{ marginBottom: 20 }}>
            <ScrollTable minWidth={900}>
              <table className="report-table">
                <thead>
                  <tr>
                    <th>Init Code</th>
                    <th>Org Code</th>
                    <th>WIP Job No</th>
                    <th>ESN</th>
                    <th>Actual MSBM</th>
                    <th>Status</th>
                    <th>Oracle Status</th>
                    <th>Received Date</th>
                    <th className="num">Group Id</th>
                  </tr>
                </thead>
                <tbody>
                  {visible.length === 0
                    ? (
                      <tr>
                        <td colSpan={9} style={{ textAlign: 'center', padding: 32, color: 'rgba(255,255,255,0.35)' }}>
                          No transaction records found.
                        </td>
                      </tr>
                    )
                    : visible.map((r, i) => (
                      <tr key={i}>
                        <td>
                          <span className={`pill pill-${(r.status ?? '').toLowerCase()}`}>
                            {r.initCode}
                          </span>
                        </td>
                        <td>{r.orgCode}</td>
                        <td className="mono">{r.wipJobNo}</td>
                        <td className="mono">{r.esn}</td>
                        <td className="mono">{r.actualMsbm}</td>
                        <td>{r.status}</td>
                        <td>{r.oracleStatus}</td>
                        <td className="mono">{r.receivedDate}</td>
                        <td className="num">{r.groupId}</td>
                      </tr>
                    ))
                  }
                </tbody>
              </table>
            </ScrollTable>
          </div>

          <Pager
            page={page}
            totalPages={totalPages}
            onPrev={() => setPage(p => Math.max(1, p - 1))}
            onNext={() => setPage(p => Math.min(totalPages, p + 1))}
          />

          {/* ERP Subinventory */}
          {erpRows.length > 0 && (
            <div style={{ marginTop: 24 }}>
              <div className="section-title" style={{ marginBottom: 8 }}>ERP Subinventory</div>
              <div className="report-table-box" style={{ maxWidth: 360 }}>
                <table className="report-table">
                  <thead>
                    <tr>
                      <th>Subinventory</th>
                      <th className="num">Qty</th>
                    </tr>
                  </thead>
                  <tbody>
                    {erpRows.map(r => (
                      <tr key={r.subinventory}>
                        <td>{r.subinventory}</td>
                        <td className="num">{r.qty}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}
    </>
  )
}

// ═══════════════════════════════════════════════════════════════════
// PAGE ROOT
// ═══════════════════════════════════════════════════════════════════
export default function ProductionReport() {
  const today = new Date()
  const fmt   = d => d.toISOString().slice(0, 10)

  const [selectedDate, setSelectedDate] = useState(fmt(today))
  const [queriedDate,  setQueriedDate]  = useState(fmt(today))
  const [activeTab,    setActiveTab]    = useState('fes')

  const [kpis,     setKpis]     = useState(null)
  const [kpiLoad,  setKpiLoad]  = useState(true)
  const [kpiError, setKpiError] = useState(null)

  // Reload KPIs whenever queriedDate changes
  useEffect(() => {
    setKpiLoad(true)
    setKpiError(null)
    fetchKpis(queriedDate)
      .then(setKpis)
      .catch(err => setKpiError(err.message ?? 'Failed to load KPIs.'))
      .finally(() => setKpiLoad(false))
  }, [queriedDate])

  const handleQuery = () => setQueriedDate(selectedDate)

  const todayLabel = today.toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric',
  })

  return (
    <div className="dash-root">
      <Sidebar />
      <div className="dash-main">

        {/* ── Top bar ── */}
        <div className="dash-topbar">
          <div className="dash-topbar-left">
            <span className="dash-page-title">Production Report</span>
            <span className="dash-date">{todayLabel}</span>
          </div>

          <div className="pr-date-bar">
            <label className="pr-date-label">Date</label>
            <input
              type="date"
              className="pr-date-input"
              value={selectedDate}
              onChange={e => setSelectedDate(e.target.value)}
            />
            <button className="pr-query-btn" onClick={handleQuery}>
              Query
            </button>
          </div>
        </div>

        <div className="dash-content">

          {/* ── KPI strip ── */}
          <KpiStrip kpis={kpis} loading={kpiLoad} error={kpiError} />

          {/* ── Tab switcher ── */}
          <div className="pr-tabs">
            <button
              className={`pr-tab${activeTab === 'fes' ? ' pr-tab--active' : ''}`}
              onClick={() => setActiveTab('fes')}
            >
              FES Report
            </button>
            <button
              className={`pr-tab${activeTab === 'history' ? ' pr-tab--active' : ''}`}
              onClick={() => setActiveTab('history')}
            >
              TCL Engine Transaction History
            </button>
          </div>

          {/* ── Tab content ── */}
          {activeTab === 'fes'     && <FesReport date={queriedDate} />}
          {activeTab === 'history' && <EngineHistory />}

        </div>
      </div>
    </div>
  )
}
