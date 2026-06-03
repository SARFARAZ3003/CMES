import { useNavigate } from 'react-router-dom'
import { useState } from 'react'
import './Login.css'

export default function Login() {
  const navigate = useNavigate()
  const [loading, setLoading] = useState(false)

  // Simulate Windows SSO — username auto-detected
  const windowsUser = 'CED\\od741'

  const handleLogin = () => {
    setLoading(true)
    // Later: call ASP.NET API here for AD validation
    setTimeout(() => {
      navigate('/dashboard')
    }, 1000)
  }

  return (
    <div className="login-root">
      <div className="bg-lines" />

      {/* Header */}
      <div className="header-bar">
        <div className="cummins-logo">TCL</div>
        <div className="header-title">Manufacturing Execution System</div>
      </div>
      <div className="sub-bar">
        <span>Tata Cummins Limited &nbsp;·&nbsp; Plant: Jamshedpur</span>
      </div>

      {/* Card */}
      <div className="login-body">
        <div className="login-card">
          <div className="avatar-wrap">
            <div className="avatar">
              <span className="avatar-icon">👤</span>
            </div>
            <div className="dept-badge">Windows SSO</div>
          </div>

          <div className="user-row">
            <span className="user-label">Username</span>
            <div className="user-value">{windowsUser}</div>
          </div>

          <p className="sso-note">
            Logged in via <span>Windows Active Directory</span>.<br />
            No password required.
          </p>

          <button
            className={`login-btn ${loading ? 'loading' : ''}`}
            onClick={handleLogin}
            disabled={loading}
          >
            {loading ? 'Signing in...' : 'Log In to MES'}
          </button>
        </div>
      </div>

      {/* Footer */}
      <div className="footer-bar">
        <span>TCL MES v2.0 &nbsp;·&nbsp; Intern Project 2026</span>
      </div>
    </div>
  )
}