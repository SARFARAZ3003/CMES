import { NavLink, useNavigate } from 'react-router-dom'
import { currentUser } from '../data/mockData'
import './Sidebar.css'

const navItems = [
  { path: '/production-report', label: 'Production Report', icon: '📋' },
  { path: '/model-tracking',    label: 'Model Tracking',    icon: '🔍' },
]

export default function Sidebar() {
  const navigate = useNavigate()

  const handleLogout = () => {
    navigate('/')
  }

  return (
    <div className="sidebar">
      {/* Header */}
      <div className="sidebar-header">
        <div className="sidebar-logo">TCL</div>
        <div className="sidebar-title">CMES</div>
      </div>

      {/* User Info */}
      <div className="sidebar-user">
        <div className="sidebar-avatar">
          {currentUser.name.charAt(0)}
        </div>
        <div className="sidebar-user-info">
          <div className="sidebar-user-name">{currentUser.name}</div>
          <div className="sidebar-user-meta">
            {currentUser.code} · Shift {currentUser.shift}
          </div>
        </div>
      </div>

      {/* Nav Links */}
      <nav className="sidebar-nav">
        {navItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) =>
              `sidebar-link ${isActive ? 'active' : ''}`
            }
          >
            <span className="sidebar-icon">{item.icon}</span>
            <span>{item.label}</span>
          </NavLink>
        ))}
      </nav>

      {/* Logout */}
      <div className="sidebar-footer">
        <button className="sidebar-logout" onClick={handleLogout}>
          🚪 Log Out
        </button>
        <div className="sidebar-version">TCL CMES v2.0</div>
      </div>
    </div>
  )
}
