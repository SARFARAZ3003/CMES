/**
 * WipReport.jsx
 *
 * Two-panel WIP report page.
 * Left  (~30%) – WIP Summary : location list; clicking a row filters the right panel.
 * Right (~70%) – WIP Details : full engine-level rows for the selected location.
 *
 * WIP summary and details are loaded from the ASP.NET backend.
 */

import { useState, useEffect, useCallback } from 'react'
import * as XLSX from 'xlsx'
import Sidebar from '../components/Sidebar'
import './WipReport.css'

// ═══════════════════════════════════════════════════════════════════════════════
// ═══════════════════════════════════════════════════════════════════════════════
// ─── Status badge colour map ──────────────────────────────────────────────────
const STATUS_COLORS = {
  'IN-PROD':     '#4CAF50',
  'ISSUE':       '#FF9800',
  'IN REPAIR':   '#2196F3',
  'UNKNOWN':     '#9E9E9E',
}

// ─── Data-access functions ────────────────────────────────────────────────────
// These three functions are the only integration seam.
// Swap the implementation inside each function when a new endpoint is ready —
// the component state, rendering, and interaction logic never change.

/**
 * WIP summary locations follow the original application's category names
 * and fixed MQUERY order. Missing categories render as 0.
 *
 * Live — hits the real ASP.NET endpoint.
 */
const locationOrder = [
  'R12 LINESET',
  'LINESET',
  'LINESET LINE',
  'OLD LINE',
  'NEW LINE',
  'TEST CELL LINE',
  'PAINT LINE',
  'QUALITY DOCK',
  'PAINT REPAIR',
  'NEWLINE LOOP',
  'MRA',
  'EQA AUDIT',
  'TEST REWORK',
  'PE',
  'SHORT BUILD',
  'UNKNOWN',
]

const LOCATION_DISPLAY_NAMES = {
  'OTHERS': 'UNKNOWN',
  'OTHER': 'UNKNOWN',
  'UNKNOWN': 'UNKNOWN',
  'TEST CELL': 'TEST CELL LINE',
  'TESTCELL': 'TEST CELL LINE',
  'QUALITY DOCK': 'QUALITY DOCK',
  'PAINT LINE': 'PAINT LINE',
  'NEWLINE': 'NEW LINE',
  'NEW LINE': 'NEW LINE',
  'OLDLINE': 'OLD LINE',
  'OLD LINE': 'OLD LINE',
  'LINESET': 'LINESET',
  'LINESET LINE': 'LINESET LINE',
  'R12 LINESET': 'R12 LINESET',
  'PAINT REPAIR': 'PAINT REPAIR',
  'NEWLINE LOOP': 'NEWLINE LOOP',
  'MRA': 'MRA',
  'EQA AUDIT': 'EQA AUDIT',
  'TEST REWORK': 'TEST REWORK',
  'PE': 'PE',
  'SHORT BUILD': 'SHORT BUILD',
}

// Maps frontend display labels to the URL slugs accepted by the backend.
// Keys are UPPERCASE display labels (what the summary table shows).
// Values are the slug strings defined in WipController.LocationRouteAliases.
// Keep this in sync with the backend dictionary — if a new category is added
// to LocationRouteAliases, add an entry here.
const LOCATION_SLUGS = {
  'R12 LINESET':    'r12-lineset',
  'LINESET':        'lineset',
  'LINESET LINE':   'lineset-line',
  'OLD LINE':       'old-line',
  'OLDLINE':        'old-line',       // Oracle value — backend also accepts this
  'NEW LINE':       'new-line',
  'NEWLINE':        'new-line',       // Oracle value
  'TEST CELL LINE': 'test-cell-line',
  'TEST CELL':      'test-cell',
  'PAINT LINE':     'paint-line',
  'PAINT REPAIR':   'paint-repair',
  'QUALITY DOCK':   'quality-dock',
  'NEWLINE LOOP':   'newline-loop',
  'MRA':            'mra',
  'EQA AUDIT':      'eqa-audit',
  'TEST REWORK':    'test-rework',
  'PE':             'pe',
  'SHORT BUILD':    'short-build',
  'UNKNOWN':        'unknown',
}

function normalizeLocationName(location) {
  const key = String(location ?? 'UNKNOWN').trim().toUpperCase()
  return LOCATION_DISPLAY_NAMES[key] ?? key
}

function orderLocations(rows) {
  const quantitiesByLocation = new Map(locationOrder.map(location => [location, 0]))

  rows.forEach(row => {
    const location = normalizeLocationName(row.location)
    const quantity = Number(row.quantity ?? 0)
    quantitiesByLocation.set(location, (quantitiesByLocation.get(location) ?? 0) + quantity)
  })

  return locationOrder.map(location => ({
    location,
    quantity: quantitiesByLocation.get(location) ?? 0,
  }))
}

