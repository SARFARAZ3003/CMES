import Sidebar from '../components/Sidebar'
import { useState } from 'react'
import './WIPReport.css'

// Mock data for WIP locations
const wipLocationData = [
  { location: 'OLD LINE TEST', quantity: 0 },
  { location: 'OLD LINESET', quantity: 0 },
  { location: 'LINESET LINE', quantity: 15 },
  { location: 'LINESET LINE', quantity: 15 },
  { location: 'OLD LINE', quantity: 5 },
  { location: 'NEW LINE', quantity: 22 },
  { location: 'PRE FILL AREA', quantity: 41 },
  { location: 'PAINT LINE', quantity: 48 },
  { location: 'PAINT REPAIR', quantity: 3 },
  { location: 'NEW LINE TEST', quantity: 8 },
  { location: 'MRA', quantity: 1 },
  { location: 'PE', quantity: 15 },
  { location: 'TEST REWORK', quantity: 0 },
  { location: 'EQA AUDIT', quantity: 13 },
  { location: 'SHORT BUILD', quantity: 1 },
  { location: 'QUALITY DOCK', quantity: 91 },
]

// Mock data for detailed WIP items
const wipDetailData = [
  { esno: 'E8895257', modelNo: 'SOA1445', blockDateTime: '08-05-2025 07:36:42', jobOrderNo: '27038013-2', workstation: '34000', status: 'ISSUE', location: 'PE', lastUpdated: '28-05-2026 10:09:20' },
  { esno: 'E9006829', modelNo: 'SOA1445', blockDateTime: '12-05-2025 08:43:22', jobOrderNo: '27111157-2', workstation: '34000', status: 'ISSUE', location: 'PE', lastUpdated: '28-05-2026 11:36:43' },
  { esno: 'G6924099', modelNo: 'SOA0820', blockDateTime: '05-11-2025 14:02:18', jobOrderNo: '27002727-4', workstation: '51200', status: 'CONTAINMENT', location: 'QUALITY DOCK', lastUpdated: '02-04-2026 09:11:33' },
  { esno: 'G6852335', modelNo: 'SOA1820', blockDateTime: '22-03-2026 11:49:35', jobOrderNo: '28123733-1', workstation: '34000', status: 'CONTAINMENT', location: 'QUALITY DOCK', lastUpdated: '13-05-2026 07:59:53' },
  { esno: 'G6852986', modelNo: 'SOA0820', blockDateTime: '22-01-2026 09:39:29', jobOrderNo: '28237519-9', workstation: '51720', status: 'ISSUE', location: 'PE', lastUpdated: '29-03-2026 13:40:20' },
]

// Mock data for model tracking
const modelTrackingData = [
  { modelNo: 'SOA0815', fes: 1, wip: 1, qualityDock: 0, paintLine: 0, testCallLine: 0, paintRepair: 0, testRework: 0, shortBuild: 0, eqaAudit: 0, mra: 0, pe: 0, unknown: 0 },
  { modelNo: 'SOA0820', fes: 0, wip: 0, qualityDock: 0, paintLine: 0, testCallLine: 0, paintRepair: 0, testRework: 0, shortBuild: 0, eqaAudit: 2, mra: 0, pe: 0, unknown: 0 },
  { modelNo: 'SOA0835', fes: 1, wip: 0, qualityDock: 0, paintLine: 0, testCallLine: 1, paintRepair: 0, testRework: 0, shortBuild: 0, eqaAudit: 0, mra: 0, pe: 0, unknown: 0 },
  { modelNo: 'SOA1445', fes: 1, wip: 0, qualityDock: 0, paintLine: 0, testCallLine: 1, paintRepair: 0, testRework: 0, shortBuild: 0, eqaAudit: 0, mra: 0, pe: 0, unknown: 0 },
]

