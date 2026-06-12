import './Login.css'

// Login landing - app khud Windows user detect karke dikhata hain (legacy MES jaisa).
// "Log In" dabane pe DB check hota hain (parent handle karta hain).
export default function Login({ detected, onLogin, loading }) {
  return (
    <div className="login-root">
      <div className="login-topbar">
        <div className="login-logo">TCL</div>
        <div className="login-sys">MANUFACTURING EXECUTION SYSTEM</div>
      </div>
      <div className="login-accent" />

      <div className="login-body">
        <div className="login-card">
          <div className="login-avatar">👤</div>
          <div className="login-detail">
            <div className="login-username">
              Username : <span>{detected || 'detecting…'}</span>
            </div>
            <button className="login-btn" onClick={onLogin} disabled={loading || !detected}>
              {loading ? 'Checking…' : 'Log In'}
            </button>
          </div>
        </div>
        <div className="login-hint">Windows user detected successfully. Click Log In to continue.</div>
      </div>
    </div>
  )
}
