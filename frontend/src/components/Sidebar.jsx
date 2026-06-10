import './Sidebar.css'

// Single-page dashboard - sidebar sirf branding + active item dikhata hain.
const user = { name: 'Sarfaraz Ahmed', code: 'OD741', shift: 'A' }

export default function Sidebar() {
  return (
    <div className="sidebar">
      {/* Header */}
      <div className="sidebar-header">
        <div className="sidebar-logo">TCL</div>
        <div className="sidebar-title">CMES</div>
      </div>

      {/* User Info */}
      <div className="sidebar-user">
        <div className="sidebar-avatar">{user.name.charAt(0)}</div>
        <div className="sidebar-user-info">
          <div className="sidebar-user-name">{user.name}</div>
          <div className="sidebar-user-meta">{user.code} · Shift {user.shift}</div>
        </div>
      </div>

      {/* Nav */}
      <nav className="sidebar-nav">
        <div className="sidebar-link active">
          <span className="sidebar-icon">📊</span>
          <span>Production Dash</span>
        </div>
      </nav>

      {/* Footer */}
      <div className="sidebar-footer">
        <div className="sidebar-version">TCL CMES v2.0</div>
      </div>
    </div>
  )
}
