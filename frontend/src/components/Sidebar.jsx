import { NavLink, useNavigate } from 'react-router-dom'
import { useUser } from '../context/UserContext'
import { useState } from 'react'
import './Sidebar.css'

const navItems = [
  { path: '/dashboard',  label: 'Production Dash', icon: '📊' },
  { path: '/wip-report', label: 'CMES Report',      icon: '📋' },
]

export default function Sidebar({ isCollapsed, setIsCollapsed }) {
  const navigate = useNavigate()
  const user     = useUser()

  const handleLogout = () => {
    navigate('/')
  }

  return (
    <div className={`sidebar ${isCollapsed ? 'collapsed' : ''}`}>
      {/* Header */}
      <div className="sidebar-header">
        <div className="sidebar-logo">TCL</div>
        {!isCollapsed && <div className="sidebar-title">MES</div>}
        <button 
          className="sidebar-toggle" 
          onClick={() => setIsCollapsed(!isCollapsed)}
          title={isCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        >
          {isCollapsed ? '→' : '←'}
        </button>
      </div>

      {/* User Info */}
      {!isCollapsed && (
        <div className="sidebar-user">
          <div className="sidebar-avatar">
            {user.name.charAt(0)}
          </div>
          <div className="sidebar-user-info">
            <div className="sidebar-user-name">{user.name}</div>
            <div className="sidebar-user-meta">
              {user.wwid} · Shift {user.shift}
            </div>
          </div>
        </div>
      )}
      {isCollapsed && (
        <div className="sidebar-user-collapsed">
          <div className="sidebar-avatar">
            {user.name.charAt(0)}
          </div>
        </div>
      )}

      {/* Nav Links */}
      <nav className="sidebar-nav">
        {navItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) =>
              `sidebar-link ${isActive ? 'active' : ''}`
            }
            title={isCollapsed ? item.label : ''}
          >
            <span className="sidebar-icon">{item.icon}</span>
            {!isCollapsed && <span>{item.label}</span>}
          </NavLink>
        ))}
      </nav>

      {/* Logout */}
      <div className="sidebar-footer">
        <button 
          className="sidebar-logout" 
          onClick={handleLogout}
          title="Log Out"
        >
          🚪 {!isCollapsed && 'Log Out'}
        </button>
        {!isCollapsed && <div className="sidebar-version">TCL MES v2.0</div>}
      </div>
    </div>
  )
}