function formatDateTime(value) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value

  return date.toLocaleString('en-IN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  })
}

/**
 * GET /api/wip/locations
 * Returns: Array<{ location: string, count: number }>
 *
 * Live — hits the real ASP.NET endpoint.
 * Response field "count" is normalised to "quantity" here so the
 * summary table never needs to know about the backend field name.
 */
async function fetchLocations() {
  const url = '/api/wip/locations'
  console.log('[WipReport] fetchLocations →', url)
  const res = await fetch(url)
  console.log('[WipReport] fetchLocations ←', res.status, res.statusText)
  if (!res.ok) {
    const body = await res.text().catch(() => '(unreadable)')
    console.error('[WipReport] fetchLocations body:', body)
    throw new Error(`Locations fetch failed: ${res.status}`)
  }
  const data = await res.json()
  console.log('[WipReport] fetchLocations data:', data)
  // Normalise { location, count } → { location, quantity }
  return orderLocations(data.map(r => ({ location: r.location, quantity: r.count })))
}

/**
 * GET /api/wip/details/{location}?page=1&pageSize=100
 *
 * Passes the location as a URL slug (e.g. "old-line") so the backend
 * resolves it via LocationRouteAliases to the canonical name ("OLD LINE")
 * before running the WORKSTATION-based filter.
 *
 * Backend returns a paginated envelope:
 *   { page, pageSize, totalCount, totalPages, items: [...] }
 *
 * Field mapping:
 *   createdOn   → blockLoadTime  (CREATEDON = block load timestamp)
 *   productId   → modelNo        (raw PRODUCTID until PRODUCT join is added)
 *   workOrderNo → jobOrderNo
 */
async function fetchDetails(location, page = 1, pageSize = 500) {
  const slug = location ? (LOCATION_SLUGS[location.toUpperCase()] ?? location) : null
  const base = slug ? `/api/wip/details/${slug}` : '/api/wip/details'
  const url  = `${base}?page=${page}&pageSize=${pageSize}`
  console.log('[WipReport] fetchDetails →', url, '(location:', location, ')')
  const res = await fetch(url)
  console.log('[WipReport] fetchDetails ←', res.status, res.statusText)
  if (!res.ok) {
    const body = await res.text().catch(() => '(unreadable)')
    console.error('[WipReport] fetchDetails body:', body)
    throw new Error(`Details fetch failed: ${res.status}`)
  }
  const envelope = await res.json()
  console.log('[WipReport] fetchDetails totalCount:', envelope.totalCount, 'page:', envelope.page)

  const items = (envelope.items ?? envelope).map(row => ({
    serialNo:      row.serialNo,
    modelNo:       row.productId,
    blockLoadTime: formatDateTime(row.createdOn),
    jobOrderNo:    row.workOrderNo,
    workstation:   row.workstation,
    status:        row.status,
    location:      row.location,
    lastUpdatedOn: formatDateTime(row.lastUpdatedOn),
  }))

  return { items, totalCount: envelope.totalCount ?? items.length }
}

// ─── Excel export ────────────────────────────────────────────────────────────
/**
 * exportToExcel(rows, location)
 *
 * Client-side export using the xlsx library.
 * Exports exactly the rows currently displayed in the details panel.
 *
 * File name format:  WIP_<LOCATION|ALL>_<yyyyMMdd_HHmm>.xlsx
 * Examples:
 *   WIP_LINESET_20260611_2130.xlsx      (location selected)
 *   WIP_ALL_20260611_2130.xlsx          (no filter)
 *
 * ── Future backend export hook ────────────────────────────────────────────────
 * If datasets grow too large for client-side processing (>10 k rows), replace
 * the body of this function with a server-side call:
 *
 *   const loc = location ?? 'ALL'
 *   const url = location
 *     ? `/api/wip/export?location=${encodeURIComponent(location)}`
 *     : '/api/wip/export'
 *   const res  = await fetch(url)
 *   const blob = await res.blob()
 *   const a    = document.createElement('a')
 *   a.href     = URL.createObjectURL(blob)
 *   a.download = buildFileName(location)
 *   a.click()
 *   URL.revokeObjectURL(a.href)
 *
 * The backend endpoint would return an application/vnd.openxmlformats-officedocument
 * .spreadsheetml.sheet stream generated by ClosedXML or EPPlus.
 * ─────────────────────────────────────────────────────────────────────────────
 */
