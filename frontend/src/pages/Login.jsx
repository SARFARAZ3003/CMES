import { useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { useUser } from '../context/UserContext'
import './Login.css'

export default function Login() {
  const navigate = useNavigate()
  const user     = useUser()
  const [loading, setLoading] = useState(false)

  const handleLogin = () => {
    setLoading(true)
    // TODO: call ASP.NET API to validate against AD
    setTimeout(() => navigate('/dashboard'), 1000)
  }

  return (
    <div className="login-root">
      <div className="bg-lines" />

      <div className="header-bar">
        <div className="cummins-logo">TCL</div>
        <div className="header-title">Manufacturing Execution System</div>
      </div>
      <div className="sub-bar">
        <span>Tata Cummins Limited &nbsp;·&nbsp; Plant: Jamshedpur</span>
      </div>

      <div className="login-body">
        <div className="login-card">

          <div className="avatar-wrap">
            <div className="avatar">
              <span className="avatar-initial">
                {user.name !== 'Unknown User' ? user.name.charAt(0) : '?'}
              </span>
            </div>
            <div className="dept-badge">Windows SSO</div>
          </div>

          {/* Single field — WWID, resolved from computer, not editable */}
          <div className="user-row">
            <span className="user-label">User ID</span>
            <div className="user-value user-id">{user.wwid}</div>
          </div>

          <p className="sso-note">
            Identity resolved via <span>Windows Active Directory</span>.<br />
            No password required.
          </p>

          <button
            className={`login-btn ${loading ? 'loading' : ''}`}
            onClick={handleLogin}
            disabled={loading}
          >
            {loading ? 'Signing in…' : 'Log In to MES'}
          </button>
        </div>
      </div>

      <div className="footer-bar">
        <span>TCL MES v2.0 &nbsp;·&nbsp; Intern Project 2026</span>
      </div>
    </div>
  )
}