export default function WIPReport() {
  const [isCollapsed, setIsCollapsed] = useState(false)
  const [modelSearch, setModelSearch] = useState('')
  const [engineSearch, setEngineSearch] = useState('')

  const total = wipLocationData.reduce((sum, item) => sum + item.quantity, 0)

  return (
    <div className="wip-root">
      <Sidebar isCollapsed={isCollapsed} setIsCollapsed={setIsCollapsed} />
      <div className="wip-main">
        {/* Top Bar */}
        <div className="wip-topbar">
          <div className="wip-topbar-left">
            <span className="wip-page-title">WIP Report</span>
          </div>
        </div>

        <div className="wip-content">
          {/* WIP Report Section */}
          <div className="wip-section">
            <div className="wip-section-header">WIP REPORT</div>
            
            <div className="wip-grid">
              {/* Left: Location Summary */}
              <div className="wip-summary-box">
                <table className="wip-table">
                  <thead>
                    <tr>
                      <th>LOCATION</th>
                      <th>QUANTITY</th>
                    </tr>
                  </thead>
                  <tbody>
                    {wipLocationData.map((item, idx) => (
                      <tr key={idx}>
                        <td>{item.location}</td>
                        <td>{item.quantity}</td>
                      </tr>
                    ))}
                    <tr className="total-row">
                      <td><strong>TOTAL</strong></td>
                      <td><strong>{total}</strong></td>
                    </tr>
                  </tbody>
                </table>
              </div>

              {/* Right: Detailed WIP Table */}
              <div className="wip-detail-box">
                <div className="wip-table-scroll">
                  <table className="wip-table wip-detail-table">
                    <thead>
                      <tr>
                        <th>ESNO</th>
                        <th>MODEL NO</th>
                        <th>BLOCK DATE TIME</th>
                        <th>JOB ORDER NO</th>
                        <th>WORKSTATION</th>
                        <th>STATUS</th>
                        <th>LOCATION</th>
                        <th>LASTUPDATEDON</th>
                      </tr>
                    </thead>
                    <tbody>
                      {wipDetailData.map((item, idx) => (
                        <tr key={idx}>
                          <td>{item.esno}</td>
                          <td>{item.modelNo}</td>
                          <td>{item.blockDateTime}</td>
                          <td>{item.jobOrderNo}</td>
                          <td>{item.workstation}</td>
                          <td>{item.status}</td>
                          <td>{item.location}</td>
                          <td>{item.lastUpdated}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>

          {/* Model Wise Tracking Section */}
          <div className="wip-section">
            <div className="wip-section-header">MODEL WISE TRACKING</div>
            
            <div className="search-row">
              <label>MODEL NO.:</label>
              <input 
                type="text" 
                value={modelSearch} 
                onChange={(e) => setModelSearch(e.target.value)}
                placeholder="SOA0841"
              />
              <button className="search-btn">SEARCH</button>
            </div>

            <div className="wip-table-scroll">
              <table className="wip-table model-table">
                <thead>
                  <tr>
                    <th>MODEL NO</th>
                    <th>FES</th>
                    <th>WIP</th>
                    <th>QUALITY DOCK</th>
                    <th>PAINT LINE</th>
                    <th>TEST CALL LINE</th>
                    <th>PAINT REPAIR</th>
                    <th>TEST REWORK</th>
                    <th>SHORT BUILD</th>
                    <th>EQA AUDIT</th>
                    <th>MRA</th>
                    <th>PE</th>
                    <th>UNKNOWN</th>
                  </tr>
                </thead>
                <tbody>
                  {modelTrackingData.map((item, idx) => (
                    <tr key={idx}>
                      <td>{item.modelNo}</td>
                      <td>{item.fes}</td>
                      <td>{item.wip}</td>
                      <td>{item.qualityDock}</td>
                      <td>{item.paintLine}</td>
                      <td>{item.testCallLine}</td>
                      <td>{item.paintRepair}</td>
                      <td>{item.testRework}</td>
                      <td>{item.shortBuild}</td>
                      <td>{item.eqaAudit}</td>
                      <td>{item.mra}</td>
                      <td>{item.pe}</td>
                      <td>{item.unknown}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Engine Transaction History Section */}
          <div className="wip-section">
            <div className="wip-section-header">ENGINE TRANSACTION HISTORY</div>
            
            <div className="search-row">
              <label>Engine Serial No.:</label>
              <input 
                type="text" 
                value={engineSearch} 
                onChange={(e) => setEngineSearch(e.target.value)}
                placeholder="G9975029"
              />
              <button className="search-btn">QUERY</button>
            </div>

            <div className="info-grid">
              <div className="info-item">
                <span className="info-label">Model No.:</span>
                <span className="info-value">SOA0841</span>
              </div>
              <div className="info-item">
                <span className="info-label">Job No.:</span>
                <span className="info-value">28157797-33</span>
              </div>
              <div className="info-item">
                <span className="info-label">Current Location:</span>
                <span className="info-value">NEWLINE</span>
              </div>
            </div>
          </div>

          {/* EMS Transaction History Section */}
          <div className="wip-section">
            <div className="wip-section-header">EMS TRANSACTION HISTORY</div>
            
            <div className="ems-summary">
              <table className="wip-table ems-table">
                <thead>
                  <tr>
                    <th>TRANSACTIONTYPE</th>
                    <th>QTY</th>
                  </tr>
                </thead>
                <tbody>
                  <tr><td>COUN</td><td>3</td></tr>
                  <tr><td>EDOK</td><td>1</td></tr>
                  <tr><td>FES</td><td>100</td></tr>
                  <tr><td>FGS</td><td>0</td></tr>
                  <tr><td>PRE HEAD</td><td>36</td></tr>
                  <tr><td>TEST AT COND</td><td>0</td></tr>
                  <tr><td>TIMI</td><td>1</td></tr>
                  <tr><td>TOK</td><td>16</td></tr>
                  <tr><td>WIP</td><td>5</td></tr>
                  <tr><td>XHOL PR</td><td>1</td></tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