function buildFileName(location) {
  const now   = new Date()
  const pad   = n => String(n).padStart(2, '0')
  const stamp = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}_${pad(now.getHours())}${pad(now.getMinutes())}`
  const loc   = location ? location.replace(/\s+/g, '_').toUpperCase() : 'ALL'
  return `WIP_${loc}_${stamp}.xlsx`
}

function exportToExcel(rows, location) {
  // Map rows to the exact column order and headers required
  const sheetData = rows.map(r => ({
    'Serial No':       r.serialNo,
    'Model No':        r.modelNo,
    'Block Load Time': r.blockLoadTime,
    'Job Order No':    r.jobOrderNo,
    'Workstation':     r.workstation,
    'Status':          r.status,
    'Location':        normalizeLocationName(r.location),
    'Last Updated On': r.lastUpdatedOn,
  }))

  const worksheet = XLSX.utils.json_to_sheet(sheetData)
  const workbook  = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(workbook, worksheet, 'WIP Details')

  // Auto-fit column widths based on content length
  const colWidths = Object.keys(sheetData[0] ?? {}).map(key => ({
    wch: Math.max(
      key.length,
      ...sheetData.map(r => String(r[key] ?? '').length)
    ) + 2,
  }))
  worksheet['!cols'] = colWidths

  XLSX.writeFile(workbook, buildFileName(location))
}
export default function WipReport() {
  const [selectedLocation, setSelectedLocation] = useState(null)

  // Location summary table — from GET /api/wip/locations
  const [summary, setSummary]               = useState([])
  const [summaryLoading, setSummaryLoading] = useState(true)
  const [summaryError, setSummaryError]     = useState(null)

  // Detail rows — from GET /api/wip/details/{location}
  const [detailRows, setDetailRows]         = useState([])
  const [detailLoading, setDetailLoading]   = useState(true)
  const [detailError, setDetailError]       = useState(null)
  const [detailPage, setDetailPage]         = useState(1)
  const [detailTotalPages, setDetailTotalPages] = useState(1)

  const today = new Date().toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric',
  })

  // Load location summary once on mount — GET /api/wip/locations
  useEffect(() => {
    setSummaryLoading(true)
    setSummaryError(null)
    fetchLocations()
      .then(setSummary)
      .catch(err => setSummaryError(err.message))
      .finally(() => setSummaryLoading(false))
  }, [])

  // Reload details whenever selectedLocation or detailPage changes
  const loadDetails = useCallback(() => {
    setDetailLoading(true)
    setDetailError(null)
    fetchDetails(selectedLocation, detailPage)
      .then(({ items, totalCount }) => {
        setDetailRows(items)
        setDetailTotalPages(Math.max(1, Math.ceil(totalCount / 100)))
      })
      .catch(err => setDetailError(err.message))
      .finally(() => setDetailLoading(false))
  }, [selectedLocation, detailPage])

  useEffect(() => { loadDetails() }, [loadDetails])

  // Reset to page 1 whenever the selected location changes
  const handleRowClick = (location) => {
    setDetailPage(1)
    setSelectedLocation(prev => (prev === location ? null : location))
  }

  const totalWip = summary.reduce((sum, row) => sum + Number(row.quantity ?? 0), 0)
  const activeLocations = summary.filter(row => Number(row.quantity ?? 0) > 0).length
  const allEngines = totalWip

  return (
    <div className="dash-root">
      <Sidebar />

      <div className="dash-main">

        {/* ── Top Bar ── */}
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

          {/* ── KPI strip — calculated from GET /api/wip/locations ── */}
          <div className="kpi-row" style={{ gridTemplateColumns: 'repeat(3, 1fr)', marginBottom: 20 }}>
            <div className="kpi-card" style={{ borderTopColor: '#2196F3' }}>
              <div className="kpi-value" style={{ color: '#2196F3' }}>
                {summaryLoading ? '—' : summaryError ? '!' : totalWip}
              </div>
              <div className="kpi-label">Total WIP</div>
              <div className="kpi-sub">in plant</div>
            </div>
            <div className="kpi-card" style={{ borderTopColor: '#FF9800' }}>
              <div className="kpi-value" style={{ color: '#FF9800' }}>
                {summaryLoading ? '—' : summaryError ? '!' : activeLocations}
              </div>
              <div className="kpi-label">Active Locations</div>
            </div>
            <div className="kpi-card" style={{ borderTopColor: '#4CAF50' }}>
              <div className="kpi-value" style={{ color: '#4CAF50' }}>
                {summaryLoading ? '—' : summaryError ? '!' : allEngines}
              </div>
              <div className="kpi-label">All Engines</div>
            </div>
          </div>

          {/* ── Two-panel layout ── */}
          <div className="wip-panels">

            {/* LEFT – Summary */}
            <div className="chart-box wip-left">
              <div className="wip-panel-title">
                WIP Summary
                {selectedLocation && (
                  <button className="wip-clear-btn" onClick={() => { setDetailPage(1); setSelectedLocation(null) }}>
                    Clear ✕
                  </button>
                )}
              </div>

              <div className="wip-table-wrap">
                {summaryError && <div className="wip-empty wip-error">{summaryError}</div>}
                {!summaryError && (
                  <table className="wip-table">
                    <thead>
                      <tr>
                        <th>Location</th>
                        <th className="wip-th-num">Qty</th>
                      </tr>
                    </thead>
                    <tbody>
                      {summaryLoading
                        ? Array.from({ length: 5 }).map((_, i) => (
                            <tr key={i} className="wip-skeleton-row">
                              <td><span className="wip-skeleton" /></td>
                              <td><span className="wip-skeleton wip-skeleton-sm" /></td>
                            </tr>
                          ))
                        : summary.map(row => (
                            <tr
                              key={row.location}
                              className={`wip-summary-row ${selectedLocation === row.location ? 'wip-row-selected' : ''}`}
                              onClick={() => handleRowClick(row.location)}
                            >
                              <td>{row.location}</td>
                              <td className="wip-th-num">
                                <span className="wip-qty-badge">{row.quantity}</span>
                              </td>
                            </tr>
                          ))
                      }
                      {!summaryLoading && (
                        <tr className="wip-total-row">
                          <td>TOTAL</td>
                          <td className="wip-th-num">
                            <span className="wip-qty-badge wip-qty-total">
                              {summary.reduce((s, r) => s + r.quantity, 0)}
                            </span>
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                )}
              </div>
            </div>

            {/* RIGHT – Details */}
            <div className="chart-box wip-right">
              <div className="wip-panel-title">
                WIP Details
                <span className="wip-panel-sub">
                  {selectedLocation ? ` — ${selectedLocation}` : ' — All Locations'}
                </span>
                {/* Export button — disabled when no rows are displayed or details are loading */}
                <button
                  className="wip-export-btn"
                  onClick={() => exportToExcel(detailRows, selectedLocation)}
                  disabled={detailLoading || detailRows.length === 0}
                  title={detailRows.length === 0 ? 'No rows to export' : `Export ${detailRows.length} rows to Excel`}
                >
                  ⬇ Export Excel
                </button>
              </div>

              <div className="wip-table-wrap">
                {detailError && <div className="wip-empty wip-error">{detailError}</div>}
                {!detailError && detailLoading && (
                  <div className="wip-empty">Loading…</div>
                )}
                {!detailError && !detailLoading && detailRows.length === 0 && (
                  <div className="wip-empty">No records for this location.</div>
                )}
                {!detailError && !detailLoading && detailRows.length > 0 && (
                  <table className="wip-table wip-detail-table">
                    <thead>
                      <tr>
                        <th>SERIAL NO</th>
                        <th>MODEL NO</th>
                        <th>BLOCK LOAD TIME</th>
                        <th>JOB ORDER NO</th>
                        <th>WORKSTATION</th>
                        <th>STATUS</th>
                        <th>LOCATION</th>
                        <th>LAST UPDATED ON</th>
                      </tr>
                    </thead>
                    <tbody>
                      {detailRows.map((row, idx) => (
                        <tr key={idx}>
                          <td className="wip-mono">{row.serialNo}</td>
                          <td>{row.modelNo}</td>
                          <td className="wip-mono">{row.blockLoadTime}</td>
                          <td className="wip-mono">{row.jobOrderNo}</td>
                          <td>{row.workstation}</td>
                          <td>
                            <span
                              className="wip-status-badge"
                              style={{
                                background:  `${STATUS_COLORS[row.status] ?? '#555'}22`,
                                color:        STATUS_COLORS[row.status] ?? '#aaa',
                                borderColor: `${STATUS_COLORS[row.status] ?? '#555'}55`,
                              }}
                            >
                              {row.status}
                            </span>
                          </td>
                          <td>{normalizeLocationName(row.location)}</td>
                          <td className="wip-mono">{row.lastUpdatedOn}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>{/* /wip-table-wrap */}

              {/* Pager — only shown when there is more than one page */}
              {!detailLoading && detailTotalPages > 1 && (
                <div className="wip-pager">
                  <button
                    className="wip-pager-btn"
                    onClick={() => setDetailPage(p => Math.max(1, p - 1))}
                    disabled={detailPage <= 1}
                  >
                    ‹ Prev
                  </button>
                  <span className="wip-pager-info">
                    Page {detailPage} of {detailTotalPages}
                  </span>
                  <button
                    className="wip-pager-btn"
                    onClick={() => setDetailPage(p => Math.min(detailTotalPages, p + 1))}
                    disabled={detailPage >= detailTotalPages}
                  >
                    Next ›
                  </button>
                </div>
              )}
            </div>{/* /wip-right */}

          </div>{/* /wip-panels */}
        </div>{/* /dash-content */}
      </div>{/* /dash-main */}
    </div>
  )
}
