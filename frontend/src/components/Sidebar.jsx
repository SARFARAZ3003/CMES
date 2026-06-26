import './Sidebar.css'

// User aata hain Windows Integrated Auth se (/api/auth/me).
// page + onNavigate se Production Dash / WIP Report ke beech switch hota.
export default function Sidebar({ user, onLogout, page = 'dashboard', onNavigate }) {
  const name = user?.fullName || user?.username || 'User'
  const username = user?.username || ''
  const role = user?.role || ''

  return (
    <div className="sidebar">
      {/* Header */}
      <div className="sidebar-header">
        <div className="sidebar-logo">TCL</div>
        <div className="sidebar-title">CMES</div>
      </div>

      {/* User Info */}
      <div className="sidebar-user">
        <div className="sidebar-avatar">{name.charAt(0).toUpperCase()}</div>
        <div className="sidebar-user-info">
          <div className="sidebar-user-name">{name}</div>
          <div className="sidebar-user-meta">
            {username}{role ? ` · ${role}` : ''}
          </div>
        </div>
      </div>

      {/* Nav */}
      <nav className="sidebar-nav">
        <div
          className={`sidebar-link ${page === 'dashboard' ? 'active' : ''}`}
          onClick={() => onNavigate?.('dashboard')}
        >
          <span className="sidebar-icon">📊</span>
          <span>Production Dash</span>
        </div>
        <div
          className={`sidebar-link ${page === 'wip' ? 'active' : ''}`}
          onClick={() => onNavigate?.('wip')}
        >
          <span className="sidebar-icon">📋</span>
          <span>WIP Report</span>
        </div>
      </nav>

      {/* Footer */}
      <div className="sidebar-footer">
        {onLogout && (
          <button className="sidebar-logout" onClick={onLogout}>
            <span className="sidebar-icon">🚪</span>
            <span>Log Out</span>
          </button>
        )}
        <div className="sidebar-version">TCL CMES v2.0</div>
      </div>
    </div>
  )
}
