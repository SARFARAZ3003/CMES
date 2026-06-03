import { NavLink, useNavigate } from 'react-router-dom'
import { currentUser } from '../data/mockData'
import { useState } from 'react'
import './Sidebar.css'

const navItems = [
  { path: '/dashboard',         label: 'Production Dash',  icon: '📊' },
  { path: '/wip-report',        label: 'WIP Report',       icon: '🔄' },
  { path: '/production-report', label: 'Production Report',icon: '📋' },
  { path: '/model-tracking',    label: 'Model Tracking',   icon: '🔍' },
  { path: '/inventory',         label: 'Inventory',        icon: '📦' },
]

export default function Sidebar({ isCollapsed, setIsCollapsed }) {
  const navigate = useNavigate()

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
            {currentUser.name.charAt(0)}
          </div>
          <div className="sidebar-user-info">
            <div className="sidebar-user-name">{currentUser.name}</div>
            <div className="sidebar-user-meta">
              {currentUser.code} · Shift {currentUser.shift}
            </div>
          </div>
        </div>
      )}
      {isCollapsed && (
        <div className="sidebar-user-collapsed">
          <div className="sidebar-avatar">
            {currentUser.name.charAt(0)}
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